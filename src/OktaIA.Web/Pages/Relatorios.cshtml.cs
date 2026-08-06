using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class RelatoriosModel : PageModel
{
    private readonly I18nService _i18n;

    public RelatoriosModel(I18nService i18n)
    {
        _i18n = i18n;
    }

    public record ReportView(string Nome, string Formato, string CorFormato, string FundoFormato, string Frequencia, string Descricao, string Ultimo);
    public record ExecStatView(string Chave, string Valor, string Cor);

    public List<ReportView> Reports { get; private set; } = [];
    public List<ExecStatView> ExecStats { get; private set; } = [];

    public void OnGet()
    {
        var lang = _i18n.Lang;
        const string accent = "#00E0A4";

        Reports =
        [
            new(lang == "pt" ? "Relatório executivo mensal" : "Monthly executive report", "PDF", "#FF3B5C", "#3A1620",
                lang == "pt" ? "MENSAL" : "MONTHLY",
                lang == "pt" ? "Postura de segurança, tendências e risco residual em linguagem de negócio." : "Security posture, trends and residual risk in business language.",
                "01/08/2026"),
            new(lang == "pt" ? "Inventário de vulnerabilidades" : "Vulnerability inventory", "XLSX", accent, "#0F2E26",
                lang == "pt" ? "SEMANAL" : "WEEKLY",
                lang == "pt" ? "Todas as CVEs abertas com CVSS, EPSS, ativo afetado e prazo de correção." : "All open CVEs with CVSS, EPSS, affected asset and remediation deadline.",
                "28/07/2026"),
            new(lang == "pt" ? "Conformidade LGPD / ISO 27001" : "GDPR / ISO 27001 compliance", "DOCX", "#4D9BFF", "#12283F",
                lang == "pt" ? "TRIMESTRAL" : "QUARTERLY",
                lang == "pt" ? "Mapeamento de controles, evidências de auditoria e desvios identificados." : "Control mapping, audit evidence and identified deviations.",
                "01/07/2026"),
            new(lang == "pt" ? "Apresentação para o conselho" : "Board presentation", "PPTX", "#FF8A3D", "#3A2410",
                lang == "pt" ? "TRIMESTRAL" : "QUARTERLY",
                lang == "pt" ? "Slides gerados por IA com narrativa de risco e comparativo setorial." : "AI-generated slides with risk narrative and sector benchmark.",
                "01/07/2026"),
            new(lang == "pt" ? "Relatório de incidentes" : "Incident report", "PDF", "#FF3B5C", "#3A1620",
                lang == "pt" ? "POR EVENTO" : "PER EVENT",
                lang == "pt" ? "Cronologia, impacto, contenção e lições aprendidas de cada incidente." : "Timeline, impact, containment and lessons learned for each incident.",
                "02/08/2026"),
            new(lang == "pt" ? "SLA e disponibilidade" : "SLA and availability", "PDF", "#FF3B5C", "#3A1620",
                lang == "pt" ? "MENSAL" : "MONTHLY",
                lang == "pt" ? "Uptime por ativo, janelas de indisponibilidade e créditos aplicáveis." : "Uptime per asset, downtime windows and applicable credits.",
                "01/08/2026"),
        ];

        ExecStats =
        [
            new(lang == "pt" ? "Eventos bloqueados" : "Events blocked", "3,84 M", accent),
            new(lang == "pt" ? "Incidentes contidos" : "Incidents contained", "7", "#4D9BFF"),
            new(lang == "pt" ? "Dívida de vulns" : "Vuln debt", "168", "#FF8A3D"),
            new(lang == "pt" ? "Risco residual" : "Residual risk", lang == "pt" ? "Médio" : "Medium", "#FFC93C"),
        ];
    }
}
