namespace OktaIA.Web.Models;

/// <summary>Como o conector se autentica na API do fabricante.</summary>
public enum TipoAuthConector { ApiKey, OAuth2, Certificado, Iam }

/// <summary>Estado operacional do conector instalado.</summary>
public enum StatusConector
{
    /// <summary>Instalado mas nunca conectou — credencial ainda não validada.</summary>
    NuncaConectado,
    Ativo,
    /// <summary>Pausado pelo gestor. Não revoga credencial, só para o sync recorrente.</summary>
    Pausado,
    /// <summary>Última tentativa falhou. Ver <see cref="UltimoErro"/>.</summary>
    Erro,
}

/// <summary>
/// Instância instalada de um conector para UMA empresa. O mesmo fabricante pode estar instalado
/// em várias empresas, cada uma com sua credencial e seu cursor — por isso a chave é
/// (CompanyId, Slug) e não só o slug.
/// </summary>
public class Conector
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>Identificador do adaptador no código ("wazuh", "defender", "fortigate").</summary>
    public required string Slug { get; set; }

    public required string Nome { get; set; }
    public required string Categoria { get; set; }  // "EDR / XDR", "SIEM", "Firewall"...
    public required string Fabricante { get; set; }

    public TipoAuthConector TipoAuth { get; set; }
    public StatusConector Status { get; set; } = StatusConector.NuncaConectado;

    /// <summary>Endereço base da API quando o produto é auto-hospedado (Wazuh, pfSense, FortiGate).</summary>
    public string? UrlBase { get; set; }

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public string? CriadoPor { get; set; }

    // Saúde observada — alimenta a tela de Conectores e a de Observabilidade, hoje maquete.
    public DateTimeOffset? UltimoSyncEm { get; set; }
    public DateTimeOffset? UltimoHealthCheckEm { get; set; }
    public int? LatenciaMs { get; set; }
    public string? UltimoErro { get; set; }
    public DateTimeOffset? UltimoErroEm { get; set; }

    public CredencialConector? Credencial { get; set; }
    public List<CursorSync> Cursores { get; set; } = [];
}
