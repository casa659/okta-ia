using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class FinanceiroModel : PageModel
{
    private readonly I18nService _i18n;

    public FinanceiroModel(I18nService i18n)
    {
        _i18n = i18n;
    }

    public record PlanView(string Nome, string Preco, string Inclusos, int Pct, string Empresas, string Cor);
    public record InvoiceView(string Numero, string Empresa, string Valor, string Status, string StatusCor);

    public List<PlanView> Plans { get; private set; } = [];
    public List<InvoiceView> Invoices { get; private set; } = [];

    public void OnGet()
    {
        var lang = _i18n.Lang;
        const string accent = "#00E0A4";
        var empresasLabel = lang == "pt" ? "empresas" : "companies";
        var empresaLabel = lang == "pt" ? "empresa" : "company";

        Plans =
        [
            new("Business", "R$ 1.890/mês", lang == "pt" ? "até 100 ativos · scanner · SIEM básico" : "up to 100 assets · scanner · basic SIEM", 25, $"2 {empresasLabel}", "#4D9BFF"),
            new("Enterprise", "R$ 8.400/mês", lang == "pt" ? "até 1.500 ativos · SOC 24×7 · IA completa" : "up to 1,500 assets · 24×7 SOC · full AI", 50, $"4 {empresasLabel}", accent),
            new("Enterprise+", "R$ 21.900/mês", lang == "pt" ? "ativos ilimitados · pentest trimestral · SLA 15min" : "unlimited assets · quarterly pentest · 15min SLA", 13, $"1 {empresaLabel}", "#7A6BFF"),
            new("Gov / MSP", lang == "pt" ? "sob consulta" : "custom", lang == "pt" ? "white label · on-premises · multi-país" : "white label · on-premises · multi-country", 12, $"1 {empresaLabel}", "#FF8A3D"),
        ];

        (string Num, string Co, string Amt, string St)[] dados =
        [
            ("NF-8841", "Banco Meridiano", "R$ 21.900", "paid"), ("NF-8840", "Grupo Vector", "R$ 8.400", "paid"),
            ("NF-8839", "NetSul Provedor", "R$ 14.200", "paid"), ("NF-8838", "Hospital Santa Clara", "R$ 8.400", "open"),
            ("NF-8837", "Prefeitura Digital", "R$ 18.700", "open"), ("NF-8836", "Pagou Fintech", "R$ 8.400", "paid"),
            ("NF-8835", "Loja Ativa", "R$ 1.890", "late"),
        ];

        Invoices = dados.Select(i =>
        {
            var (label, cor) = i.St switch
            {
                "paid" => (lang == "pt" ? "PAGA" : "PAID", accent),
                "open" => (lang == "pt" ? "ABERTA" : "OPEN", "#4D9BFF"),
                _ => (lang == "pt" ? "ATRASADA" : "LATE", "#FF3B5C"),
            };
            return new InvoiceView(i.Num, i.Co, i.Amt, label, cor);
        }).ToList();
    }
}
