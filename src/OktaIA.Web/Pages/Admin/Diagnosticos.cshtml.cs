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
        int Cobertura, decimal? Maturidade, int Completude, int Respostas,
        bool TemRelatorio);

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

    /// <summary>
    /// Devolve um arquivado à lista ativa.
    ///
    /// Volta para Concluído se ele já tinha sido fechado algum dia — o carimbo `ConcluidoEm` é
    /// que sabe disso, porque o arquivamento apaga o status mas não o carimbo. Sem isso, um
    /// levantamento fechado voltaria como "em andamento" e pediria para ser concluído de novo,
    /// o que descongelaria números que o cliente já viu.
    /// </summary>
    public async Task<IActionResult> OnPostDesarquivarAsync(int id, int? empresaId)
    {
        var empresa = await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaId);
        var diagnostico = empresa is null
            ? null
            : await _db.Diagnosticos.FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == empresa.Id);

        if (diagnostico is null)
        {
            Mensagem = "Diagnóstico não encontrado.";
            MensagemOk = false;
        }
        else
        {
            diagnostico.Status = diagnostico.ConcluidoEm is not null
                ? StatusDiagnostico.Concluido
                : StatusDiagnostico.EmAndamento;
            await _db.SaveChangesAsync();
            await _auditoria.RegistrarAsync("diagnostico.desarquivado",
                diagnostico.Titulo, User.Identity?.Name ?? "—");
            Mensagem = "Diagnóstico devolvido à lista.";
            MensagemOk = true;
        }

        return RedirectToPage(new { empresa = empresa?.Id });
    }

    /// <summary>
    /// Apaga de vez, com as respostas, ferramentas, riscos e análises juntas (cascata do EF).
    ///
    /// Existe ao lado de Arquivar porque as duas coisas são diferentes: arquivar guarda um
    /// levantamento que perdeu a validade mas ainda diz o que o cliente respondeu; excluir é para
    /// o que nunca deveria ter existido — um teste, um nome errado, uma linha duplicada.
    ///
    /// Irreversível de propósito, e por isso confirmado na tela antes de chegar aqui.
    /// </summary>
    public async Task<IActionResult> OnPostExcluirAsync(int id, int? empresaId)
    {
        var empresa = await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaId);
        var diagnostico = empresa is null
            ? null
            : await _db.Diagnosticos.FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == empresa.Id);

        if (diagnostico is null)
        {
            Mensagem = "Diagnóstico não encontrado.";
            MensagemOk = false;
        }
        else
        {
            var titulo = diagnostico.Titulo;
            _db.Diagnosticos.Remove(diagnostico);
            await _db.SaveChangesAsync();

            // Fica na auditoria: o registro sumiu, mas o ato de apagar não pode sumir junto.
            await _auditoria.RegistrarAsync("diagnostico.excluido",
                $"{empresa!.Nome} · {titulo}", User.Identity?.Name ?? "—");

            Mensagem = $"\"{titulo}\" foi excluído.";
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
                d.Respostas.Count,
                // Pelo carimbo, não pelo status: arquivar sobrescreve o status, e o relatório
                // de um levantamento arquivado continua existindo — e continua sendo o que o
                // consultor quer abrir meses depois.
                d.ConcluidoEm is not null);
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
