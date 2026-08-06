namespace OktaIA.Web.Models;

/// <summary>Trilha de auditoria do console de Administração — quem fez o quê, quando.</summary>
public class AdminAuditLog
{
    public int Id { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public required string Acao { get; set; } // ex.: "empresa.criada", "usuario.convidado"
    public required string Detalhe { get; set; }
    public required string Autor { get; set; }
    public string? OrigemIp { get; set; }
}
