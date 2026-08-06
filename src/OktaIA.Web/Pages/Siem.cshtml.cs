using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class SiemModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly I18nService _i18n;

    public SiemModel(ApplicationDbContext db, I18nService i18n)
    {
        _db = db;
        _i18n = i18n;
    }

    public record FacetItemView(string Chave, string Valor);
    public record FacetView(string Nome, List<FacetItemView> Itens);
    public record HistBarView(int AlturaPct, string Cor);
    public record LogRowView(string Hora, string Nivel, string NivelCor, string Origem, string Mensagem, string Host);

    public string Query { get; private set; } = "";
    public List<FacetView> Facets { get; private set; } = [];
    public List<HistBarView> Histograma { get; private set; } = [];
    public List<LogRowView> Logs { get; private set; } = [];
    public CopilotPrompt AskSiemPrompt { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        var lang = _i18n.Lang;
        Query = "event.category:auth AND event.outcome:failure AND source.geo != \"BR\" | stats count by source.ip";
        AskSiemPrompt = CopilotPrompts.For(lang)[3]; // "Qual servidor está mais lento agora?" — mesmo atalho do mockup

        var tenantAtual = await TenantResolver.ResolverAtualAsync(HttpContext, _db);

        // Facet de severidade é real (contagem das últimas 24h); Fonte/Nuvem ficam com os
        // mesmos valores de referência do mockup — SecurityEvent não modela sistema de origem/
        // provedor de nuvem ainda (ficaria pra uma fase de ingestão de log real).
        var desde24h = DateTime.UtcNow.AddHours(-24);
        var porSeveridade = await _db.SecurityEvents.Where(e => e.CriadoEm >= desde24h && e.CompanyId == tenantAtual!.Id)
            .GroupBy(e => e.Severidade).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        string Fmt(int n) => n >= 1000 ? $"{n / 1000.0:0.#}k" : n.ToString();

        Facets =
        [
            new(lang == "pt" ? "Fonte" : "Source", [
                new("Linux auditd", "842k"), new("Windows Sec", "611k"), new("FortiGate", "418k"),
                new("Nginx", "294k"), new("Kubernetes", "187k"),
            ]),
            new(lang == "pt" ? "Severidade" : "Severity", [
                new("critical", Fmt(porSeveridade.FirstOrDefault(s => s.Key == Models.Severidade.Critica)?.Count ?? 0)),
                new("high", Fmt(porSeveridade.FirstOrDefault(s => s.Key == Models.Severidade.Alta)?.Count ?? 0)),
                new("medium", Fmt(porSeveridade.FirstOrDefault(s => s.Key == Models.Severidade.Media)?.Count ?? 0)),
                new("low", Fmt(porSeveridade.FirstOrDefault(s => s.Key == Models.Severidade.Baixa)?.Count ?? 0)),
            ]),
            new(lang == "pt" ? "Nuvem" : "Cloud", [new("Azure", "221k"), new("AWS", "164k"), new("GCP", "38k")]),
        ];

        // Histograma real: volume de eventos por hora nas últimas 44h (mesma janela visual do
        // mockup, só que com contagem de verdade em vez de curva senoidal simulada).
        var desde44h = DateTime.UtcNow.AddHours(-44);
        var eventosJanela = await _db.SecurityEvents.Where(e => e.CriadoEm >= desde44h && e.CompanyId == tenantAtual!.Id).ToListAsync();
        var baldes = new int[44];
        var baldeTemCritico = new bool[44];
        foreach (var e in eventosJanela)
        {
            var h = (int)(DateTime.UtcNow - e.CriadoEm).TotalHours;
            if (h is >= 0 and < 44)
            {
                var idx = 43 - h;
                baldes[idx]++;
                if (e.Severidade == Models.Severidade.Critica)
                {
                    baldeTemCritico[idx] = true;
                }
            }
        }

        var maxBalde = Math.Max(1, baldes.Max());
        Histograma = baldes.Select((v, i) => new HistBarView(
            Math.Max(6, (int)Math.Round((decimal)v / maxBalde * 100)),
            baldeTemCritico[i] ? "#FF3B5C" : v > maxBalde * 0.6 ? "#FF8A3D" : "#1F3A4A")).ToList();

        var ultimosEventos = await _db.SecurityEvents.Where(e => e.CompanyId == tenantAtual!.Id)
            .OrderByDescending(e => e.CriadoEm).Take(16).ToListAsync();
        var origens = new[] { "sshd", "nginx", "fortigate", "auditd", "k8s-api" };
        var hosts = new[] { "sp-01", "sp-02", "fra-01", "iad-01" };
        Logs = ultimosEventos.Select((e, i) =>
        {
            var nivel = e.Severidade switch
            {
                Models.Severidade.Critica => "CRITICAL",
                Models.Severidade.Alta => "ERROR",
                Models.Severidade.Media => "WARN",
                _ => "INFO",
            };
            var tipo = lang == "pt" ? e.TipoPt : e.TipoEn;
            var origemTxt = lang == "pt" ? "origem" : "from";
            return new LogRowView(e.CriadoEm.ToString("HH:mm:ss"), nivel, SeverityStyle.Cor(e.Severidade),
                origens[i % origens.Length], $"{tipo} — {origemTxt} {e.OrigemIp} → {e.Alvo}", hosts[i % hosts.Length]);
        }).ToList();
    }
}
