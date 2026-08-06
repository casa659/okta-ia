using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly I18nService _i18n;

    public DashboardModel(ApplicationDbContext db, I18nService i18n)
    {
        _db = db;
        _i18n = i18n;
    }

    public record KpiView(string Label, string Valor, string Delta, string Cor, string DeltaCor, string Spark);
    public record GaugeView(string Label, int Valor, string Texto, string Cor);
    public record OrigemView(string Codigo, string Nome, int Pct, string Contagem);
    public record FeedItemView(string SevLabel, string SevCor, string SevBg, string Tipo, string Hora, string Ip, string Cc, string Alvo, string Acao, string AcaoCor);

    public List<KpiView> Kpis { get; private set; } = [];
    public List<GaugeView> Gauges { get; private set; } = [];
    public List<OrigemView> TopOrigens { get; private set; } = [];
    public List<FeedItemView> Feed { get; private set; } = [];
    public string MapaJson { get; private set; } = "{}";
    public string AiBriefBody { get; private set; } = "";
    public List<CopilotPrompt> Prompts { get; private set; } = [];
    public string GreetMsg { get; private set; } = "";

    public async Task OnGetAsync()
    {
        var lang = _i18n.Lang;
        var culture = lang == "en" ? new CultureInfo("en-US") : new CultureInfo("pt-BR");
        var agora = DateTime.UtcNow;
        var desde24h = agora.AddHours(-24);
        var desde48h = agora.AddHours(-48);

        var tenantAtual = await TenantResolver.ResolverAtualAsync(HttpContext, _db);
        var tenantId = tenantAtual?.Id;

        var eventos24h = await _db.SecurityEvents.Where(e => e.CriadoEm >= desde24h && e.CompanyId == tenantId).ToListAsync();
        var eventos24a48h = await _db.SecurityEvents.CountAsync(e => e.CriadoEm >= desde48h && e.CriadoEm < desde24h && e.CompanyId == tenantId);
        var bloqueados24h = eventos24h.Count(e => e.Bloqueado);
        var taxaBloqueio = eventos24h.Count > 0 ? (decimal)bloqueados24h / eventos24h.Count * 100 : 0;
        var variacaoEventos = eventos24a48h > 0 ? (decimal)(eventos24h.Count - eventos24a48h) / eventos24a48h * 100 : 0;

        // KPIs abaixo vêm dos contadores da própria empresa (não somados de todo o portfólio) —
        // é o que muda de verdade ao trocar de organização no seletor do header.
        var incidentesAbertos = tenantAtual?.IncidentesCount ?? 0;
        var vulnsCriticas = tenantAtual?.VulnsCount ?? 0;
        var ativosMonitorados = tenantAtual?.AtivosCount ?? 0;
        var disponibilidade = tenantAtual?.UptimePercentual ?? 0;

        var accent = "#00E0A4";
        Kpis =
        [
            new(lang == "pt" ? "Eventos 24h" : "Events 24h", eventos24h.Count.ToString("N0", culture),
                $"{(variacaoEventos >= 0 ? "+" : "")}{variacaoEventos.ToString("0.0", culture)}%", "#4D9BFF", "#4A5A70",
                SparkHorario(eventos24h, agora)),
            new(lang == "pt" ? "Bloqueados" : "Blocked", bloqueados24h.ToString("N0", culture),
                $"{taxaBloqueio.ToString("0.0", culture)}%", accent, accent,
                SparkHorario(eventos24h.Where(e => e.Bloqueado).ToList(), agora)),
            new(lang == "pt" ? "Incidentes abertos" : "Open incidents", incidentesAbertos.ToString("N0", culture),
                "", "#FF3B5C", "#4A5A70", SparkPath.Generate(5, true)),
            new(lang == "pt" ? "Vulns críticas" : "Critical vulns", vulnsCriticas.ToString("N0", culture),
                "", "#FF8A3D", "#4A5A70", SparkPath.Generate(7, true)),
            new(lang == "pt" ? "Ativos monitorados" : "Monitored assets", ativosMonitorados.ToString("N0", culture),
                "", "#7A6BFF", "#4A5A70", SparkPath.Generate(9)),
            new(lang == "pt" ? "Disponibilidade" : "Availability", disponibilidade.ToString("0.00", culture) + "%",
                "SLA", accent, "#4A5A70", SparkPath.Generate(11)),
        ];

        var ultimaInfra = await _db.InfraHealthSnapshots.OrderByDescending(s => s.CriadoEm).FirstOrDefaultAsync()
            ?? new InfraHealthSnapshot();
        string GaugeCor(int v) => v > 80 ? "#FF3B5C" : v > 65 ? "#FF8A3D" : accent;
        Gauges =
        [
            new("CPU", ultimaInfra.CpuPct, $"{ultimaInfra.CpuPct}%", GaugeCor(ultimaInfra.CpuPct)),
            new(lang == "pt" ? "Memória" : "Memory", ultimaInfra.RamPct, $"{ultimaInfra.RamPct}%", GaugeCor(ultimaInfra.RamPct)),
            new(lang == "pt" ? "Disco" : "Disk", ultimaInfra.DiscoPct, $"{ultimaInfra.DiscoPct}%", GaugeCor(ultimaInfra.DiscoPct)),
            new(lang == "pt" ? "Rede" : "Network", ultimaInfra.RedePct, $"{ultimaInfra.RedePct}%", GaugeCor(ultimaInfra.RedePct)),
            new(lang == "pt" ? "Latência API" : "API latency", Math.Min(100, ultimaInfra.LatenciaMs), $"{ultimaInfra.LatenciaMs}ms", GaugeCor(ultimaInfra.LatenciaMs)),
        ];

        var porOrigem = eventos24h.GroupBy(e => new { e.OrigemPaisCodigo, Nome = lang == "pt" ? e.OrigemPaisNomePt : e.OrigemPaisNomeEn })
            .Select(g => new { g.Key.OrigemPaisCodigo, g.Key.Nome, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(6)
            .ToList();
        var maxOrigem = porOrigem.Count > 0 ? porOrigem[0].Count : 1;
        TopOrigens = porOrigem.Select(o => new OrigemView(o.OrigemPaisCodigo, o.Nome,
            (int)Math.Round((decimal)o.Count / maxOrigem * 100), o.Count.ToString("N0", culture))).ToList();

        var ultimosEventos = await _db.SecurityEvents.Where(e => e.CompanyId == tenantId)
            .OrderByDescending(e => e.CriadoEm).Take(14).ToListAsync();
        Feed = ultimosEventos.Select(e => new FeedItemView(
            SeverityStyle.Label(e.Severidade, lang), SeverityStyle.Cor(e.Severidade), SeverityStyle.Fundo(e.Severidade),
            lang == "pt" ? e.TipoPt : e.TipoEn, e.CriadoEm.ToString("HH:mm:ss"),
            e.OrigemIp, e.OrigemPaisCodigo, e.Alvo,
            e.Bloqueado ? (lang == "pt" ? "BLOQUEADO" : "BLOCKED") : (lang == "pt" ? "INVESTIGANDO" : "INVESTIGATING"),
            e.Bloqueado ? accent : "#FF8A3D")).ToList();

        var origensMapa = porOrigem.Select(o =>
        {
            var origem = ThreatCatalog.Origens.First(x => x.Cc == o.OrigemPaisCodigo);
            return new { cc = o.OrigemPaisCodigo, nome = o.Nome, lat = origem.Lat, lng = origem.Lng, count = o.Count };
        });
        var datacenters = ThreatCatalog.Datacenters.Select(d => new { codigo = d.Codigo, lat = d.Lat, lng = d.Lng });
        MapaJson = System.Text.Json.JsonSerializer.Serialize(new { origens = origensMapa, datacenters });

        AiBriefBody = _i18n.T("aiBriefBody");
        Prompts = CopilotPrompts.For(lang).ToList();
        GreetMsg = _i18n.T("greet");
    }

    // Sparkline real: eventos por hora nas últimas ~20h, normalizado no mesmo formato de path
    // usado pelo restante do dashboard (viewBox 0 0 100 22).
    private static string SparkHorario(List<SecurityEvent> eventos, DateTime agora)
    {
        var baldes = new int[21];
        foreach (var e in eventos)
        {
            var horasAtras = (int)(agora - e.CriadoEm).TotalHours;
            if (horasAtras is >= 0 and <= 20)
            {
                baldes[20 - horasAtras]++;
            }
        }

        var max = Math.Max(1, baldes.Max());
        var p = "";
        for (var i = 0; i <= 20; i++)
        {
            var v = Math.Max(2, Math.Min(20, (decimal)baldes[i] / max * 20));
            p += (i == 0 ? "M" : "L") + (i * 5) + " " + (22 - v).ToString("0.0", CultureInfo.InvariantCulture);
        }

        return p;
    }
}
