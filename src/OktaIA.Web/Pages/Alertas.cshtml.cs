using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        string Conector, string Quando, string? StatusOrigem,
        StatusTriagem Status, string StatusRotulo, string StatusCor,
        string? Responsavel, string? NotaTriagem, string? TriadoEm, string? TriadoPor);

    public record ContadorView(string Rotulo, string Valor, string Cor);

    public List<AlertaView> Alertas { get; private set; } = [];
    public List<ContadorView> Contadores { get; private set; } = [];
    public List<(int Id, string Nome)> EmpresasDisponiveis { get; private set; } = [];
    public List<(int Id, string Nome)> ConectoresDisponiveis { get; private set; } = [];

    public int? EmpresaSelecionadaId { get; private set; }
    public int? ConectorFiltro { get; private set; }
    public Severidade? SeveridadeFiltro { get; private set; }
    public StatusTriagem? StatusFiltro { get; private set; }
    public string? Busca { get; private set; }
    public int TotalFiltrado { get; private set; }
    public bool TemMaisQuePagina { get; private set; }
    public bool SemConectores { get; private set; }

    public async Task OnGetAsync(int? empresa, int? conector, string? severidade, string? q, string? status)
    {
        EmpresasDisponiveis = (await TenantResolver.EmpresasVisiveis(HttpContext, _db).OrderBy(c => c.Nome)
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
        StatusFiltro = Enum.TryParse<StatusTriagem>(status, ignoreCase: true, out var st) ? st : null;

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

        // Contadores de TRIAGEM: são a resposta para "alguém está tratando isso?". Sem eles a tela
        // volta a dizer só quantos alertas existem, que é o que o gestor já sabia.
        var porStatus = (await consulta
                .GroupBy(a => a.Status)
                .Select(g => new { g.Key, Total = g.Count() })
                .ToListAsync())
            .ToDictionary(x => x.Key, x => x.Total);

        Contadores =
        [
            new("Total", porSeveridade.Values.Sum().ToString("N0"), "#8FA3BC"),
            new("Críticos", porSeveridade.GetValueOrDefault(Severidade.Critica).ToString(), "#FF3B5C"),
            new("Altos", porSeveridade.GetValueOrDefault(Severidade.Alta).ToString(), "#FF8A3D"),
            new("Novos", porStatus.GetValueOrDefault(StatusTriagem.Novo).ToString("N0"), "#4D9BFF"),
            new("Em andamento", porStatus.GetValueOrDefault(StatusTriagem.EmAndamento).ToString("N0"), "#FFC93C"),
            new("Resolvidos", porStatus.GetValueOrDefault(StatusTriagem.Resolvido).ToString("N0"), "#00E0A4"),
            new("Últimas 24h", ultimas24h.ToString("N0"), "#8FA3BC"),
        ];

        if (ConectorFiltro is not null)
        {
            consulta = consulta.Where(a => a.ConectorId == ConectorFiltro);
        }

        if (SeveridadeFiltro is not null)
        {
            consulta = consulta.Where(a => a.Severidade == SeveridadeFiltro);
        }

        if (StatusFiltro is not null)
        {
            consulta = consulta.Where(a => a.Status == StatusFiltro);
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
                a.StatusOrigem,
                a.Status, RotuloStatus(a.Status), CorStatus(a.Status),
                a.Responsavel, a.NotaTriagem,
                a.TriadoEm?.ToLocalTime().ToString("dd/MM HH:mm"),
                a.TriadoPor))
            .ToList();
    }

    public static string RotuloStatus(StatusTriagem s) => s switch
    {
        StatusTriagem.Novo => "Novo",
        StatusTriagem.EmAndamento => "Em andamento",
        StatusTriagem.Resolvido => "Resolvido",
        StatusTriagem.FalsoPositivo => "Falso positivo",
        _ => s.ToString(),
    };

    public static string CorStatus(StatusTriagem s) => s switch
    {
        StatusTriagem.Novo => "#4D9BFF",
        StatusTriagem.EmAndamento => "#FFC93C",
        StatusTriagem.Resolvido => "#00E0A4",
        // Cinza de propósito: falso positivo não é conquista nem risco — é ruído a ser reduzido.
        StatusTriagem.FalsoPositivo => "#8FA3BC",
        _ => "#8FA3BC",
    };

    /// <summary>
    /// Grava a triagem de UM alerta. O alerta é buscado com o filtro de empresa aplicado: sem isso,
    /// bastaria trocar o id no formulário para triar alerta de outro cliente — o mesmo furo que o
    /// isolamento multi-inquilino fechou nas listagens.
    /// </summary>
    public async Task<IActionResult> OnPostTriarAsync(int id, int? empresa, string? status,
        string? responsavel, string? nota, int? conector, string? severidade, string? q, string? statusFiltro)
    {
        var empresaAtual = await ResolverEmpresaAsync(empresa);
        if (empresaAtual is null)
        {
            return RedirectToPage();
        }

        var alerta = await _db.AlertasUnificados
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == empresaAtual.Id);

        if (alerta is not null && Enum.TryParse<StatusTriagem>(status, ignoreCase: true, out var novo))
        {
            alerta.Status = novo;
            alerta.Responsavel = string.IsNullOrWhiteSpace(responsavel) ? null : responsavel.Trim();
            alerta.NotaTriagem = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
            alerta.TriadoEm = DateTimeOffset.UtcNow;
            alerta.TriadoPor = User.Identity?.Name;
            await _db.SaveChangesAsync();
        }

        // Volta para a MESMA listagem (filtros preservados): triar em lote fica inviável se cada
        // gravação joga o analista de volta ao topo da lista sem filtro.
        return RedirectToPage(new { empresa = empresaAtual.Id, conector, severidade, q, status = statusFiltro });
    }

    // Delegado ao TenantResolver de propósito: conta de cliente é presa à própria empresa e o
    // parâmetro  é descartado. Resolver isso aqui, em cinco cópias, era como o furo
    // sobreviveria à correção do resolvedor.
    private async Task<Company?> ResolverEmpresaAsync(int? empresaParam)
        => await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaParam);
}
