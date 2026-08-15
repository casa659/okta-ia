using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Diagnostico;

/// <summary>Como um controle aparece na matriz executiva TEM / PARCIAL / NÃO TEM.</summary>
/// <param name="Pergunta">Pergunta que representa o controle.</param>
/// <param name="Situacao">O que a resposta diz.</param>
/// <param name="Origem">Declarado, evidenciado ou validado. Vai junto sempre.</param>
public record LinhaDaMatriz(PerguntaDoDiagnostico Pergunta, SituacaoDoControle Situacao, OrigemDaInformacao Origem);

/// <summary>Resultado de um domínio. Maturidade é nula quando não há o que medir, nunca zero.</summary>
public record ResultadoDoDominio
{
    public required DominioDeSeguranca Dominio { get; init; }

    /// <summary>0 a 100. Quanto dos controles esperados existe.</summary>
    public int Cobertura { get; init; }

    /// <summary>
    /// 0,0 a 5,0. Quão bem gerenciado é o que existe. **Nulo** quando o domínio não tem pergunta
    /// de qualidade respondida — dizer "maturidade 0" de um domínio que não foi avaliado é
    /// exatamente o tipo de número inventado que este produto não usa.
    /// </summary>
    public decimal? Maturidade { get; init; }

    public int PerguntasVisiveis { get; init; }
    public int PerguntasRespondidas { get; init; }

    public List<LinhaDaMatriz> Matriz { get; init; } = [];
}

/// <summary>Os números do diagnóstico inteiro, com o rastro de como foram obtidos.</summary>
public record ResultadoDoDiagnostico
{
    public int Cobertura { get; init; }
    public decimal? Maturidade { get; init; }

    /// <summary>
    /// Quanto das capacidades já pagas está em uso. Nulo quando nenhuma ferramenta foi cadastrada —
    /// sem inventário não há como afirmar subutilização.
    /// </summary>
    public int? UsoDoInvestimento { get; init; }

    /// <summary>Percentual das ferramentas existentes já integradas ao L'Okta IA.</summary>
    public int? Integracao { get; init; }

    /// <summary>Percentual das perguntas aplicáveis que foram efetivamente respondidas.</summary>
    public int Completude { get; init; }

    public List<ResultadoDoDominio> Dominios { get; init; } = [];

    /// <summary>
    /// Quantas respostas vieram de cada origem. É o número que impede o relatório de passar por
    /// medição: um diagnóstico 100% declarado precisa dizer que é 100% declarado.
    /// </summary>
    public Dictionary<OrigemDaInformacao, int> PorOrigem { get; init; } = [];

    public int ControlesAusentes { get; init; }
    public int ControlesParciais { get; init; }
}

/// <summary>
/// Transforma respostas em números.
///
/// **Cobertura e maturidade medem coisas diferentes e saem de perguntas diferentes** — a distinção
/// não é cosmética, é o que o gestor precisa para decidir entre comprar e organizar:
///
/// - **Cobertura** vem das perguntas raiz: o controle existe? ("possui firewall?")
/// - **Maturidade** vem das perguntas condicionais que só aparecem quando o controle existe:
///   está licenciado, atualizado, revisado, testado, com alguém tratando o alerta?
///
/// Uma empresa pode ter 90% de cobertura e maturidade 1,8 — tem tudo, gerencia nada. É o caso mais
/// comum numa PME que comprou ferramentas ao longo dos anos, e é exatamente o argumento do serviço
/// gerenciado.
/// </summary>
public static class CalculadoraDoDiagnostico
{
    /// <summary>Nota de uma resposta, de 0 a 1. Respeita perguntas invertidas.</summary>
    private static decimal Nota(PerguntaDoDiagnostico pergunta, string? opcao)
    {
        var bruta = opcao switch
        {
            CatalogoDeDominios.Sim => 1m,
            CatalogoDeDominios.Parcial => 0.5m,
            // Não saber não é meio-controle. Quem não sabe se tem, não gerencia — e o relatório
            // precisa refletir isso em vez de dar meio ponto por incerteza.
            _ => 0m,
        };
        return pergunta.RespostaBoaEhNao ? 1m - bruta : bruta;
    }

