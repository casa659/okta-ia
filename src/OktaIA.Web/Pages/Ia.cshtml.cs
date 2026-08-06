using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class IaModel : PageModel
{
    private readonly I18nService _i18n;

    public IaModel(I18nService i18n)
    {
        _i18n = i18n;
    }

    public record KpiView(string Label, string Valor, string Sub, string Cor);
    public record ModelView(string Nome, string Precisao, int Pct, string Cor);

    public List<KpiView> Kpis { get; private set; } = [];
    public List<AiDetection> Deteccoes { get; private set; } = [];
    public List<ModelView> Modelos { get; private set; } = [];

    public void OnGet()
    {
        var lang = _i18n.Lang;
        const string accent = "#00E0A4";

        Kpis =
        [
            new(lang == "pt" ? "Alertas classificados" : "Alerts classified", "41.208", lang == "pt" ? "últimas 24h" : "last 24h", "#4D9BFF"),
            new(lang == "pt" ? "Ruído eliminado" : "Noise removed", "93,4%", lang == "pt" ? "38.487 suprimidos" : "38,487 suppressed", accent),
            new(lang == "pt" ? "Falsos positivos" : "False positives", "4,1%", lang == "pt" ? "-6,2 p.p. no mês" : "-6.2 pp this month", "#FFC93C"),
            new(lang == "pt" ? "Investigações" : "Investigations", "312", lang == "pt" ? "assistidas por IA" : "AI-assisted", "#7A6BFF"),
        ];

        Deteccoes = AiDetections.For(lang).ToList();

        Modelos =
        [
            new(lang == "pt" ? "Classificador de alertas" : "Alert classifier", "96,2%", 96, accent),
            new("UEBA (baseline)", "91,8%", 92, accent),
            new(lang == "pt" ? "Detecção de anomalia de rede" : "Network anomaly detection", "88,4%", 88, "#4D9BFF"),
            new(lang == "pt" ? "Triagem de phishing" : "Phishing triage", "97,1%", 97, accent),
            new(lang == "pt" ? "Priorização de CVE (EPSS+)" : "CVE prioritization (EPSS+)", "84,6%", 85, "#FFC93C"),
        ];
    }
}
