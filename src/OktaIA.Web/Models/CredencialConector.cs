namespace OktaIA.Web.Models;

/// <summary>
/// Segredo de acesso à API do fabricante, cifrado em repouso. Guarda um JSON com os campos que
/// aquele conector precisa (apiKey, ou clientId+clientSecret+tenantId, etc.) num blob único —
/// cada fabricante pede um conjunto diferente, e modelar coluna por campo engessaria.
///
/// REGRAS, e elas não são negociáveis:
/// - <see cref="SegredoCifrado"/> nunca sai desta camada em claro: não vai pra ViewModel, não vai
///   pra log, não volta pro formulário. Editar credencial é sempre substituir, nunca "ver e alterar".
/// - Quem grava/lê é o ProtetorDeCredencial. Nenhuma página deve chamar Encrypt/Decrypt direto.
/// </summary>
public class CredencialConector
{
    public int Id { get; set; }

    public int ConectorId { get; set; }
    public Conector? Conector { get; set; }

    /// <summary>JSON cifrado (AES-GCM, base64). Nunca logar, nunca expor.</summary>
    public required string SegredoCifrado { get; set; }

    /// <summary>
    /// Dica não-sensível pra UI identificar qual credencial está lá sem revelá-la
    /// (ex.: "client id a1b2…", "chave terminada em …9F4"). Preenchida pelo adaptador.
    /// </summary>
    public string? Referencia { get; set; }

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public string? CriadoPor { get; set; }
    public DateTimeOffset? RotacionadaEm { get; set; }
}