    private static SituacaoDoControle Situacao(PerguntaDoDiagnostico pergunta, string? opcao)
    {
        if (opcao is null) { return SituacaoDoControle.NaoAvaliado; }
        var nota = Nota(pergunta, opcao);
        return nota switch
        {
            1m => SituacaoDoControle.Tem,
            0.5m => SituacaoDoControle.Parcial,
            _ => opcao == CatalogoDeDominios.NaoSei ? SituacaoDoControle.NaoAvaliado : SituacaoDoControle.NaoTem,
        };
    }

    /// <summary>
    /// Se a pergunta deve aparecer, dadas as respostas até agora. Condição não satisfeita esconde a
    /// pergunta E a tira do denominador — perguntar sobre nuvem a quem não usa nuvem e depois
    /// contar como lacuna produziria um score punitivo e falso.
    /// </summary>
    public static bool Visivel(CondicaoDeExibicao? condicao, IReadOnlyDictionary<string, string?> respostas)
    {
        if (condicao is null) { return true; }
        return respostas.TryGetValue(condicao.PerguntaCodigo, out var valor)
               && valor is not null
               && condicao.Valores.Contains(valor);
    }

    /// <summary>
    /// Pergunta de qualidade: só aparece quando o controle PAI, que pontua, já existe. É o que
    /// alimenta maturidade em vez de cobertura.
    /// </summary>
    private static bool EhDeQualidade(DominioDeSeguranca dominio, PerguntaDoDiagnostico pergunta)
    {
        if (pergunta.SomenteSe is not { } cond) { return false; }
        var pai = dominio.Perguntas.FirstOrDefault(p => p.Codigo == cond.PerguntaCodigo);
        // Pai fora do domínio (ex.: "existe trabalho remoto?") ou pai que não pontua (ex.: "usa
        // nuvem?") são porteiros de aplicabilidade, não indicadores de qualidade.
        return pai is { Peso: > 0 } && cond.Valores.Contains(CatalogoDeDominios.Sim);
    }

