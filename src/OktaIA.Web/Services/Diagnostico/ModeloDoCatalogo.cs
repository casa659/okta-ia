namespace OktaIA.Web.Services.Diagnostico;

/// <summary>Como a pergunta é respondida na tela.</summary>
public enum TipoDePergunta
{
    /// <summary>Sim / Parcialmente / Não / Não sei. A forma padrão de um controle.</summary>
    Controle,

    /// <summary>Lista fechada de opções (fabricante, modelo de operação).</summary>
    Escolha,

    /// <summary>Várias opções ao mesmo tempo (quais nuvens, quais fontes de log).</summary>
    Multipla,

    /// <summary>Texto livre curto. Nunca entra em cálculo — serve ao relatório.</summary>
    Texto,

    /// <summary>Número (quantidade de endpoints, dias de retenção, RTO em horas).</summary>
    Numero,
}

/// <summary>
/// Condição para uma pergunta aparecer. A lógica condicional da especificação: "se NÃO tem
/// firewall, pergunte quantos links de internet existem".
///
/// Deliberadamente simples — uma pergunta, um conjunto de valores. Condições compostas (E/OU
/// aninhados) foram deixadas de fora: elas tornam o questionário difícil de auditar e, na prática,
/// toda ramificação real do levantamento cabe nesta forma.
/// </summary>
/// <param name="PerguntaCodigo">De qual resposta esta pergunta depende.</param>
/// <param name="Valores">Valores que liberam a pergunta.</param>
public record CondicaoDeExibicao(string PerguntaCodigo, string[] Valores);

/// <summary>
/// Uma pergunta do catálogo.
///
/// <see cref="Codigo"/> é a chave estável gravada em cada resposta. **Nunca renomear um código já
/// usado** — a resposta gravada perderia o vínculo e o diagnóstico do cliente passaria a exibir a
/// pergunta como não respondida. Para mudar o sentido de uma pergunta, criar código novo.
/// </summary>
public record PerguntaDoDiagnostico
{
    public required string Codigo { get; init; }
    public required string Texto { get; init; }
    public TipoDePergunta Tipo { get; init; } = TipoDePergunta.Controle;

    /// <summary>Explicação para o consultor, não para o cliente. Aparece embaixo da pergunta.</summary>
    public string? Ajuda { get; init; }

    /// <summary>Opções, para <see cref="TipoDePergunta.Escolha"/> e <see cref="TipoDePergunta.Multipla"/>.</summary>
    public string[] Opcoes { get; init; } = [];

    public CondicaoDeExibicao? SomenteSe { get; init; }

    /// <summary>
    /// Peso no cálculo de cobertura. 0 = não entra na conta (perguntas de contexto e de detalhe).
    /// Controles cuja ausência costuma ser a causa raiz de um incidente pesam mais.
    /// </summary>
    public int Peso { get; init; } = 1;

    /// <summary>
    /// Se a resposta "não" gera risco automático, e de que gravidade. Nulo = ausência não é risco
    /// por si só (ex.: não usar nuvem não é um problema).
    /// </summary>
    public Models.GravidadeRisco? RiscoSeNao { get; init; }

    /// <summary>Consequência provável quando o controle não existe. Linguagem de risco, não de susto.</summary>
    public string? SeNaoTratar { get; init; }

    /// <summary>O que fazer a respeito. Vira a recomendação do relatório.</summary>
    public string? Recomendacao { get; init; }

    /// <summary>
    /// Controles de referência que esta pergunta toca (ex.: "NIST PR.AC-1", "CIS 6.3", "ISO A.8.5",
    /// "LGPD art. 46"). Serve ao mapa de frameworks do relatório.
    ///
    /// ⚠️ O relatório chama isso de "controles relacionados", NUNCA de conformidade: a plataforma
    /// enxerga uma fatia estreita do que um auditor avalia, e afirmar conformidade é o tipo de
    /// alegação que o auditor do cliente derruba na primeira pergunta.
    /// </summary>
    public string[] Frameworks { get; init; } = [];
}

/// <summary>
/// Um domínio de segurança — a unidade em que maturidade e cobertura são calculadas e apresentadas.
/// </summary>
public record DominioDeSeguranca
{
    public required string Codigo { get; init; }
    public required string Nome { get; init; }
    public required string Resumo { get; init; }

    /// <summary>Ordem no wizard. O levantamento segue a ordem em que a conversa flui numa reunião.</summary>
    public int Ordem { get; init; }

    /// <summary>Domínio de contexto (o perfil da empresa) não entra em cobertura nem maturidade.</summary>
    public bool Pontua { get; init; } = true;

    public CondicaoDeExibicao? SomenteSe { get; init; }

    public required PerguntaDoDiagnostico[] Perguntas { get; init; }
}
