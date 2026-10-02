namespace OktaIA.Web.Models;

/// <summary>Em qual total a frente entra.</summary>
public enum CobrancaFrente
{
    /// <summary>Projeto com fim — entra na implantação.</summary>
    Implantacao,
    /// <summary>Serviço que continua — entra na mensalidade.</summary>
    Mensal,
}

/// <summary>
/// A natureza da frente, que é a pergunta comercial mais importante da lista.
///
/// 🔴 A DIFERENÇA NÃO É DE PREÇO, É DE PROMESSA. Produto está no ar e pode ser demonstrado hoje;
/// projeto é trabalho a fazer, com prazo e pessoas. Misturar os dois numa lista só faz o cliente
/// assinar achando que tudo começa a funcionar na segunda-feira — e a primeira reunião de
/// cobrança é sobre o que ele entendeu, não sobre o que estava escrito.
/// </summary>
public enum NaturezaFrente
{
    /// <summary>Software em operação. Demonstrável antes de assinar.</summary>
    Produto,
    /// <summary>Consultoria: alguém faz, entrega um documento, acaba.</summary>
    Projeto,
}

/// <summary>
/// Uma frente da adequação à LGPD, com o artigo que ela atende.
///
/// 🔴 POR QUE EXISTE (02/10/2026, pedido do dono: "orçar especificamente para LGPD"): o
/// `/Admin/Orcamentos` precificava **monitoramento**, que atende quatro artigos (46, 6º VII e VIII,
/// 48 e 49). "Se enquadrar na lei" é bem maior do que isso — e um orçamento que entrega só o
/// monitoramento, chamado de "adequação à LGPD", vende ao cliente a impressão de que ele ficou em
/// conformidade. É o mesmo erro que o relatório de postura combate ao listar o que NÃO cobre, só
/// que agora com uma nota fiscal em cima.
///
/// ⚠️ CADA FRENTE CARREGA O ARTIGO. Sem ele, a lista vira menu de serviços e o cliente não tem como
/// conferir por que precisa de cada item — nem o vendedor, defender o preço.
/// </summary>
/// <param name="Chave">Id curto e estável. É o que vai gravado no orçamento; não mude depois.</param>
/// <param name="Artigo">O dispositivo da Lei nº 13.709/2018, como se cita. Vazio quando não há um.</param>
/// <param name="OQueEntrega">O produto do trabalho — o que o cliente recebe na mão.</param>
/// <param name="Onde">
/// Em que sistema a frente vive, quando é produto. Vai no documento: o cliente precisa saber que
/// aquilo é uma tela que ele já pode abrir, e não uma promessa.
/// </param>
public record FrenteLgpd(
    string Chave,
    string Artigo,
    string Nome,
    string OQueEntrega,
    CobrancaFrente Cobranca,
    NaturezaFrente Natureza,
    decimal ValorPadrao,
    string? Onde = null);