    public static ResultadoDoDiagnostico Calcular(Models.Diagnostico diagnostico)
    {
        var respostas = diagnostico.Respostas.ToDictionary(r => r.PerguntaCodigo, r => r.Opcao);
        var porCodigo = diagnostico.Respostas.ToDictionary(r => r.PerguntaCodigo);

        var resultados = new List<ResultadoDoDominio>();
        decimal pesoCoberturaTotal = 0, notaCoberturaTotal = 0;
        decimal pesoQualidadeTotal = 0, notaQualidadeTotal = 0;
        int visiveisTotal = 0, respondidasTotal = 0, ausentes = 0, parciais = 0;
        var porOrigem = new Dictionary<OrigemDaInformacao, int>();

        foreach (var dominio in CatalogoDeDominios.Todos)
        {
            if (!Visivel(dominio.SomenteSe, respostas)) { continue; }

            decimal pesoCob = 0, notaCob = 0, pesoQual = 0, notaQual = 0;
            int visiveis = 0, respondidas = 0;
            var matriz = new List<LinhaDaMatriz>();

            foreach (var pergunta in dominio.Perguntas)
            {
                if (!Visivel(pergunta.SomenteSe, respostas)) { continue; }

                visiveis++;
                porCodigo.TryGetValue(pergunta.Codigo, out var resposta);
                var opcao = resposta?.Opcao;
                var temResposta = opcao is not null || resposta?.Texto is not null || resposta?.Numero is not null;
                if (temResposta) { respondidas++; }

                if (resposta is not null)
                {
                    porOrigem[resposta.Origem] = porOrigem.GetValueOrDefault(resposta.Origem) + 1;
                }

                // Não aplicável sai da conta inteira: não é lacuna nem controle.
                if (resposta?.Origem == OrigemDaInformacao.NaoAplicavel) { continue; }
                if (!dominio.Pontua || pergunta.Peso == 0 || pergunta.Tipo != TipoDePergunta.Controle) { continue; }
                if (opcao is null) { continue; }

                var nota = Nota(pergunta, opcao);
                if (EhDeQualidade(dominio, pergunta))
                {
                    pesoQual += pergunta.Peso;
                    notaQual += nota * pergunta.Peso;
                }
                else
                {
                    pesoCob += pergunta.Peso;
                    notaCob += nota * pergunta.Peso;
                    var situacao = Situacao(pergunta, opcao);
                    matriz.Add(new LinhaDaMatriz(pergunta, situacao, resposta?.Origem ?? OrigemDaInformacao.Declarado));
                    if (situacao == SituacaoDoControle.NaoTem) { ausentes++; }
                    if (situacao == SituacaoDoControle.Parcial) { parciais++; }
                }
            }

            visiveisTotal += visiveis;
            respondidasTotal += respondidas;
            pesoCoberturaTotal += pesoCob;
            notaCoberturaTotal += notaCob;
            pesoQualidadeTotal += pesoQual;
            notaQualidadeTotal += notaQual;

            if (!dominio.Pontua) { continue; }

            resultados.Add(new ResultadoDoDominio
            {
                Dominio = dominio,
                Cobertura = pesoCob == 0 ? 0 : (int)Math.Round(100 * notaCob / pesoCob),
                Maturidade = pesoQual == 0 ? null : Math.Round(5 * notaQual / pesoQual, 1),
                PerguntasVisiveis = visiveis,
                PerguntasRespondidas = respondidas,
                Matriz = matriz,
            });
        }

        var (uso, integracao) = CalcularFerramentas(diagnostico);

        return new ResultadoDoDiagnostico
        {
            Cobertura = pesoCoberturaTotal == 0 ? 0 : (int)Math.Round(100 * notaCoberturaTotal / pesoCoberturaTotal),
            Maturidade = pesoQualidadeTotal == 0 ? null : Math.Round(5 * notaQualidadeTotal / pesoQualidadeTotal, 1),
            UsoDoInvestimento = uso,
            Integracao = integracao,
            Completude = visiveisTotal == 0 ? 0 : (int)Math.Round(100m * respondidasTotal / visiveisTotal),
            Dominios = resultados,
            PorOrigem = porOrigem,
            ControlesAusentes = ausentes,
            ControlesParciais = parciais,
        };
    }

    /// <summary>
    /// Uso do investimento e integração, a partir do inventário de ferramentas.
    ///
    /// Uso mede quatro coisas por ferramenta: licenciada, atualizada, monitorada, e com alguém
    /// tratando o alerta. **Integração fica de fora do uso de propósito** — integrar ao L'Okta IA é
    /// o que nós vendemos, e misturar isso no indicador faria o número que argumenta "aproveite o
    /// que já tem" cair só porque o cliente ainda não nos contratou.
    /// </summary>
    private static (int? Uso, int? Integracao) CalcularFerramentas(Models.Diagnostico diagnostico)
    {
        var ferramentas = diagnostico.Ferramentas;
        if (ferramentas.Count == 0) { return (null, null); }

        var pontos = ferramentas.Sum(f =>
            (f.Licenciado ? 1 : 0) + (f.Atualizado ? 1 : 0) + (f.Monitorado ? 1 : 0) + (f.AlertasTratados ? 1 : 0));

        var uso = (int)Math.Round(100m * pontos / (ferramentas.Count * 4));
        var integracao = (int)Math.Round(100m * ferramentas.Count(f => f.IntegradaAoLokta) / ferramentas.Count);
        return (uso, integracao);
    }

