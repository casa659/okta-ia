namespace OktaIA.Web.Models;

/// <summary>
/// De onde veio a informação de um controle. É a distinção mais importante deste módulo inteiro.
///
/// O L'Okta IA se construiu recusando afirmar o que não mediu. Um assessment é o oposto: tudo nele
/// é DECLARADO pelo cliente, sem prova. Se o resultado sair com a mesma cara do score do scanner,
/// a plataforma passa a afirmar precisão que não tem — e quem derruba isso é o auditor do cliente,
/// na frente dele. Por isso a origem viaja junto de cada resposta e aparece em toda tela e PDF.
/// </summary>
public enum OrigemDaInformacao
{
    /// <summary>Ninguém respondeu ainda. Não é "não tem" — é "não olhamos".</summary>
    NaoAvaliado,

    /// <summary>O cliente disse. Sem prova. É o estado normal de um assessment.</summary>
    Declarado,

    /// <summary>O cliente anexou evidência (print, relatório, config). Alguém olhou o documento.</summary>
    Evidenciado,

    /// <summary>Confirmado por leitura técnica nossa — scanner, conector, console do fabricante.</summary>
    Validado,

    /// <summary>Não se aplica ao ambiente do cliente. Difere de "não tem" no cálculo.</summary>
    NaoAplicavel,
}

/// <summary>Situação de um controle no ambiente do cliente.</summary>
public enum SituacaoDoControle
{
    NaoAvaliado,
    NaoTem,
    /// <summary>Tem a tecnologia, mas ela não cobre tudo, não está integrada, ou ninguém trata o alerta.</summary>
    Parcial,
    Tem,
    NaoAplicavel,
}

/// <summary>Estágio do diagnóstico. Rascunho é o padrão — a reunião raramente termina numa sentada.</summary>
public enum StatusDiagnostico
{
    Rascunho,
    EmAndamento,
    Concluido,
    /// <summary>Arquivado sem concluir (proposta perdida, cliente sumiu). Não entra em média nenhuma.</summary>
    Arquivado,
}

/// <summary>
/// Um diagnóstico de uma empresa, numa data. Fica preso à `Company` como todo o resto da
/// plataforma — sem isso o consultor de um cliente leria o levantamento de outro.
///
/// Preenchido pelo CONSULTOR na reunião, não pelo cliente por link (decisão de 15/08/2026): é o
/// mesmo modelo gerenciado do resto do produto — nós operamos, o cliente observa.
/// </summary>
public class Diagnostico
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>Como o consultor identifica esta rodada ("Diagnóstico inicial", "Revisão anual").</summary>
    public required string Titulo { get; set; }

    public StatusDiagnostico Status { get; set; } = StatusDiagnostico.Rascunho;

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public required string CriadoPor { get; set; }
    public DateTimeOffset? ConcluidoEm { get; set; }

    /// <summary>Quem respondeu do lado do cliente. Vai no relatório: um assessment sem fonte não vale nada.</summary>
    public string? Respondente { get; set; }
    public string? RespondenteCargo { get; set; }

    /// <summary>Data da conversa, quando diferente da data de criação do registro.</summary>
    public DateOnly? RealizadoEm { get; set; }

    public string? Observacoes { get; set; }

    // ── Números derivados, GRAVADOS ao concluir ─────────────────────────────
    // Recalcular na leitura faria o relatório entregue ao cliente mudar sozinho quando o catálogo
    // de perguntas evoluísse — o PDF que ele guardou deixaria de bater com a tela. Congela-se.

    /// <summary>0 a 100. Quanto do que precisa existir, existe. Separado de maturidade de propósito.</summary>
    public int? Cobertura { get; set; }

    /// <summary>0,0 a 5,0. Quão bem gerenciado é o que existe.</summary>
    public decimal? Maturidade { get; set; }

    /// <summary>
    /// 0 a 100. Quanto das capacidades que o cliente JÁ PAGA estão de fato em uso. É o número mais
    /// comercial do módulo: sustenta "não precisa comprar a 13ª ferramenta" em vez de empurrar mais.
    /// </summary>
    public int? UsoDoInvestimento { get; set; }

    /// <summary>Percentual dos controles existentes que estão integrados ao L'Okta IA.</summary>
    public int? Integracao { get; set; }

    public List<DiagnosticoResposta> Respostas { get; set; } = [];
    public List<DiagnosticoFerramenta> Ferramentas { get; set; } = [];
    public List<DiagnosticoRisco> Riscos { get; set; } = [];
    public List<DiagnosticoAcao> Acoes { get; set; } = [];
}

