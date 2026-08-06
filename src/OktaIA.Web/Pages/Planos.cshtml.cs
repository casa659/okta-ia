using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

public class PlanosModel : PageModel
{
    public record PriceVariant(string Model, string Price, string Unit, string Note);
    public record PlanView(MarketingContent.Plan Plan, IReadOnlyList<PriceVariant> Prices);

    public IReadOnlyList<PlanView> Plans { get; private set; } = Array.Empty<PlanView>();

    public void OnGet()
    {
        Plans = MarketingContent.Plans.Select(BuildView).ToList();
    }

    private static PlanView BuildView(MarketingContent.Plan pl)
    {
        var porte = new PriceVariant("porte", pl.Porte, pl.Mult == 0 ? "" : "/mês", $"faixa de {pl.Range} conforme escopo");
        var ativoPrice = pl.Rates[0] > 0 ? $"R$ {pl.Rates[0]}" : "Sob consulta";
        var ativoNote = pl.Rates[1] > 0 ? $"R$ {pl.Rates[1]} por servidor · R$ {pl.Rates[2]} por firewall" : "precificação por escopo e criticidade";
        var ativo = new PriceVariant("ativo", ativoPrice, pl.Mult == 0 ? "" : "/estação · mês", ativoNote);
        var userNote = pl.Mult == 0 ? "a partir de 500 usuários" : "mínimo de 25 usuários";
        var user = new PriceVariant("user", pl.User, pl.Mult == 0 ? "" : "/usuário · mês", userNote);
        return new PlanView(pl, new[] { porte, ativo, user });
    }
}
