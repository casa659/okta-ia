namespace OktaIA.Web.Models;

// Incidente com triagem/análise assistida por IA — modelo mais rico do design original
// (narrativa, ações recomendadas, linha do tempo). AbertoEm é real (não string fixa "12m" como
// no mockup); "idade" é sempre calculada a partir dele.
public class Incident
{
    public int Id { get; set; }

    // Dono do incidente (tenant) — nullable pra incidentes cujo Asset não bate com nenhum Asset
    // cadastrado. Resolvido no seed/backfill casando Asset (texto) com Asset.Nome.
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public required string Codigo { get; set; } // "INC-4821"
    public Severidade Severidade { get; set; }
    public required string Asset { get; set; }
    public required string MitreCode { get; set; }
    public string? Analista { get; set; }
    public int EventosCount { get; set; }

    public required string TituloPt { get; set; }
    public required string TituloEn { get; set; }
    public required string AiResumoPt { get; set; }
    public required string AiResumoEn { get; set; }
    public required string AiConfianca { get; set; } // "94%"
    public required string NarrativaPt { get; set; }
    public required string NarrativaEn { get; set; }

    public DateTime AbertoEm { get; set; } = DateTime.UtcNow;

    public List<IncidentStep> Passos { get; set; } = [];
    public List<IncidentTimelineEvent> LinhaDoTempo { get; set; } = [];
}

public class IncidentStep
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public int Ordem { get; set; }
    public required string DescricaoPt { get; set; }
    public required string DescricaoEn { get; set; }
    public required string Categoria { get; set; } // "FIREWALL", "IAM", "EDR"...
}

public class IncidentTimelineEvent
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public required string Hora { get; set; } // "14:02" — snapshot de exibição, não recalculado
    public required string Cor { get; set; }
    public required string DescricaoPt { get; set; }
    public required string DescricaoEn { get; set; }
    public required string Origem { get; set; } // "siem.auth"
}
