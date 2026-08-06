using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class IncidentesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly I18nService _i18n;

    public IncidentesModel(ApplicationDbContext db, I18nService i18n)
    {
        _db = db;
        _i18n = i18n;
    }

    public record QueueItemView(int Id, string Codigo, string Idade, string Titulo, string Asset, string Mitre,
        string SevLabel, string SevCor, string SevBg, string AiResumo, string AiConfianca, bool Selecionado);
    public record StepView(string Numero, string Texto, string Categoria);
    public record TimelineView(string Hora, string Cor, string Texto, string Origem);
    public record DetalheView(string Codigo, string Titulo, string SevLabel, string SevCor, string SevBg,
        string Asset, string Mitre, string Analista, string Aberto, string Eventos,
        string Narrativa, string AiResumo, string AiConfianca, List<StepView> Passos, List<TimelineView> Timeline);

    public int QueueCount { get; private set; }
    public List<QueueItemView> Fila { get; private set; } = [];
    public DetalheView? Detalhe { get; private set; }

    public async Task OnGetAsync(int? id)
    {
        var lang = _i18n.Lang;
        var tenantAtual = await TenantResolver.ResolverAtualAsync(HttpContext, _db);
        // Ordenação em memória: Severidade é gravada como texto no banco (HasConversion<string>),
        // então um ORDER BY traduzido pro SQL compararia as strings em ordem alfabética
        // ("Media" > "Critica") em vez da ordem semântica do enum — teria que ordenar depois de
        // materializar mesmo.
        var incidentes = (await _db.Incidents
            .Include(i => i.Passos)
            .Include(i => i.LinhaDoTempo)
            .Where(i => i.CompanyId == tenantAtual!.Id)
            .ToListAsync())
            .OrderByDescending(i => i.Severidade)
            .ThenByDescending(i => i.AbertoEm)
            .ToList();

        QueueCount = incidentes.Count;
        var selecionado = (id is not null ? incidentes.FirstOrDefault(i => i.Id == id) : null) ?? incidentes.FirstOrDefault();

        Fila = incidentes.Select(i => new QueueItemView(
            i.Id, i.Codigo, FormatarIdade(i.AbertoEm, lang), lang == "pt" ? i.TituloPt : i.TituloEn, i.Asset, i.MitreCode,
            SeverityStyle.Label(i.Severidade, lang), SeverityStyle.Cor(i.Severidade), SeverityStyle.Fundo(i.Severidade),
            lang == "pt" ? i.AiResumoPt : i.AiResumoEn, i.AiConfianca, i.Id == selecionado?.Id)).ToList();

        if (selecionado is null)
        {
            return;
        }

        Detalhe = new DetalheView(
            selecionado.Codigo, lang == "pt" ? selecionado.TituloPt : selecionado.TituloEn,
            SeverityStyle.Label(selecionado.Severidade, lang), SeverityStyle.Cor(selecionado.Severidade), SeverityStyle.Fundo(selecionado.Severidade),
            selecionado.Asset, selecionado.MitreCode, selecionado.Analista ?? "—",
            FormatarIdade(selecionado.AbertoEm, lang), selecionado.EventosCount.ToString("N0"),
            lang == "pt" ? selecionado.NarrativaPt : selecionado.NarrativaEn,
            lang == "pt" ? selecionado.AiResumoPt : selecionado.AiResumoEn, selecionado.AiConfianca,
            selecionado.Passos.OrderBy(p => p.Ordem).Select(p => new StepView(p.Ordem.ToString("00"), lang == "pt" ? p.DescricaoPt : p.DescricaoEn, p.Categoria)).ToList(),
            selecionado.LinhaDoTempo.OrderBy(t => t.Hora).Select(t => new TimelineView(t.Hora, t.Cor, lang == "pt" ? t.DescricaoPt : t.DescricaoEn, t.Origem)).ToList());
    }

    private static string FormatarIdade(DateTime abertoEm, string lang)
    {
        var diff = DateTime.UtcNow - abertoEm;
        if (diff.TotalMinutes < 60)
        {
            return $"{(int)diff.TotalMinutes}m";
        }

        return diff.TotalHours < 24 ? $"{(int)diff.TotalHours}h" : $"{(int)diff.TotalDays}d";
    }
}
