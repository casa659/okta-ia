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
        bool TemRelatorio, bool EhExemplo);

    public List<LinhaDiagnostico> Itens { get; private set; } = [];

    /// <summary>
    /// Os frameworks do menu. Vêm de <see cref="CatalogoDeFrameworks"/>, que os DERIVA das
    /// etiquetas das perguntas — nunca de uma lista escrita aqui, que envelheceria calada.
    /// </summary>
    public IReadOnlyList<CatalogoDeFrameworks.Framework> Frameworks => CatalogoDeFrameworks.Todos;
    public List<(int Id, string Nome)> EmpresasDisponiveis { get; private set; } = [];
    public int? EmpresaSelecionadaId { get; private set; }
    public string? EmpresaNome { get; private set; }

    [TempData] public string? Mensagem { get; set; }
    [TempData] public bool MensagemOk { get; set; }

    public async Task OnGetAsync(int? empresa) => await CarregarAsync(empresa);

    // ── Planilha por framework ───────────────────────────────────────────────

    /// <summary>
    /// Baixa a planilha de levantamento de um framework, já com o nome da empresa dentro.
    ///
    /// ⚠️ Passa pelo `TenantResolver` como todo o resto: o nome do cliente vai impresso no arquivo,
    /// e sem o filtro bastaria trocar o número na URL para levar embora o nome de outra empresa.
    /// </summary>
    public async Task<IActionResult> OnGetPlanilhaAsync(string framework, int? empresaId,
        [FromServices] PlanilhaDoFramework planilhas)
    {
        var empresa = await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaId);
        var alvo = CatalogoDeFrameworks.Buscar(framework);
        if (empresa is null || alvo is null)
        {
            Mensagem = "Escolha a empresa e o framework antes de gerar a planilha.";
            MensagemOk = false;
            return RedirectToPage(new { empresa = empresaId });
        }

        var bytes = planilhas.Gerar(alvo, empresa.Nome);
        await _auditoria.RegistrarAsync("diagnostico.planilha.gerada",
            $"{empresa.Nome} · {alvo.Nome}", User.Identity?.Name ?? "—");

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"levantamento-{Arquivo(alvo.Prefixo)}-{Arquivo(empresa.Nome)}.xlsx");
    }

    /// <summary>
    /// Recebe a planilha preenchida e grava as respostas num diagnóstico NOVO.
    ///
    /// ⚠️ NOVO, e não mesclado num existente. Mesclar sobrescreveria em silêncio o que o consultor
    /// levantou na reunião — e ele não teria como saber que a planilha do cliente passou por cima
    /// da resposta dele. Um registro novo é visível, comparável e descartável.
    ///
    /// ⚠️ Tudo entra como DECLARADO. Uma planilha preenchida pelo cliente é a definição de
    /// declaração sem prova; marcá-la de outro jeito daria ao número a cara de medição, que é
    /// exatamente o que este módulo se recusa a fazer.
    /// </summary>
    public async Task<IActionResult> OnPostImportarAsync(string framework, int? empresaId,
        IFormFile? arquivo, string? respondente, string? cargo,
        [FromServices] PlanilhaDoFramework planilhas)
    {
        var empresa = await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaId);
        var alvo = CatalogoDeFrameworks.Buscar(framework);
        if (empresa is null || alvo is null)
        {
            Mensagem = "Escolha a empresa e o framework antes de enviar a planilha.";
            MensagemOk = false;
            return RedirectToPage(new { empresa = empresaId });
        }

        if (arquivo is null || arquivo.Length == 0)
        {
            Mensagem = "Nenhum arquivo foi enviado.";
            MensagemOk = false;
            return RedirectToPage(new { empresa = empresa.Id });
        }

        PlanilhaDoFramework.Leitura leitura;
        using (var conteudo = arquivo.OpenReadStream())
        {
            leitura = planilhas.Ler(conteudo);
        }

        if (leitura.Erro is { } erro)
        {
            Mensagem = erro;
            MensagemOk = false;
            return RedirectToPage(new { empresa = empresa.Id });
        }

        if (leitura.Aproveitadas.Count == 0)
        {
            // ⚠️ Não cria diagnóstico vazio. Um registro sem resposta nenhuma, na lista do cliente,
            // parece levantamento feito — e é o oposto disso.
            Mensagem = leitura.Recusadas.Count > 0
                ? $"Nenhuma resposta pôde ser lida. {leitura.Recusadas.Count} linha(s) tinham valor "
                  + "fora do esperado — confira se as respostas seguem a lista da coluna Resposta."
                : "A planilha veio sem nenhuma resposta preenchida.";
            MensagemOk = false;
            return RedirectToPage(new { empresa = empresa.Id });
        }

        var diagnostico = new Models.Diagnostico
        {
            CompanyId = empresa.Id,
            Titulo = $"Levantamento {alvo.Nome} · planilha",
            Status = StatusDiagnostico.EmAndamento,
            CriadoPor = User.Identity?.Name ?? "desconhecido",
            Respondente = string.IsNullOrWhiteSpace(respondente) ? null : respondente.Trim(),
            RespondenteCargo = string.IsNullOrWhiteSpace(cargo) ? null : cargo.Trim(),
            RealizadoEm = DateOnly.FromDateTime(DateTime.Today),
            Observacoes = $"Respostas importadas da planilha \"{arquivo.FileName}\" em "
                        + $"{DateTimeOffset.Now:dd/MM/yyyy HH:mm}. "
                        + $"{leitura.Aproveitadas.Count} aproveitada(s), {leitura.EmBranco} em branco"
                        + (leitura.Recusadas.Count > 0 ? $", {leitura.Recusadas.Count} não entendida(s)" : "")
                        + ". Origem: declarada pelo cliente, sem verificação.",
        };

        foreach (var linha in leitura.Aproveitadas)
        {
            var pergunta = CatalogoDeDominios.BuscarPergunta(linha.Codigo);
            if (pergunta is null) { continue; }

            diagnostico.Respostas.Add(new DiagnosticoResposta
            {
                PerguntaCodigo = linha.Codigo,
                Opcao = linha.Opcao,
                Texto = string.IsNullOrWhiteSpace(linha.Observacao) ? linha.Texto
                      : string.IsNullOrWhiteSpace(linha.Texto) ? linha.Observacao
                      : $"{linha.Texto} — {linha.Observacao}",
                Numero = linha.Numero,
                // A conversão mora na calculadora, e só lá. Ver o comentário dela.
                Situacao = CalculadoraDoDiagnostico.Situacao(pergunta, linha.Opcao),
                Origem = OrigemDaInformacao.Declarado,
            });
        }

        _db.Diagnosticos.Add(diagnostico);
        await _db.SaveChangesAsync();
        await _auditoria.RegistrarAsync("diagnostico.planilha.importada",
            $"{empresa.Nome} · {alvo.Nome} · {leitura.Aproveitadas.Count} resposta(s)",
            User.Identity?.Name ?? "—");

        // ⚠️ O que NÃO entrou é dito por extenso, com o valor que veio. Importação que anuncia só o
        // sucesso deixa o consultor achando que a planilha inteira subiu — e ele descobre a falta
        // na frente do cliente, lendo um relatório com buracos.
        Mensagem = $"{leitura.Aproveitadas.Count} resposta(s) importada(s)"
                 + (leitura.EmBranco > 0 ? $", {leitura.EmBranco} pergunta(s) em branco" : "")
                 + (leitura.Recusadas.Count > 0
                    ? $". {leitura.Recusadas.Count} linha(s) NÃO foram lidas: "
                      + string.Join(", ", leitura.Recusadas.Take(5).Select(r =>
                            r.Reconhecida ? $"{r.Codigo} (valor \"{r.RespostaCrua}\")"
                                          : $"{r.Codigo} (código fora do catálogo)"))
                      + (leitura.Recusadas.Count > 5 ? "…" : "")
                    : ".");
        MensagemOk = leitura.Recusadas.Count == 0;

        return RedirectToPage("/Admin/Diagnostico", new { id = diagnostico.Id });
    }

    /// <summary>Nome de arquivo sem acento nem espaço, para não quebrar no download.</summary>
    private static string Arquivo(string nome)
    {
        var limpo = new string(nome.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (limpo.Contains("--")) { limpo = limpo.Replace("--", "-"); }
        return limpo.Trim('-');
    }

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
        else if (diagnostico.CriadoPor == "seed")
        {
            // A tela já esconde o botão, mas esconder botão não é proteger: o POST continua
            // alcançável por quem montar o formulário na mão ou repetir uma requisição antiga.
            Mensagem = "O diagnóstico de exemplo não pode ser excluído — ele é a demonstração do módulo.";
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
                d.ConcluidoEm is not null,
                d.CriadoPor == "seed");
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