/// <summary>
/// Resposta a UMA pergunta do catálogo.
///
/// A pergunta é referenciada por <see cref="PerguntaCodigo"/> (string estável, ex.: "rede.firewall"),
/// não por FK para uma tabela de perguntas. Motivo: o catálogo vive em código (é versionado junto
/// com a lógica que o interpreta), e uma resposta gravada precisa continuar legível mesmo depois de
/// o catálogo mudar. O preço é não ter editor de questionário na tela — decisão consciente da
/// Fase 1; quando um cliente quiser questionário próprio, a migração é acrescentar a tabela e
/// manter o código como semente.
/// </summary>
public class DiagnosticoResposta
{
    public int Id { get; set; }

    public int DiagnosticoId { get; set; }
    public Diagnostico? Diagnostico { get; set; }

    public required string PerguntaCodigo { get; set; }

    /// <summary>Opção escolhida quando a pergunta é de lista ("sim", "nao", "naosei", "parcial").</summary>
    public string? Opcao { get; set; }

    /// <summary>Resposta livre, quando a pergunta pede texto (fabricante, versão, observação).</summary>
    public string? Texto { get; set; }

    /// <summary>Resposta numérica (quantidade de endpoints, dias de retenção, RTO em horas).</summary>
    public int? Numero { get; set; }

    public SituacaoDoControle Situacao { get; set; } = SituacaoDoControle.NaoAvaliado;
    public OrigemDaInformacao Origem { get; set; } = OrigemDaInformacao.NaoAvaliado;

    /// <summary>Nome do arquivo de evidência, quando houver. O binário não fica no banco.</summary>
    public string? EvidenciaArquivo { get; set; }

