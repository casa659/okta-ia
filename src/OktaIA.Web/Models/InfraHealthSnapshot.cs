namespace OktaIA.Web.Models;

// Leitura periódica de saúde de infraestrutura — Dashboard mostra sempre a mais recente;
// histórico fica gravado pra permitir gráfico de tendência numa fase futura.
public class InfraHealthSnapshot
{
    public int Id { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public int CpuPct { get; set; }
    public int RamPct { get; set; }
    public int DiscoPct { get; set; }
    public int RedePct { get; set; }
    public int LatenciaMs { get; set; }
}
