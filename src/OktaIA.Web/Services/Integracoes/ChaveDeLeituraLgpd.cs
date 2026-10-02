using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Integracoes;

/// <summary>
/// Gera e confere a chave com que o LinkEscola lê o monitoramento de UMA empresa.
///
/// Fica separada do <see cref="ProtetorDeCredencial"/> porque resolve o problema CONTRÁRIO: aquele
/// guarda segredo de terceiro que precisamos usar depois, então cifra e decifra; este guarda um
/// segredo NOSSO que só precisamos reconhecer, então faz hash e nunca volta. Misturar os dois
/// levaria a cifrar esta chave — e a cifra, por definição, pode ser desfeita por quem tem a chave
/// do cofre, o que é exatamente o que não se quer para uma credencial que não precisa ser lida.
/// </summary>
public class ChaveDeLeituraLgpd
{
    private readonly ApplicationDbContext _db;

    public ChaveDeLeituraLgpd(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Cria uma chave nova para a empresa e devolve o valor EM CLARO — a única vez que ele existe.
    ///
    /// ⚠️ Substitui a anterior. Gerar de novo DERRUBA a integração que estava funcionando, e a tela
    /// avisa isso antes: sem o aviso, alguém geraria "só para ver como é" e o painel do DPO pararia
    /// de atualizar sem nenhum erro aparente do lado dele.
    /// </summary>
    public async Task<string> GerarAsync(Company empresa, string quem, CancellationToken ct = default)
    {
        // 32 bytes de aleatório criptográfico. Base64 url-safe para caber num cabeçalho HTTP sem
        // escape — `+` e `/` em cabeçalho são o tipo de detalhe que falha em um proxy e funciona em
        // outro, e o sintoma seria "testei aqui e deu certo, no cliente não".
        var bruto = RandomNumberGenerator.GetBytes(32);
        var valor = "lgpd_" + Convert.ToBase64String(bruto)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        empresa.ChaveLgpdHash = Hash(valor);
        empresa.ChaveLgpdPrefixo = valor[..13];   // "lgpd_" + 8 caracteres
        empresa.ChaveLgpdEm = DateTimeOffset.UtcNow;
        empresa.ChaveLgpdPor = quem;
        empresa.ChaveLgpdUsoEm = null;            // chave nova nunca foi usada; herdar a data mentiria

        await _db.SaveChangesAsync(ct);
        return valor;
    }

    /// <summary>Apaga a chave. A leitura externa daquela empresa para de existir na hora.</summary>
    public async Task RevogarAsync(Company empresa, CancellationToken ct = default)
    {
        empresa.ChaveLgpdHash = null;
        empresa.ChaveLgpdPrefixo = null;
        empresa.ChaveLgpdEm = null;
        empresa.ChaveLgpdPor = null;
        empresa.ChaveLgpdUsoEm = null;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Descobre de qual empresa é a chave que chegou. Nulo = nenhuma.
    ///
    /// 🔴 COMPARA O HASH, e por isso pode comparar no banco: o hash de uma chave errada não se
    /// parece com o de uma certa em nada, então não há informação a extrair do tempo de resposta —
    /// diferente da comparação de segredos em claro, onde `==` sai no primeiro byte diferente e o
    /// relógio entrega o valor byte a byte (ver o `/postura/trilha` do LinkEscola, que compara em
    /// tempo fixo justamente porque lá o segredo está em claro na configuração).
    /// </summary>
    public async Task<Company?> QuemEAsync(string? chaveRecebida, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chaveRecebida)) { return null; }

        var hash = Hash(chaveRecebida.Trim());
        var empresa = await _db.Companies.FirstOrDefaultAsync(c => c.ChaveLgpdHash == hash, ct);
        if (empresa is null) { return null; }

        // ⚠️ Marca o uso, mas NÃO deixa a falha de gravação derrubar a leitura: o carimbo é
        // diagnóstico, e perder um carimbo é muito menos grave do que devolver erro a uma
        // integração que estava autorizada.
        try
        {
            empresa.ChaveLgpdUsoEm = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch { /* o carimbo é acessório; a resposta não depende dele */ }

        return empresa;
    }

    private static string Hash(string valor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
}
