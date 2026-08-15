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
/// O resultado do levantamento: números, matriz de controles, riscos priorizados e a leitura do
/// modelo.
///
/// A tela mostra a origem das respostas com o mesmo destaque dos números. Um diagnóstico
/// inteiramente declarado precisa dizer que é declarado — é o que separa este relatório de uma
/// afirmação que o auditor do cliente derruba.
/// </summary>
[Authorize]
public class DiagnosticoResultadoModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IAnalisadorDeDiagnostico _analisador;
    private readonly AdminAuditService _auditoria;
    private readonly PropostaComercialPdfService _proposta;
    private readonly DiagnosticoPdfService _relatorio;

    public DiagnosticoResultadoModel(ApplicationDbContext db, IAnalisadorDeDiagnostico analisador,
        AdminAuditService auditoria, PropostaComercialPdfService proposta,
        DiagnosticoPdfService relatorio)
    {
        _db = db;
        _analisador = analisador;
        _auditoria = auditoria;
        _proposta = proposta;
        _relatorio = relatorio;
    }

    public Models.Diagnostico? Diagnostico { get; private set; }
    public string? EmpresaNome { get; private set; }
    public ResultadoDoDiagnostico? Resultado { get; private set; }
    public List<DiagnosticoRisco> Riscos { get; private set; } = [];
    public DiagnosticoAnalise? Analise { get; private set; }
    public bool IaConfigurada => _analisador.Configurado;

    /// <summary>O desenho do ambiente, camada a camada. É a tela que se mostra ao diretor.</summary>
    public List<CamadaDaArquitetura> Mapa { get; private set; } = [];
    public string Narrativa { get; private set; } = "";

    [TempData] public string? Mensagem { get; set; }
    [TempData] public bool MensagemOk { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }
        return Page();
    }

    /// <summary>
    /// Pede a leitura ao modelo e grava. Guardar em vez de gerar a cada abertura tem três motivos:
    /// o texto entregue ao cliente não pode mudar sozinho, a chamada custa, e numa auditoria é
    /// preciso mostrar o que foi dito e por qual modelo.
    /// </summary>
    public async Task<IActionResult> OnPostAnalisarAsync(int id)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }

        var analise = await _analisador.AnalisarAsync(
            Diagnostico!, Resultado!, User.Identity?.Name ?? "—", HttpContext.RequestAborted);

        _db.DiagnosticoAnalises.Add(analise);
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync("diagnostico.analisado",
            $"{Diagnostico!.Titulo} · {analise.Resultado}", User.Identity?.Name ?? "—");

        (Mensagem, MensagemOk) = analise.Resultado switch
        {
            ResultadoAnalise.Sucesso => ("Análise gerada.", true),
            // Recusa não é falha do sistema, e a mensagem precisa dizer isso — senão o operador
            // fica tentando de novo achando que foi instabilidade.
            ResultadoAnalise.Recusado => (
                $"O modelo recusou analisar este conteúdo{(analise.MotivoRecusa is { } m ? $" (categoria: {m})" : "")}. " +
                "Acontece com material de segurança e não indica erro no diagnóstico.", false),
            _ => ($"Não foi possível gerar a análise: {analise.Erro}", false),
        };

        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Gera a proposta comercial já com este diagnóstico dentro.
    ///
    /// É o mesmo documento de sempre — não um segundo PDF paralelo. Duas propostas com números
    /// diferentes circulando no mesmo cliente é como uma consultoria perde a conversa.
    /// </summary>
    public async Task<IActionResult> OnGetPropostaAsync(int id)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }

        var empresa = Diagnostico!.Company!;
        var achados = await _db.Vulnerabilities
            .Where(v => v.CompanyId == empresa.Id && v.FonteScan).ToListAsync();
        var ativos = await _db.Assets.Where(a => a.CompanyId == empresa.Id).ToListAsync();
        var ativosReais = ativos.Where(a => a.Real).ToList();
        var ultimaVarredura = ativosReais.Where(a => a.UltimoScanEm.HasValue)
            .Select(a => a.UltimoScanEm!.Value).OrderByDescending(x => x).FirstOrDefault();

        var pdf = _proposta.Gerar(empresa, achados, ativosReais.Count, ativos.Count,
            ultimaVarredura == default ? null : ultimaVarredura,
            Diagnostico, Resultado, Riscos);

        await _auditoria.RegistrarAsync("diagnostico.proposta",
            $"{empresa.Nome} · {Diagnostico.Titulo}", User.Identity?.Name ?? "—");

        var nome = $"proposta-comercial-lokta-ia-{empresa.Nome.Replace(" ", "-").ToLowerInvariant()}.pdf";
        return File(pdf, "application/pdf", nome);
    }

    /// <summary>
    /// O relatório do diagnóstico em PDF — o documento técnico que fica com o cliente.
    ///
    /// Existe separado da proposta porque os leitores são outros: a proposta vai para quem assina e
    /// mostra o que sustenta a venda; este vai para quem vai executar, e traz a matriz inteira,
    /// todos os riscos e a origem de cada resposta — inclusive o que não favorece ninguém.
    /// </summary>
    public async Task<IActionResult> OnGetRelatorioAsync(int id)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }

        var pdf = _relatorio.Gerar(Diagnostico!, EmpresaNome ?? "Cliente", Resultado!, Mapa,
            Riscos, Analise, Narrativa);

        await _auditoria.RegistrarAsync("diagnostico.relatorio",
            $"{EmpresaNome} · {Diagnostico!.Titulo}", User.Identity?.Name ?? "—");

        // A data do levantamento é opcional no modelo; sem o fallback o nome sairia terminado em
        // hífen, e um arquivo desses num e-mail para o cliente parece erro de sistema.
        var data = Diagnostico.RealizadoEm
                   ?? DateOnly.FromDateTime((Diagnostico.ConcluidoEm ?? Diagnostico.CriadoEm).LocalDateTime);
        var nome = $"diagnostico-seguranca-{Slug(EmpresaNome)}-{data:yyyy-MM-dd}.pdf";
        return File(pdf, "application/pdf", nome);
    }

    private static string Slug(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) { return "cliente"; }
        var limpo = new string(nome.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        return string.Join("-", limpo.Split('-', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }

    private async Task<bool> CarregarAsync(int id)
    {
        var visiveis = await TenantResolver.EmpresasVisiveis(HttpContext, _db).Select(c => c.Id).ToListAsync();

        Diagnostico = await _db.Diagnosticos
            .Include(d => d.Company)
            .Include(d => d.Respostas)
            .Include(d => d.Ferramentas)
            .FirstOrDefaultAsync(d => d.Id == id && visiveis.Contains(d.CompanyId));

        if (Diagnostico is null) { return false; }

        EmpresaNome = Diagnostico.Company?.Nome;
        Resultado = CalculadoraDoDiagnostico.Calcular(Diagnostico);
        Mapa = MapaDaArquitetura.Montar(Diagnostico);
        Narrativa = MapaDaArquitetura.Narrativa(Mapa, Resultado, Diagnostico.Ferramentas.Count);

        Riscos = await _db.DiagnosticoRiscos
            .Where(r => r.DiagnosticoId == id)
            .OrderBy(r => r.Prioridade)
            .ToListAsync();

        // Se ainda não houve conclusão, mostra os riscos calculados na hora para o consultor já
        // enxergar o mapa enquanto preenche — sem gravar nada.
        if (Riscos.Count == 0)
        {
            Riscos = CalculadoraDoDiagnostico.GerarRiscos(Diagnostico);
        }

        Analise = await _db.DiagnosticoAnalises
            .Where(a => a.DiagnosticoId == id)
            .OrderByDescending(a => a.GeradaEm)
            .FirstOrDefaultAsync();

        return true;
    }

    // Os textos moram em RotulosDoDiagnostico, porque o PDF usa os mesmos. Estes atalhos ficam
    // para a view não precisar mudar — e para ninguém ser tentado a redefinir o rótulo aqui.
    public static string CorDaGravidade(GravidadeRisco g) => RotulosDoDiagnostico.CorDaGravidade(g);

    public static string RotuloDaOrigem(OrigemDaInformacao o) => RotulosDoDiagnostico.Origem(o);

    public static string RotuloDaSituacao(SituacaoDoControle s) => RotulosDoDiagnostico.Situacao(s);

    public static string CorDaSituacao(SituacaoDoControle s) => RotulosDoDiagnostico.CorDaSituacao(s);
}
