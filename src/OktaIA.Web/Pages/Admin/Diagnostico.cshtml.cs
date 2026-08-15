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
/// O levantamento em si, um domínio por vez.
///
/// Salva a cada domínio em vez de tudo no fim: a reunião é interrompida o tempo todo, e perder uma
/// hora de respostas porque o navegador fechou é o tipo de coisa que faz a ferramenta ser abandonada
/// depois do primeiro uso.
/// </summary>
[Authorize]
public class DiagnosticoModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AdminAuditService _auditoria;

    public DiagnosticoModel(ApplicationDbContext db, AdminAuditService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    /// <summary>Passo especial, fora do catálogo: o inventário do que a empresa já tem.</summary>
    public const string PassoFerramentas = "ferramentas";

    public Models.Diagnostico? Diagnostico { get; private set; }
    public string? EmpresaNome { get; private set; }
    public ResultadoDoDiagnostico? Resultado { get; private set; }

    /// <summary>Domínios que se aplicam a esta empresa, já filtrados pelas condições respondidas.</summary>
    public List<DominioDeSeguranca> DominiosVisiveis { get; private set; } = [];

    public DominioDeSeguranca? DominioAtual { get; private set; }
    public bool NoPassoDeFerramentas { get; private set; }

    /// <summary>Respostas atuais, por código de pergunta — a view lê daqui para marcar o que está escolhido.</summary>
    public Dictionary<string, DiagnosticoResposta> Respostas { get; private set; } = [];

    /// <summary>Só as opções escolhidas, para o JS resolver as condições sem uma segunda consulta.</summary>
    public Dictionary<string, string?> Escolhas { get; private set; } = [];

    public string? ProximoPasso { get; private set; }
    public string? PassoAnterior { get; private set; }

    [TempData] public string? Mensagem { get; set; }
    [TempData] public bool MensagemOk { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, string? d)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }
        DefinirPasso(d);
        return Page();
    }

    /// <summary>
    /// Grava as respostas do domínio atual.
    ///
    /// Chega como dois dicionários paralelos vindos do formulário (código → valor). Respostas de
    /// pergunta escondida por condição são gravadas do mesmo jeito: se o consultor voltar atrás e
    /// mudar a resposta pai, o que já havia sido respondido continua lá — e o cálculo simplesmente
    /// ignora o que não está visível.
    /// </summary>
    public async Task<IActionResult> OnPostSalvarAsync(
        int id, string? d, string? proximo,
        [FromForm] Dictionary<string, string>? opcao,
        [FromForm] Dictionary<string, string>? texto,
        [FromForm] Dictionary<string, string>? numero,
        [FromForm] Dictionary<string, string>? origem,
        [FromForm] Dictionary<string, string[]>? multipla)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }
        var diagnostico = Diagnostico!;

        var codigos = new HashSet<string>();
        if (opcao is not null) { codigos.UnionWith(opcao.Keys); }
        if (texto is not null) { codigos.UnionWith(texto.Keys); }
        if (numero is not null) { codigos.UnionWith(numero.Keys); }
        if (multipla is not null) { codigos.UnionWith(multipla.Keys); }

        foreach (var codigo in codigos)
        {
            var pergunta = CatalogoDeDominios.BuscarPergunta(codigo);
            if (pergunta is null) { continue; }   // campo forjado no formulário: ignora em silêncio

            var valorOpcao = Valor(opcao, codigo);
            var valorTexto = Valor(texto, codigo);
            var valorNumero = Valor(numero, codigo);
            var valorOrigem = Valor(origem, codigo);

            // Múltipla escolha chega como várias marcações da mesma chave e é guardada num único
            // campo de texto. Não entra em cálculo — serve ao relatório e ao contexto da IA.
            if (multipla is not null && multipla.TryGetValue(codigo, out var marcadas) && marcadas.Length > 0)
            {
                valorTexto = string.Join("; ", marcadas.Where(m => !string.IsNullOrWhiteSpace(m)));
            }

            var vazio = valorOpcao is null && valorTexto is null && valorNumero is null;

            if (!Respostas.TryGetValue(codigo, out var resposta))
            {
                if (vazio) { continue; }   // não cria linha para pergunta que ficou em branco
                resposta = new DiagnosticoResposta { DiagnosticoId = diagnostico.Id, PerguntaCodigo = codigo };
                _db.DiagnosticoRespostas.Add(resposta);
                Respostas[codigo] = resposta;
            }

            resposta.Opcao = valorOpcao;
            resposta.Texto = valorTexto;
            resposta.Numero = int.TryParse(valorNumero, out var n) ? n : null;
            resposta.RespondidoEm = DateTimeOffset.UtcNow;

            // Origem é escolha explícita do consultor. O padrão é "declarado" porque é a verdade
            // do que acabou de acontecer: o cliente falou. Marcar como evidenciado ou validado é
            // ato consciente de quem viu a prova.
            resposta.Origem = Enum.TryParse<OrigemDaInformacao>(valorOrigem, out var o)
                ? o
                : resposta.Origem == OrigemDaInformacao.NaoAvaliado ? OrigemDaInformacao.Declarado : resposta.Origem;

            resposta.Situacao = SituacaoDe(pergunta, valorOpcao);
        }

        if (diagnostico.Status == StatusDiagnostico.Rascunho)
        {
            diagnostico.Status = StatusDiagnostico.EmAndamento;
        }

        await _db.SaveChangesAsync();
        Mensagem = "Respostas salvas.";
        MensagemOk = true;

        return RedirectToPage(new { id, d = proximo ?? d });
    }

    /// <summary>Acrescenta uma ferramenta ao inventário do cliente.</summary>
    public async Task<IActionResult> OnPostFerramentaAsync(
        int id, string dominioCodigo, string categoria, string fabricante, string? produto,
        bool licenciado, bool atualizado, bool monitorado, bool alertasTratados, bool integrada)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }

        if (string.IsNullOrWhiteSpace(categoria) || string.IsNullOrWhiteSpace(fabricante))
        {
            Mensagem = "Categoria e fabricante são obrigatórios.";
            MensagemOk = false;
            return RedirectToPage(new { id, d = PassoFerramentas });
        }

        _db.DiagnosticoFerramentas.Add(new DiagnosticoFerramenta
        {
            DiagnosticoId = id,
            DominioCodigo = dominioCodigo,
            Categoria = categoria.Trim(),
            Fabricante = fabricante.Trim(),
            Produto = string.IsNullOrWhiteSpace(produto) ? null : produto.Trim(),
            Licenciado = licenciado,
            Atualizado = atualizado,
            Monitorado = monitorado,
            AlertasTratados = alertasTratados,
            IntegradaAoLokta = integrada,
        });

        await _db.SaveChangesAsync();
        return RedirectToPage(new { id, d = PassoFerramentas });
    }

    public async Task<IActionResult> OnPostRemoverFerramentaAsync(int id, int ferramentaId)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }

        var ferramenta = await _db.DiagnosticoFerramentas
            .FirstOrDefaultAsync(f => f.Id == ferramentaId && f.DiagnosticoId == id);
        if (ferramenta is not null)
        {
            _db.DiagnosticoFerramentas.Remove(ferramenta);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { id, d = PassoFerramentas });
    }

    /// <summary>
    /// Fecha o diagnóstico: congela os números e gera os riscos a partir das lacunas.
    ///
    /// Riscos anteriores são substituídos — reabrir e concluir de novo tem que refletir as
    /// respostas de agora, não acumular achados de versões antigas do levantamento.
    /// </summary>
    public async Task<IActionResult> OnPostConcluirAsync(int id)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }
        var diagnostico = Diagnostico!;

        var resultado = CalculadoraDoDiagnostico.Calcular(diagnostico);

        diagnostico.Cobertura = resultado.Cobertura;
        diagnostico.Maturidade = resultado.Maturidade;
        diagnostico.UsoDoInvestimento = resultado.UsoDoInvestimento;
        diagnostico.Integracao = resultado.Integracao;
        diagnostico.Status = StatusDiagnostico.Concluido;
        diagnostico.ConcluidoEm = DateTimeOffset.UtcNow;

        var antigos = await _db.DiagnosticoRiscos.Where(r => r.DiagnosticoId == id).ToListAsync();
        _db.DiagnosticoRiscos.RemoveRange(antigos);
        _db.DiagnosticoRiscos.AddRange(CalculadoraDoDiagnostico.GerarRiscos(diagnostico));

        await _db.SaveChangesAsync();
        await _auditoria.RegistrarAsync("diagnostico.concluido",
            $"{EmpresaNome} · {diagnostico.Titulo} · cobertura {resultado.Cobertura}%",
            User.Identity?.Name ?? "—");

        return RedirectToPage("/Admin/DiagnosticoResultado", new { id });
    }

    /// <summary>Reabre um diagnóstico concluído para correção.</summary>
    public async Task<IActionResult> OnPostReabrirAsync(int id)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }
        Diagnostico!.Status = StatusDiagnostico.EmAndamento;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    private static string? Valor(Dictionary<string, string>? dic, string chave) =>
        dic is not null && dic.TryGetValue(chave, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static SituacaoDoControle SituacaoDe(PerguntaDoDiagnostico pergunta, string? opcao)
    {
        if (opcao is null) { return SituacaoDoControle.NaoAvaliado; }
        var positiva = opcao == CatalogoDeDominios.Sim;
        if (pergunta.RespostaBoaEhNao) { positiva = opcao == CatalogoDeDominios.Nao; }

        return opcao switch
        {
            CatalogoDeDominios.Parcial => SituacaoDoControle.Parcial,
            CatalogoDeDominios.NaoSei => SituacaoDoControle.NaoAvaliado,
            _ => positiva ? SituacaoDoControle.Tem : SituacaoDoControle.NaoTem,
        };
    }

    private async Task<bool> CarregarAsync(int id)
    {
        var empresasVisiveis = await TenantResolver.EmpresasVisiveis(HttpContext, _db)
            .Select(c => c.Id).ToListAsync();

        Diagnostico = await _db.Diagnosticos
            .Include(d => d.Company)
            .Include(d => d.Respostas)
            .Include(d => d.Ferramentas)
            .FirstOrDefaultAsync(d => d.Id == id && empresasVisiveis.Contains(d.CompanyId));

        if (Diagnostico is null) { return false; }

        EmpresaNome = Diagnostico.Company?.Nome;
        Respostas = Diagnostico.Respostas.ToDictionary(r => r.PerguntaCodigo);
        Escolhas = Diagnostico.Respostas.ToDictionary(r => r.PerguntaCodigo, r => r.Opcao);
        Resultado = CalculadoraDoDiagnostico.Calcular(Diagnostico);

        DominiosVisiveis = CatalogoDeDominios.Todos
            .Where(dom => CalculadoraDoDiagnostico.Visivel(dom.SomenteSe, Escolhas))
            .ToList();

        return true;
    }

    private void DefinirPasso(string? d)
    {
        NoPassoDeFerramentas = d == PassoFerramentas;
        if (NoPassoDeFerramentas)
        {
            PassoAnterior = DominiosVisiveis.LastOrDefault()?.Codigo;
            ProximoPasso = null;
            return;
        }

        DominioAtual = DominiosVisiveis.FirstOrDefault(x => x.Codigo == d) ?? DominiosVisiveis.FirstOrDefault();
        if (DominioAtual is null) { return; }

        var indice = DominiosVisiveis.IndexOf(DominioAtual);
        PassoAnterior = indice > 0 ? DominiosVisiveis[indice - 1].Codigo : null;
        ProximoPasso = indice < DominiosVisiveis.Count - 1
            ? DominiosVisiveis[indice + 1].Codigo
            : PassoFerramentas;
    }

    /// <summary>Quantas perguntas visíveis do domínio já foram respondidas — alimenta o progresso.</summary>
    public (int Respondidas, int Total) Progresso(DominioDeSeguranca dominio)
    {
        var visiveis = dominio.Perguntas
            .Where(p => CalculadoraDoDiagnostico.Visivel(p.SomenteSe, Escolhas)).ToList();
        var respondidas = visiveis.Count(p => Respostas.ContainsKey(p.Codigo));
        return (respondidas, visiveis.Count);
    }
}