    /// <summary>
    /// Gera os riscos a partir das lacunas. Só existe risco onde há resposta que o sustente — a
    /// função nunca inventa achado, e a origem da resposta viaja junto para o relatório poder
    /// distinguir o que foi declarado do que foi medido.
    /// </summary>
    public static List<DiagnosticoRisco> GerarRiscos(Models.Diagnostico diagnostico)
    {
        var respostas = diagnostico.Respostas.ToDictionary(r => r.PerguntaCodigo, r => r.Opcao);
        var porCodigo = diagnostico.Respostas.ToDictionary(r => r.PerguntaCodigo);
        var riscos = new List<DiagnosticoRisco>();

        foreach (var dominio in CatalogoDeDominios.Todos)
        {
            if (!Visivel(dominio.SomenteSe, respostas)) { continue; }

            foreach (var pergunta in dominio.Perguntas)
            {
                if (pergunta.RiscoSeNao is not { } gravidade) { continue; }
                if (!Visivel(pergunta.SomenteSe, respostas)) { continue; }
                if (!porCodigo.TryGetValue(pergunta.Codigo, out var resposta) || resposta.Opcao is null) { continue; }
                if (resposta.Origem == OrigemDaInformacao.NaoAplicavel) { continue; }

                var nota = Nota(pergunta, resposta.Opcao);
                if (nota >= 1m) { continue; }   // controle presente: não há lacuna

                // Controle parcial vale um degrau a menos de gravidade: existe, mas não cobre tudo.
                var efetiva = nota == 0.5m ? Reduzir(gravidade) : gravidade;

                // "Não sei" não é o mesmo que "não tem" — mas também não é conforto. Entra como
                // risco, com o texto dizendo que a ausência de resposta É o achado.
                var incerto = resposta.Opcao == CatalogoDeDominios.NaoSei;

                // O título muda com o caso, porque as três situações são achados diferentes e uma
                // frase só descreveria mal duas delas:
                //
                //   não      → a afirmação do catálogo:  "Sistemas críticos sem backup"
                //   parcial  → a mesma, qualificada:     "... — cobertura parcial"
                //   não sei  → a PERGUNTA, porque aqui o achado é literalmente ela ter ficado sem
                //              resposta. Afirmar a ausência do controle seria inventar: ninguém
                //              disse que não existe, disseram que não sabem.
                //
                // Sem TituloDoRisco cai no texto da pergunta, como era antes — assim uma pergunta
                // nova sem frase escrita ainda gera risco, em vez de aparecer com título vazio.
                var afirmacao = pergunta.TituloDoRisco ?? pergunta.Texto;
                var titulo = incerto
                    ? $"Sem visibilidade: {pergunta.Texto}"
                    : nota == 0.5m ? $"{afirmacao} — cobertura parcial" : afirmacao;

                riscos.Add(new DiagnosticoRisco
                {
                    DiagnosticoId = diagnostico.Id,
                    DominioCodigo = dominio.Codigo,
                    PerguntaCodigo = pergunta.Codigo,
                    Titulo = titulo,
                    Descricao = incerto
                        ? "Não foi possível confirmar a existência deste controle no levantamento. A ausência de resposta indica que ninguém acompanha o item hoje."
                        : pergunta.Ajuda ?? pergunta.Texto,
                    Gravidade = incerto ? Reduzir(gravidade) : efetiva,
                    Origem = resposta.Origem,
                    SeNaoTratar = pergunta.SeNaoTratar,
                    Recomendacao = pergunta.Recomendacao,
                });
            }
        }

        // Prioridade por gravidade; empate resolvido pela ordem do levantamento, que já segue a
        // sequência em que os controles se sustentam (não adianta SIEM antes de ter log).
        var ordenados = riscos
            .OrderByDescending(r => r.Gravidade)
            .ThenBy(r => CatalogoDeDominios.Buscar(r.DominioCodigo)?.Ordem ?? 99)
            .ToList();

        for (var i = 0; i < ordenados.Count; i++) { ordenados[i].Prioridade = i + 1; }
        return ordenados;
    }

    private static GravidadeRisco Reduzir(GravidadeRisco g) => g switch
    {
        GravidadeRisco.Critico => GravidadeRisco.Alto,
        GravidadeRisco.Alto => GravidadeRisco.Medio,
        _ => GravidadeRisco.Baixo,
    };
}
