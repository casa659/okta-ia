namespace OktaIA.Web.Models;

public enum Severidade { Baixa, Media, Alta, Critica }

// Evento bruto do "fluxo de eventos" / mapa de ataques — histórico real em banco (substitui o
// gerador aleatório em memória do mockup). KPIs do Dashboard (Eventos 24h, Bloqueados, mapa,
// principais origens) são agregações reais sobre esta tabela.
public class SecurityEvent
{
    public int Id { get; set; }

    // Dono do evento (tenant) — nullable pq eventos de infraestrutura própria da okta-ia não
    // pertencem a nenhum cliente. Resolvido no seed/backfill casando Alvo com Asset.Nome.
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public required string TipoPt { get; set; }
    public required string TipoEn { get; set; }
    public Severidade Severidade { get; set; }

    public required string OrigemPaisCodigo { get; set; } // ISO-2, ex. "CN"
    public required string OrigemPaisNomePt { get; set; }
    public required string OrigemPaisNomeEn { get; set; }
    public decimal OrigemLat { get; set; }
    public decimal OrigemLng { get; set; }
    public required string OrigemIp { get; set; }

    public required string Alvo { get; set; }
    public bool Bloqueado { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
