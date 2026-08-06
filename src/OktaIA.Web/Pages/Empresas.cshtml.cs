using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class EmpresasModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly I18nService _i18n;

    public EmpresasModel(ApplicationDbContext db, I18nService i18n)
    {
        _db = db;
        _i18n = i18n;
    }

    public record StatView(string Valor, string Rotulo, string Cor);
    public record CompanyView(int Id, string Nome, string Setor, string Plano, int Risco, string RiscoCor, List<StatView> Stats);

    public List<CompanyView> Empresas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var lang = _i18n.Lang;
        var empresas = await _db.Companies.Where(c => c.Ativo).OrderBy(c => c.Id).ToListAsync();
        const string accent = "#00E0A4";

        // Empresas seed (demo) têm Company.ScoreRisco/AtivosCount/VulnsCount/IncidentesCount/
        // UptimePercentual preenchidos manualmente no seed — mas uma empresa REAL (cadastrada via
        // "+ Adicionar ativo real" em /Ativos) nunca tem esses contadores atualizados, porque nada
        // no app escreve neles depois da criação. Resultado: card fica travado em zero pra sempre,
        // mesmo com ativo real escaneado e achados reais no banco (mesmo tipo de contador
        // desconectado já flagrado no Dashboard). Fix: para empresa com pelo menos 1 ativo real,
        // calcula os 5 números ao vivo a partir de Asset/Vulnerability/Incident — mesma fórmula de
        // risco usada em /Vulnerabilidades (CompanySecurityScoreCalculator), só invertida porque
        // aqui "Risk" é quanto maior pior, e Score ali é quanto maior melhor.
        var idsComAtivoReal = (await _db.Assets.Where(a => a.Real).Select(a => a.CompanyId).Distinct().ToListAsync()).ToHashSet();

        Empresas = [];
        foreach (var c in empresas)
        {
            if (idsComAtivoReal.Contains(c.Id))
            {
                var ativosDaEmpresa = await _db.Assets.Where(a => a.CompanyId == c.Id).ToListAsync();
                var achadosReais = await _db.Vulnerabilities.Where(v => v.CompanyId == c.Id && v.FonteScan).ToListAsync();
                var incidentesCount = await _db.Incidents.CountAsync(i => i.CompanyId == c.Id);
                var portasAbertas = achadosReais.Count(v => v.CategoriaScan == SecurityScanService.CategoriaPortas);
                var score = CompanySecurityScoreCalculator.Calcular(achadosReais, ativosDaEmpresa.Count, portasAbertas);
                var risco = 100 - score.Score;
                var uptimeMedio = ativosDaEmpresa.Count > 0 ? ativosDaEmpresa.Average(a => a.UptimePercentual) : 100m;

                Empresas.Add(new CompanyView(
                    c.Id, c.Nome, lang == "pt" ? c.SetorPt : c.SetorEn, c.Plano,
                    risco, risco > 70 ? "#FF3B5C" : risco > 45 ? "#FF8A3D" : accent,
                    [
                        new(ativosDaEmpresa.Count.ToString("N0"), lang == "pt" ? "ativos" : "assets", "#D4DDEA"),
                        new(achadosReais.Count.ToString(), "vulns", "#FF8A3D"),
                        new(incidentesCount.ToString(), lang == "pt" ? "incid." : "incid.", incidentesCount > 0 ? "#FF3B5C" : "#4A5A70"),
                        new(uptimeMedio.ToString("0.0") + "%", "SLA", accent),
                    ]));
            }
            else
            {
                Empresas.Add(new CompanyView(
                    c.Id, c.Nome, lang == "pt" ? c.SetorPt : c.SetorEn, c.Plano,
                    c.ScoreRisco, c.ScoreRisco > 70 ? "#FF3B5C" : c.ScoreRisco > 45 ? "#FF8A3D" : accent,
                    [
                        new(c.AtivosCount.ToString("N0"), lang == "pt" ? "ativos" : "assets", "#D4DDEA"),
                        new(c.VulnsCount.ToString(), "vulns", "#FF8A3D"),
                        new(c.IncidentesCount.ToString(), lang == "pt" ? "incid." : "incid.", c.IncidentesCount > 0 ? "#FF3B5C" : "#4A5A70"),
                        new(c.UptimePercentual.ToString("0.0") + "%", "SLA", accent),
                    ]));
            }
        }
    }
}
