using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

/// <summary>
/// Painel dos alertas que vieram das ferramentas do cliente, já normalizados. É o outro lado da
/// plataforma de integração: Conectores é onde se configura, aqui é onde se lê.
///
/// Separado de /Vulnerabilidades de propósito: lá são achados do NOSSO scanner de superfície
/// externa; aqui é o que as ferramentas DELE (EDR, SIEM, firewall) detectaram. Misturar os dois
/// numa tela só apagaria a distinção entre "o que descobrimos" e "o que o parque dele reportou".
/// </summary>
[Authorize]
public class AlertasModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly I18nService _i18n;

    private const int PorPagina = 50;

    public AlertasModel(ApplicationDbContext db, I18nService i18n)
    {
        _db = db;
        _i18n = i18n;
    }

    public record AlertaView(int Id, string Titulo, string? Descricao, Severidade Severidade,
        string SeveridadeRotulo, string Cor, string Fundo, string? Categoria, string? AtivoNome,
        string Conector, string Quando, string? StatusOrigem);

    public record ContadorView(string Rotulo, string Valor, string Cor);

    public List<AlertaView> Alertas { get; private set; } = [];
    public List<ContadorView> Contadores { get; private set; } = [];
    public List<(int Id, string Nome)> EmpresasDisponiveis { get; private set; } = [];
    public List<(int Id, string Nome)> ConectoresDisponiveis { get; private set; } = [];

    public int? EmpresaSelecionadaId { get; private set; }
    public int? ConectorFiltro { get; private set; }
    public Severidade? SeveridadeFiltro { get; private set; }
    public string? Busca { get; private set; }
    public int TotalFiltrado { get; private set; }
    public bool TemMaisQuePagina { get; private set; }
    public bool SemConectores { get; private set; }

    public async Task OnGetAsync(int? empresa, int? conector, string? severidade, string? q)
    {
        EmpresasDisponiveis = (await _db.Companies.Where(c => c.Ativo).OrderBy(c => c.Nome)
                .Select(c => new { c.Id, c.Nome }).ToListAsync())
            .Select(c => (c.Id, c.Nome)).ToList();

        var empresaAtual = await ResolverEmpresaAsync(empresa);
        EmpresaSelecionadaId = empresaAtual?.Id;
        if (empresaAtual is null)
        {
            return;
        }

        ConectoresDisponiveis = (await _db.Conectores
                .Where(c => c.CompanyId == empresaAtual.Id)
                .OrderBy(c => c.Nome)
                .Select(c => new { c.Id, c.Nome }).ToListAsync())
            .Select(c => (c.Id, c.Nome)).ToList();
        SemConectores = ConectoresDisponiveis.Count == 0;

        ConectorFiltro = conector;
        Busca = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        SeveridadeFiltro = Enum.TryParse<Severidade>(severidade, ignoreCase: true, out var sev) ? sev : null;

        var consulta = _db.AlertasUnificados
            .Include(a => a.Conector)
            .Where(a => a.CompanyId == empresaAtual.Id);

        // Contadores sempre refletem a empresa inteira, não o filtro — senão o "crítico: 0" some
        // justamente quando alguém filtra por Baixa, e a leitura de risco fica errada.
        var porSeveridade = (await consulta
                .GroupBy(a => a.Severidade)
                .Select(g => new { g.Key, Total = g.Count() })
                .ToListAsync())
            .ToDictionary(x => x.Key, x => x.Total);

        var desde24h = DateTimeOffset.UtcNow.AddHours(-24);
        var ultimas24h = await consulta.CountAsync(a => a.OcorridoEm >= desde24h);

        Contadores =
        [
            new("Total", porSeveridade.Values.Sum().ToString("N0"), "#8FA3BC"),
            new("Críticos", porSeveridade.GetValueOrDefault(Severidade.Critica).ToString(), "#FF3B5C"),
            new("Altos", porSeveridade.GetValueOrDefault(Severidade.Alta).ToString(), "#FF8A3D"),
            new("Médios", porSeveridade.GetValueOrDefault(Severidade.Media).ToString(), "#FFC93C"),
            new("Baixos", porSeveridade.GetValueOrDefault(Severidade.Baixa).ToString(), "#4D9BFF"),
            new("Últimas 24h", ultimas24h.ToString("N0"), "#00E0A4"),
        ];

        if (ConectorFiltro is not null)
        {
            consulta = consulta.Where(a => a.ConectorId == ConectorFiltro);
        }

        if (SeveridadeFiltro is not null)
        {
            consulta = consulta.Where(a => a.Severidade == SeveridadeFiltro);
        }

        if (Busca is not null)
        {
            var termo = $"%{Busca}%";
            consulta = consulta.Where(a =>
                EF.Functions.ILike(a.Titulo, termo) ||
                (a.AtivoNome != null && EF.Functions.ILike(a.AtivoNome, termo)));
        }

        TotalFiltrado = await consulta.CountAsync();
        TemMaisQuePagina = TotalFiltrado > PorPagina;

        var lang = _i18n.Lang;
        Alertas = (await consulta
                .OrderByDescending(a => a.OcorridoEm)
                .Take(PorPagina)
                .ToListAsync())
            .Select(a => new AlertaView(
                a.Id, a.Titulo, a.Descricao, a.Severidade,
                SeverityStyle.Label(a.Severidade, lang),
                SeverityStyle.Cor(a.Severidade),
                SeverityStyle.Fundo(a.Severidade),
                a.Categoria, a.AtivoNome,
                a.Conector?.Nome ?? "—",
                a.OcorridoEm.ToLocalTime().ToString("dd/MM HH:mm"),
                a.StatusOrigem))
            .ToList();
    }

    private async Task<Company?> ResolverEmpresaAsync(int? empresaParam)
    {
        if (empresaParam.HasValue)
        {
            var escolhida = await _db.Companies.FirstOrDefaultAsync(c => c.Id == empresaParam.Value && c.Ativo);
            if (escolhida is not null)
            {
                return escolhida;
            }
        }

        return await TenantResolver.ResolverAtualAsync(HttpContext, _db);
    }
}
