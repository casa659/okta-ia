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
    private readonly PropostaLgpdPdfService _propostaLgpd;
    private readonly PosturaLgpd _postura;
    private readonly DiagnosticoPdfService _relatorio;

    public DiagnosticoResultadoModel(ApplicationDbContext db, IAnalisadorDeDiagnostico analisador,
        AdminAuditService auditoria, PropostaComercialPdfService proposta,
        PropostaLgpdPdfService propostaLgpd, PosturaLgpd postura,
        DiagnosticoPdfService relatorio)
    {
        _db = db;
        _analisador = analisador;
        _auditoria = auditoria;
        _proposta = proposta;
        _propostaLgpd = propostaLgpd;
        _postura = postura;
        _relatorio = relatorio;
    }

    /// <summary>
    /// O framework deste diagnóstico, quando ele nasceu de uma planilha — o que decide se a tela
    /// oferece a proposta EXCLUSIVA do framework em vez da proposta da plataforma inteira.
    ///
    /// ⚠️ É `Diagnostico.OrigemFramework`, direto — nunca inferido das etiquetas das perguntas
    /// respondidas. Uma primeira versão tentou inferir e não fechava: um controle carrega várias
    /// etiquetas ao mesmo tempo ("LGPD art. 46", "CIS 3.6", "ISO A.8.24" na mesma pergunta), então
    /// até um diagnóstico só-LGPD tocava CIS e ISO juntos. Ver o comentário do campo no modelo.
    /// </summary>
    public string? EscopoUnico { get; private set; }

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
    /// ⚠️ DUAS PORTAS PARA DOCUMENTOS DIFERENTES (06/09/2026, pedido do dono: "a proposta deve
    /// ser exclusiva para LGPD, somente LGPD" e "100% em cima da resposta do Excel"). Quando o
    /// diagnóstico toca um framework só (<see cref="EscopoUnico"/> não nulo), a proposta da
    /// PLATAFORMA INTEIRA — módulos, ROI, scanner de vulnerabilidade — deixa de fazer sentido: ela
    /// citaria coisa nenhuma do que foi de fato perguntado. Neste caso sai a proposta
    /// <see cref="PropostaLgpdPdfService"/>, pequena e presa ao que existe.
    ///
    /// Fora desse caso — diagnóstico completo, vários domínios — continua o mesmo documento de
    /// sempre. Duas propostas com números diferentes circulando no mesmo cliente é como uma
    /// consultoria perde a conversa.
    /// </summary>
    public async Task<IActionResult> OnGetPropostaAsync(int id)
    {
        if (!await CarregarAsync(id)) { return RedirectToPage("/Admin/Diagnosticos"); }

        var empresa = Diagnostico!.Company!;

        // ⚠️ SÓ LGPD, POR AGORA. O pedido do dono foi específico a LGPD; um diagnóstico puro de
        // outro framework (CIS, ISO, NIST) ainda cai na proposta geral — construir a versão
        // escopada de cada um sem um pedido real seria gastar em cima de um formato que pode
        // não servir a nenhum dos três.
        if (string.Equals(EscopoUnico, "LGPD", StringComparison.OrdinalIgnoreCase))
        {
            return await GerarPropostaLgpdAsync(empresa);
        }

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
    /// A proposta exclusiva de LGPD. Ver o comentário de <see cref="OnGetPropostaAsync"/> para o
    /// porquê da bifurcação.
    ///
    /// ⚠️ MEDIDO VENCE DECLARADO. Antes de usar as respostas da planilha, pergunta a
    /// `PosturaLgpd` se a empresa já tem conector — se tiver, o documento usa o que foi MEDIDO
    /// (Wazuh), porque ignorar um fato para citar uma opinião seria o próprio módulo desmentindo
    /// a si mesmo. Pedido do dono, 06/09/2026: "se a empresa já estiver sendo monitorada pelo
    /// Wazuh, na proposta tem que ser com base no relatório do Wazuh".
    ///
    /// ⚠️ O PARCEIRO E O PREÇO VÊM DO ORÇAMENTO, quando existe um para a mesma empresa — é lá que
    /// mora `ParceiroNome` e os valores fechados. Sem orçamento, a proposta sai sem preço: não é
    /// erro, é a ordem natural quando o levantamento chega antes da negociação comercial.
    /// </summary>
    private async Task<IActionResult> GerarPropostaLgpdAsync(Company empresa)
    {
        var orcamento = await _db.Orcamentos.AsNoTracking()
            .Where(o => o.CompanyId == empresa.Id)
            .OrderByDescending(o => o.CriadaEm)
            .FirstOrDefaultAsync();

        var preco = orcamento is null ? null
            : new PropostaLgpdPdfService.Preco(orcamento.Numero, orcamento.ValorImplantacao, orcamento.ValorMensal,
                Itens: string.IsNullOrWhiteSpace(orcamento.ItensDeCustoJson) ? null
                    : System.Text.Json.JsonSerializer.Deserialize<List<CalculadoraDeOrcamento.ItemDeCusto>>(orcamento.ItensDeCustoJson),
                Maquinas: orcamento.MaquinasTotal,
                HospedagemDoCliente: orcamento.HospedagemDoCliente);

        var postura = await _postura.DeAsync(empresa.Id);

        var trataDadosDeCriancas = orcamento?.TrataDadosDeCriancas ?? false;

        var pdf = postura.Implantado
            ? _propostaLgpd.GerarMedido(empresa.Nome, empresa.Cnpj, orcamento?.ParceiroNome, postura, preco,
                trataDadosDeCriancas)
            : _propostaLgpd.GerarDeclarado(empresa.Nome, empresa.Cnpj, orcamento?.ParceiroNome,
                Diagnostico!, Riscos, preco, trataDadosDeCriancas,
                orcamento?.JaTemFerramenta ?? false, orcamento?.FerramentaExistente);

        await _auditoria.RegistrarAsync("diagnostico.proposta.lgpd",
            $"{empresa.Nome} · {(postura.Implantado ? "medido" : "declarado")}"
            + (orcamento?.ParceiroNome is { Length: > 0 } parc ? $" · via {parc}" : ""),
            User.Identity?.Name ?? "—");

        var nome = $"proposta-lgpd-{Slug(empresa.Nome)}.pdf";
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

        EscopoUnico = Diagnostico.OrigemFramework;

        return true;
    }

    // Os textos moram em RotulosDoDiagnostico, porque o PDF usa os mesmos. Estes atalhos ficam
    // para a view não precisar mudar — e para ninguém ser tentado a redefinir o rótulo aqui.
    public static string CorDaGravidade(GravidadeRisco g) => RotulosDoDiagnostico.CorDaGravidade(g);

    public static string RotuloDaOrigem(OrigemDaInformacao o) => RotulosDoDiagnostico.Origem(o);

    public static string RotuloDaSituacao(SituacaoDoControle s) => RotulosDoDiagnostico.Situacao(s);

    public static string CorDaSituacao(SituacaoDoControle s) => RotulosDoDiagnostico.CorDaSituacao(s);
}