/// <summary>
/// As frentes que a L'okta orça. Lista fixa no código, preço editável pelo dono.
///
/// 🔴 OS VALORES SÃO CHUTE DE PARTIDA, e a tela diz isso. Nenhum saiu de pesquisa de mercado —
/// existem para o formulário nunca abrir com R$ 0 e para dar ordem de grandeza. A mesma regra dos
/// outros parâmetros do orçamento: preço é do dono, não do código.
///
/// ⚠️ O MONITORAMENTO NÃO ESTÁ AQUI. Ele é calculado pelo levantamento de máquinas
/// (<see cref="Services.CalculadoraDeOrcamento"/>) e entra no total com o preço de lá. Repeti-lo
/// nesta lista criaria dois preços para a mesma coisa, e um dia eles divergiriam.
/// </summary>
public static class CatalogoFrentesLgpd
{
    public static readonly IReadOnlyList<FrenteLgpd> Todas = new[]
    {
        // ── O QUE JÁ ESTÁ NO AR ─────────────────────────────────────────────────────────────
        // O portal LGPD (LinkEscola) em operação. Cada linha aqui é uma tela que existe e que o
        // cliente pode ver funcionando antes de assinar.

        new FrenteLgpd("titulares", "Art. 18",
            "Atendimento aos direitos do titular",
            "Portal onde o titular abre o pedido com protocolo, acompanha o andamento e recebe a "
            + "resposta. Prazo contado pelo sistema, trilha de tudo que foi feito e relatório em PDF "
            + "ao fim — é a prova de que o direito foi atendido, e não só a promessa de atender.",
            CobrancaFrente.Mensal, NaturezaFrente.Produto, 900m, Onde: "portal LGPD"),

        new FrenteLgpd("encarregado", "Art. 41",
            "Encarregado publicado e canal de atendimento",
            "Identificação do encarregado publicada ao titular e canal de contato aberto, com as "
            + "mensagens registradas. Painel do DPO com a fila, os prazos e o que está vencendo.",
            CobrancaFrente.Mensal, NaturezaFrente.Produto, 600m, Onde: "portal LGPD"),

        new FrenteLgpd("consentimentos", "Arts. 7º e 8º",
            "Registro de finalidades e consentimentos",
            "Cada finalidade de tratamento cadastrada com a sua base legal, e o consentimento "
            + "registrado por titular — com data, versão do texto e a possibilidade de revogar.",
            CobrancaFrente.Mensal, NaturezaFrente.Produto, 600m, Onde: "portal LGPD"),

        new FrenteLgpd("prestacao", "Art. 6º, X",
            "Prestação de contas: trilha e relatórios",
            "Trilha de quem acessou qual dado e quando, com relatórios emitidos em PDF e datados. É "
            + "o que se apresenta numa fiscalização para demonstrar — não afirmar — que as medidas "
            + "existem.",
            CobrancaFrente.Mensal, NaturezaFrente.Produto, 600m, Onde: "portal LGPD"),

        new FrenteLgpd("agente", "",
            "Agente de IA com base jurídica da LGPD",
            "Consulta à lei e à regulamentação dentro do portal, com citação do artigo em cada "
            + "resposta e validação das referências. Atende a equipe que opera o dia a dia, que é "
            + "quem decide errado quando não tem a quem perguntar.",
            CobrancaFrente.Mensal, NaturezaFrente.Produto, 450m, Onde: "portal LGPD"),

        // ── O QUE É PROJETO ─────────────────────────────────────────────────────────────────
        // Consultoria: alguém levanta, escreve e entrega. Não existe tela para demonstrar.

        new FrenteLgpd("inventario", "Art. 37",
            "Registro das operações de tratamento",
            "O inventário de dados da instituição: quais dados pessoais existem, onde ficam, por que "
            + "são tratados, quem acessa e por quanto tempo. É a base das outras frentes — sem ele, "
            + "as demais trabalham no escuro.",
            CobrancaFrente.Implantacao, NaturezaFrente.Projeto, 4800m),

        new FrenteLgpd("criancas", "Art. 14",
            "Regime de dados de crianças e adolescentes",
            "Consentimento específico e destacado de um dos pais ou responsável, critério do melhor "
            + "interesse e revisão das coletas que envolvem alunos menores.",
            CobrancaFrente.Implantacao, NaturezaFrente.Projeto, 3600m),

        new FrenteLgpd("retencao", "Art. 16",
            "Política de retenção e eliminação",
            "Por quanto tempo cada dado fica, o que acontece no fim do prazo e como a eliminação é "
            + "comprovada. Dado guardado sem prazo é passivo que só cresce.",
            CobrancaFrente.Implantacao, NaturezaFrente.Projeto, 2400m),

        new FrenteLgpd("contratos", "Art. 39",
            "Contratos com operadores e fornecedores",
            "Revisão e adequação dos contratos com quem trata dados em nome da instituição — sistema "
            + "acadêmico, folha, nuvem, transporte escolar.",
            CobrancaFrente.Implantacao, NaturezaFrente.Projeto, 3000m),

        new FrenteLgpd("ripd", "Art. 38",
            "Relatório de impacto à proteção de dados (RIPD)",
            "O RIPD dos tratamentos de maior risco — os que envolvem dados de crianças, biometria ou "
            + "decisão automatizada. É o documento que a ANPD pode exigir a qualquer momento.",
            CobrancaFrente.Implantacao, NaturezaFrente.Projeto, 5400m),

        new FrenteLgpd("planoincidente", "Art. 48",
            "Plano de resposta e comunicação de incidente",
            "Quem decide, em quanto tempo e por qual canal — com o prazo de 3 dias úteis da Resolução "
            + "CD/ANPD nº 15/2024 escrito e ensaiado antes de precisar dele. A detecção já vem do "
            + "monitoramento; o que falta é a decisão escrita.",
            CobrancaFrente.Implantacao, NaturezaFrente.Projeto, 2400m),

        new FrenteLgpd("treinamento", "",
            "Treinamento das pessoas que tratam dados",
            "Turmas para secretaria, coordenação, TI e professores. A lei não exige por artigo "
            + "próprio, mas é o controle que mais evita incidente: quase todo vazamento começa em "
            + "alguém que não sabia.",
            CobrancaFrente.Implantacao, NaturezaFrente.Projeto, 1800m),
    };

    public static FrenteLgpd? Achar(string chave) =>
        Todas.FirstOrDefault(f => f.Chave == chave);

    public static IEnumerable<FrenteLgpd> De(NaturezaFrente natureza) =>
        Todas.Where(f => f.Natureza == natureza);
}

/// <summary>
/// Uma frente ESCOLHIDA num orçamento, com o valor que valeu naquele dia.
///
/// 🔴 O VALOR FICA GRAVADO, não é relido do catálogo. Orçamento é promessa com data: mudar o preço
/// de tabela amanhã não pode mudar o que o cliente recebeu ontem. Mesma decisão já tomada para os
/// totais em <see cref="OrcamentoMonitoramento.ValorImplantacao"/>.
/// </summary>
public record FrenteEscolhida(string Chave, decimal Valor);