    public DateTimeOffset RespondidoEm { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Tecnologia de segurança que o cliente JÁ possui.
///
/// Existe separado das respostas porque é o insumo do argumento comercial central: aproveitar o
/// que já foi comprado. Ter a ferramenta não significa que o controle está adequado — daí os
/// campos de licença, atualização, integração e tratamento de alerta, que é onde a conversa
/// costuma virar.
/// </summary>
public class DiagnosticoFerramenta
{
    public int Id { get; set; }

    public int DiagnosticoId { get; set; }
    public Diagnostico? Diagnostico { get; set; }

    /// <summary>Domínio a que pertence ("endpoint", "rede", "identidade"...).</summary>
    public required string DominioCodigo { get; set; }

    public required string Categoria { get; set; }   // "EDR / XDR", "Firewall", "Backup"
    public required string Fabricante { get; set; }
    public string? Produto { get; set; }
    public string? Versao { get; set; }

    public int? Quantidade { get; set; }
    public string? Responsavel { get; set; }
    public DateOnly? LicencaExpiraEm { get; set; }

    public bool Licenciado { get; set; }
    public bool Atualizado { get; set; }
    public bool Monitorado { get; set; }
    /// <summary>Alguém trata os alertas que ela gera — a pergunta que separa ferramenta de proteção.</summary>
    public bool AlertasTratados { get; set; }

    /// <summary>Já integrada ao L'Okta IA. Alimenta o indicador de Integração.</summary>
    public bool IntegradaAoLokta { get; set; }

    /// <summary>Existe conector implementado para este fabricante — define se dá para integrar hoje.</summary>
    public string? ConectorSlug { get; set; }

    public string? Observacoes { get; set; }
}

/// <summary>Gravidade de um risco levantado no diagnóstico. Mesma escala do resto da plataforma.</summary>
public enum GravidadeRisco { Baixo, Medio, Alto, Critico }

/// <summary>
/// Risco derivado de uma lacuna. Sempre nasce de uma resposta — nunca é inventado pela IA sem
/// dado que o sustente; o campo <see cref="Origem"/> herda a origem da resposta que o gerou, para
/// o relatório poder dizer se aquilo foi declarado ou medido.
/// </summary>
public class DiagnosticoRisco
{
    public int Id { get; set; }

    public int DiagnosticoId { get; set; }
    public Diagnostico? Diagnostico { get; set; }

    public required string DominioCodigo { get; set; }
    /// <summary>Pergunta/controle que revelou a lacuna. Rastreabilidade do achado.</summary>
    public required string PerguntaCodigo { get; set; }

    public required string Titulo { get; set; }
    public required string Descricao { get; set; }

    public GravidadeRisco Gravidade { get; set; }
    public OrigemDaInformacao Origem { get; set; } = OrigemDaInformacao.Declarado;

    /// <summary>Consequência provável se nada for feito. Linguagem de risco, nunca afirmação absoluta.</summary>
    public string? SeNaoTratar { get; set; }

    public string? Recomendacao { get; set; }

    /// <summary>Ordem de tratamento, 1 = primeiro. Calculada, mas editável pelo consultor.</summary>
    public int Prioridade { get; set; }
}

/// <summary>Janela do plano de ação.</summary>
public enum HorizonteAcao
{
    /// <summary>0 a 30 dias — o que não pode esperar.</summary>
    Imediato,
    /// <summary>31 a 90 dias — alta prioridade.</summary>
    CurtoPrazo,
    /// <summary>3 a 6 meses — projetos estruturantes.</summary>
    MedioPrazo,
    /// <summary>6 a 12 meses — evolução de maturidade.</summary>
    LongoPrazo,
}

/// <summary>
/// O que se propõe fazer. Cada ação nasce ligada a um risco: plano de ação sem risco de origem é
/// lista de desejos, e é o que faz um diagnóstico virar catálogo de venda.
/// </summary>
public class DiagnosticoAcao
{
    public int Id { get; set; }

    public int DiagnosticoId { get; set; }
    public Diagnostico? Diagnostico { get; set; }

    public int? RiscoId { get; set; }
    public DiagnosticoRisco? Risco { get; set; }

    public required string Titulo { get; set; }
    public string? Descricao { get; set; }

    public HorizonteAcao Horizonte { get; set; }

    /// <summary>
    /// Como esta ação se resolve. Separa o que o cliente JÁ TEM (e só precisa ligar) do que
    /// precisa comprar — é a diferença entre uma proposta honesta e um orçamento inflado.
    /// </summary>
    public TipoDeEncaminhamento Encaminhamento { get; set; }

    public string? Responsavel { get; set; }
    public bool Concluida { get; set; }
}

/// <summary>Para onde vai cada lacuna na proposta comercial.</summary>
public enum TipoDeEncaminhamento
{
    /// <summary>Já possui e está adequado. Não entra na proposta — entra no argumento.</summary>
    JaPossui,
    /// <summary>Tem a ferramenta, falta configurar/licenciar/atualizar. Serviço, não compra.</summary>
    Otimizar,
    /// <summary>Existe conector — dá para trazer para o L'Okta IA sem trocar nada.</summary>
    Integrar,
    /// <summary>Controle inexistente. Aqui sim há algo a adquirir.</summary>
    Implementar,
    /// <summary>Tecnologia existe, falta quem opere. É o serviço gerenciado.</summary>
    Gerenciar,
}
