using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OktaIA.Web.Services.Integracoes;

/// <summary>
/// Cifra e decifra o JSON de credencial dos conectores com AES-GCM.
///
/// Por que não o Data Protection do ASP.NET: o projeto não tem key ring persistido configurado, e
/// passar a configurar um agora invalidaria os cookies de sessão já emitidos (todo mundo deslogado).
/// Credencial de terceiro também não deveria depender da infraestrutura de sessão do site — são
/// ciclos de vida diferentes: cookie pode ser descartado à vontade, credencial não.
///
/// A chave vem de configuração (`Integracoes:ChaveCriptografia`, 32 bytes em base64) e NUNCA do
/// código ou do banco — quem tem o banco não tem a chave. Se a chave sumir, as credenciais viram
/// lixo irrecuperável e cada cliente precisa emitir de novo; por isso ela é backup obrigatório.
///
/// AES-GCM dá confidencialidade e autenticidade juntas: um blob adulterado no banco falha ao
/// decifrar em vez de devolver dado corrompido silenciosamente.
/// </summary>
public class ProtetorDeCredencial
{
    private const int TamanhoNonce = 12;   // 96 bits, recomendado pra GCM
    private const int TamanhoTag = 16;     // 128 bits
    private readonly byte[] _chave;

    public ProtetorDeCredencial(IConfiguration config)
    {
        var bruta = config["Integracoes:ChaveCriptografia"];
        if (string.IsNullOrWhiteSpace(bruta))
        {
            // Sem chave o serviço existe mas recusa operar. Falhar aqui, na tela de integração,
            // é muito melhor do que gravar credencial em claro por engano.
            _chave = [];
            return;
        }

        var bytes = Convert.FromBase64String(bruta);
        if (bytes.Length != 32)
        {
            throw new InvalidOperationException(
                $"Integracoes:ChaveCriptografia precisa ter 32 bytes (256 bits) em base64; veio com {bytes.Length}.");
        }

        _chave = bytes;
    }

    public bool Configurado => _chave.Length == 32;

    /// <summary>Cifra o dicionário de campos da credencial. Saída: base64 de nonce+tag+cifrado.</summary>
    public string Proteger(IReadOnlyDictionary<string, string> campos)
    {
        GarantirConfigurado();

        var claro = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(campos));
        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var cifrado = new byte[claro.Length];
        var tag = new byte[TamanhoTag];

        using (var aes = new AesGcm(_chave, TamanhoTag))
        {
            aes.Encrypt(nonce, claro, cifrado, tag);
        }

        // Layout: [nonce][tag][cifrado] — tudo num blob só, pra caber numa coluna de texto.
        var saida = new byte[TamanhoNonce + TamanhoTag + cifrado.Length];
        nonce.CopyTo(saida, 0);
        tag.CopyTo(saida, TamanhoNonce);
        cifrado.CopyTo(saida, TamanhoNonce + TamanhoTag);

        return Convert.ToBase64String(saida);
    }

    /// <summary>Decifra. Lança se a chave estiver errada ou o blob tiver sido adulterado.</summary>
    public IReadOnlyDictionary<string, string> Revelar(string protegido)
    {
        GarantirConfigurado();

        var bytes = Convert.FromBase64String(protegido);
        if (bytes.Length < TamanhoNonce + TamanhoTag)
        {
            throw new CryptographicException("Blob de credencial menor que o cabeçalho mínimo — dado corrompido.");
        }

        var nonce = bytes.AsSpan(0, TamanhoNonce);
        var tag = bytes.AsSpan(TamanhoNonce, TamanhoTag);
        var cifrado = bytes.AsSpan(TamanhoNonce + TamanhoTag);
        var claro = new byte[cifrado.Length];

        using (var aes = new AesGcm(_chave, TamanhoTag))
        {
            aes.Decrypt(nonce, cifrado, tag, claro);
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(claro)
               ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Dica não-sensível pra UI mostrar qual credencial está guardada sem revelá-la —
    /// só os 4 últimos caracteres do campo indicado.
    /// </summary>
    public static string Referencia(string valor) =>
        valor.Length <= 4 ? "…" : $"…{valor[^4..]}";

    /// <summary>Gera uma chave nova pronta pra colar no App Setting. Usada só no setup.</summary>
    public static string GerarChaveBase64() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private void GarantirConfigurado()
    {
        if (!Configurado)
        {
            throw new InvalidOperationException(
                "Integracoes:ChaveCriptografia não está definida — sem ela nenhuma credencial de conector " +
                "pode ser lida ou gravada. Defina o App Setting antes de instalar qualquer conector.");
        }
    }
}
