using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Models;
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
        var empresas = await TenantResolver.EmpresasVisiveis(HttpContext, _db).OrderBy(c => c.Id).ToListAsync();
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

        // ── Alertas das ferramentas do cliente ──────────────────────────────────────────────
        //
        // ⚠️ ESTA TELA IGNORAVA A METADE NOVA. Ela contava vulnerabilidade do NOSSO scanner e
        // incidente, e não enxergava nada do que o Wazuh (ou qualquer conector) do cliente
        // reportou — justamente o que a operação de monitoramento existe para ver. A tela que
        // deveria responder "qual cliente está pegando fogo" respondia com metade do parque.
        //
        // ⚠️ SÓ OS ABERTOS E SÓ OS GRAVES. Alerta de nível baixo vem às centenas (auditoria de
        // configuração sozinha gera 400 numa máquina): contá-los aqui faria todo cliente parecer
        // em chamas e o número perderia o sentido em uma semana. O que a tela precisa dizer é
        // quantos exigem alguém agora.
        var graves = await _db.AlertasUnificados
            .Where(a => a.Severidade == Severidade.Critica || a.Severidade == Severidade.Alta)
            .GroupBy(a => a.CompanyId)
            .Select(g => new { CompanyId = g.Key, Quantos = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Quantos);

        // O total serve para outra pergunta: "este cliente está mesmo mandando dado?". Zero aqui,
        // com conector instalado, é sinal de que a integração parou — e é diferente de "está tudo
        // bem", que é o que um painel sem esta coluna diria.
        var totais = await _db.AlertasUnificados
            .GroupBy(a => a.CompanyId)
            .Select(g => new { CompanyId = g.Key, Quantos = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Quantos);

        string Alertas(int companyId)
        {
            var g = graves.GetValueOrDefault(companyId);
            var t = totais.GetValueOrDefault(companyId);
            return g > 0 ? g.ToString("N0") : t.ToString("N0");
        }

        string CorAlertas(int companyId) =>
            graves.GetValueOrDefault(companyId) > 0 ? "#FF3B5C"
            : totais.GetValueOrDefault(companyId) > 0 ? "#3D7BFF"
            : "#4A5A70";

        string RotuloAlertas(int companyId) =>
            graves.GetValueOrDefault(companyId) > 0 ? "graves" : "alertas";

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
                        new(Alertas(c.Id), RotuloAlertas(c.Id), CorAlertas(c.Id)),
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
                        // ⚠️ Também aqui: uma empresa pode ter conector e nenhum ativo do NOSSO
                        // scanner — é o caso normal de quem só nos deixa ler a ferramenta dele.
                        // Sem esta linha, justamente esse cliente apareceria sem número nenhum.
                        new(Alertas(c.Id), RotuloAlertas(c.Id), CorAlertas(c.Id)),
                        new(c.UptimePercentual.ToString("0.0") + "%", "SLA", accent),
                    ]));
            }
        }
    }
}
