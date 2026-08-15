using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;
using OktaIA.Web.Services.Diagnostico;

namespace OktaIA.Web.Pages.Admin;

/// <summary>
/// Lista de diagnósticos de uma empresa e a criação de um novo.
///
/// Filtra por empresa pelo `TenantResolver` como as demais telas com `?empresa=` — resolver aqui
/// por conta própria reabriria o caminho que o isolamento multi-inquilino fechou.
/// </summary>
[Authorize]
public class DiagnosticosModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AdminAuditService _auditoria;

    public DiagnosticosModel(ApplicationDbContext db, AdminAuditService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public record LinhaDiagnostico(
        int Id, string Titulo, StatusDiagnostico Status, string StatusRotulo, string Cor,
        DateTimeOffset CriadoEm, string CriadoPor, string? Respondente,
        int Cobertura, decimal? Maturidade, int Completude, int Respostas);

    public List<LinhaDiagnostico> Itens { get; private set; } = [];
    public List<(int Id, string Nome)> EmpresasDisponiveis { get; private set; } = [];
    public int? EmpresaSelecionadaId { get; private set; }
    public string? EmpresaNome { get; private set; }

    [TempData] public string? Mensagem { get; set; }
    [TempData] public bool MensagemOk { get; set; }

    public async Task OnGetAsync(int? empresa) => await CarregarAsync(empresa);

    public async Task<IActionResult> OnPostCriarAsync(int? empresaId, string titulo, string? respondente, string? cargo)
    {
        var empresa = await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaId);
        if (empresa is null)
        {
            Mensagem = "Empresa não encontrada.";
            MensagemOk = false;
            return RedirectToPage(new { empresa = empresaId });
        }

        if (string.IsNullOrWhiteSpace(titulo))
        {
            Mensagem = "Dê um nome ao diagnóstico — é como você vai encontrá-lo depois.";
            MensagemOk = false;
            return RedirectToPage(new { empresa = empresa.Id });
        }

        var diagnostico = new Models.Diagnostico
        {
            CompanyId = empresa.Id,
            Titulo = titulo.Trim(),
            CriadoPor = User.Identity?.Name ?? "desconhecido",
            Respondente = string.IsNullOrWhiteSpace(respondente) ? null : respondente.Trim(),
            RespondenteCargo = string.IsNullOrWhiteSpace(cargo) ? null : cargo.Trim(),
            RealizadoEm = DateOnly.FromDateTime(DateTime.Today),
        };

        _db.Diagnosticos.Add(diagnostico);
        await _db.SaveChangesAsync();
        await _auditoria.RegistrarAsync("diagnostico.criado",
            $"{empresa.Nome} · {diagnostico.Titulo}", User.Identity?.Name ?? "—");

        return RedirectToPage("/Admin/Diagnostico", new { id = diagnostico.Id });
    }

    /// <summary>
    /// Arquiva em vez de apagar. Um levantamento perdido ainda diz o que o cliente respondeu
    /// naquele dia, e é o que sustenta uma segunda conversa meses depois.
    /// </summary>
    public async Task<IActionResult> OnPostArquivarAsync(int id, int? empresaId)
    {
        var empresa = await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaId);
        var diagnostico = await _db.Diagnosticos
            .FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == (empresa != null ? empresa.Id : 0));

        if (diagnostico is null)
        {
            Mensagem = "Diagnóstico não encontrado.";
            MensagemOk = false;
        }
        else
        {
            diagnostico.Status = StatusDiagnostico.Arquivado;
            await _db.SaveChangesAsync();
            await _auditoria.RegistrarAsync("diagnostico.arquivado",
                diagnostico.Titulo, User.Identity?.Name ?? "—");
            Mensagem = "Diagnóstico arquivado.";
            MensagemOk = true;
        }

        return RedirectToPage(new { empresa = empresa?.Id });
    }

    private async Task CarregarAsync(int? empresaParam)
    {
        EmpresasDisponiveis = await TenantResolver.EmpresasVisiveis(HttpContext, _db)
            .OrderBy(c => c.Nome).Select(c => new ValueTuple<int, string>(c.Id, c.Nome)).ToListAsync();

        var empresa = await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaParam);
        EmpresaSelecionadaId = empresa?.Id;
        EmpresaNome = empresa?.Nome;
        if (empresa is null) { return; }

        var diagnosticos = await _db.Diagnosticos
            .Include(d => d.Respostas)
            .Include(d => d.Ferramentas)
            .Where(d => d.CompanyId == empresa.Id)
            .OrderByDescending(d => d.CriadoEm)
            .Take(50)
            .ToListAsync();

        Itens = diagnosticos.Select(d =>
        {
            // Concluído mostra o número congelado; em andamento recalcula, porque é justamente o
            // que o consultor precisa ver evoluir enquanto preenche.
            var resultado = d.Status == StatusDiagnostico.Concluido && d.Cobertura is not null
                ? null
                : CalculadoraDoDiagnostico.Calcular(d);

            var (rotulo, cor) = Aparencia(d.Status);
            return new LinhaDiagnostico(
                d.Id, d.Titulo, d.Status, rotulo, cor, d.CriadoEm, d.CriadoPor, d.Respondente,
                resultado?.Cobertura ?? d.Cobertura ?? 0,
                resultado?.Maturidade ?? d.Maturidade,
                resultado?.Completude ?? 100,
                d.Respostas.Count);
        }).ToList();
    }

    private static (string Rotulo, string Cor) Aparencia(StatusDiagnostico status) => status switch
    {
        StatusDiagnostico.Rascunho => ("rascunho", "#7A8FAB"),
        StatusDiagnostico.EmAndamento => ("em andamento", "#4D9BFF"),
        StatusDiagnostico.Concluido => ("concluído", "#00E0A4"),
        _ => ("arquivado", "#5A7191"),
    };
}
