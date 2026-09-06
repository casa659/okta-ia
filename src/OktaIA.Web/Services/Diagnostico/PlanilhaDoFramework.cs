using ClosedXML.Excel;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Diagnostico;

/// <summary>
/// A planilha de levantamento de um framework: gerar para o cliente preencher, e ler de volta.
///
/// ⚠️ GERAR E LER MORAM NO MESMO ARQUIVO, de propósito. São os dois lados de um formato, e formato
/// escrito em dois lugares diverge no primeiro ajuste — o sintoma seria a planilha voltar e a
/// importação "não achar nada", sem erro nenhum a que se agarrar.
///
/// ⚠️ A COLUNA "Código" É O CONTRATO. É ela, e não a posição da linha nem o texto da pergunta, que
/// liga a resposta ao catálogo. Por isso a aba vai PROTEGIDA com só Resposta e Observação
/// liberadas: quem apagar um código não perde uma linha, perde a ligação — e o dado volta órfão.
///
/// ⚠️ O QUE VOLTA DAQUI É DECLARADO, nunca medido. Uma planilha preenchida pelo cliente é a
/// definição de declaração sem prova; ver <see cref="OrigemDaInformacao"/>. Tratá-la como medição
/// daria ao número da planilha a mesma cara do número do scanner, e é isso que o auditor do cliente
/// derruba na frente dele.
/// </summary>
public class PlanilhaDoFramework
{
    private const string Aba = "Levantamento";

    /// <summary>Onde ficam os cabeçalhos. Ler procura por eles em vez de confiar neste número.</summary>
    private const int LinhaDoCabecalho = 7;

    private const string ColunaCodigo = "Código";
    private const string ColunaResposta = "Resposta";
    private const string ColunaObservacao = "Observação";

    // ── O vocabulário da planilha ────────────────────────────────────────────
    //
    // ⚠️ Quem preenche vê PALAVRA, o banco guarda CÓDIGO. A tela grava "naosei"; pedir isso a uma
    // pessoa numa planilha é pedir que ela digite o identificador interno do sistema — e o primeiro
    // "Não sei" escrito por extenso voltaria como resposta inválida.
    private static readonly (string Palavra, string Codigo)[] RespostasDeControle =
    [
        ("Sim", CatalogoDeDominios.Sim),
        ("Parcialmente", CatalogoDeDominios.Parcial),
        ("Não", CatalogoDeDominios.Nao),
        ("Não sei", CatalogoDeDominios.NaoSei),
    ];

    private static string PalavraDe(string codigo) =>
        RespostasDeControle.FirstOrDefault(r => r.Codigo == codigo).Palavra ?? "";

    private static string? CodigoDe(string palavra)
    {
        var t = (palavra ?? "").Trim();
        foreach (var r in RespostasDeControle)
        {
            if (string.Equals(r.Palavra, t, StringComparison.OrdinalIgnoreCase)) { return r.Codigo; }
            // Aceita o código cru também: quem copiou de outra planilha, ou automatizou, não erra por isso.
            if (string.Equals(r.Codigo, t, StringComparison.OrdinalIgnoreCase)) { return r.Codigo; }
        }
        return null;
    }

    // ── Gerar ────────────────────────────────────────────────────────────────

    public byte[] Gerar(CatalogoDeFrameworks.Framework framework, string empresa)
    {
        var itens = CatalogoDeFrameworks.QuestionarioDe(framework.Prefixo);

        using var livro = new XLWorkbook();
        var aba = livro.Worksheets.Add(Aba);

        aba.Cell(1, 1).Value = framework.Nome;
        aba.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(15);
        aba.Cell(2, 1).Value = empresa;
        aba.Cell(2, 1).Style.Font.SetFontSize(12).Font.SetFontColor(XLColor.FromHtml("#44546A"));
        aba.Cell(3, 1).Value = $"Gerado em {DateTimeOffset.Now:dd/MM/yyyy 'às' HH:mm}"
                             + $" · {itens.Count} pergunta(s) · {framework.Controles} controle(s) tocado(s)";
        aba.Cell(3, 1).Style.Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#7F8FA6"));

        // ⚠️ A ressalva vai DENTRO do arquivo, e não só na tela que o gerou. A planilha circula por
        // e-mail, é reencaminhada e é aberta meses depois por quem nunca viu esta plataforma — e é
        // nessa hora que ela seria lida como "avaliação de conformidade ISO".
        aba.Cell(4, 1).Value =
            "Este é um levantamento técnico: as perguntas do nosso catálogo que tocam controles deste "
            + "framework. NÃO é auditoria nem avaliação de conformidade, e não cobre o framework "
            + "inteiro. As respostas são declaradas por quem preenche, sem verificação.";
        aba.Cell(4, 1).Style.Font.SetFontSize(9).Font.SetItalic()
            .Font.SetFontColor(XLColor.FromHtml("#8A6D1F"));

        aba.Cell(5, 1).Value =
            "Como preencher: responda APENAS nas colunas Resposta e Observação. As demais estão "
            + "bloqueadas porque a coluna Código é o que liga cada linha ao sistema. Devolva o arquivo "
            + "pelo mesmo lugar de onde ele foi baixado.";
        aba.Cell(5, 1).Style.Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#44546A"));

        string[] cabecalhos =
        [
            ColunaCodigo, "Domínio", "Controles", "Pergunta", "Como responder",
            ColunaResposta, ColunaObservacao,
        ];

        for (var c = 0; c < cabecalhos.Length; c++)
        {
            var celula = aba.Cell(LinhaDoCabecalho, c + 1);
            celula.Value = cabecalhos[c];
            celula.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
                  .Fill.SetBackgroundColor(XLColor.FromHtml("#0B45DD"))
                  .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }

        var linha = LinhaDoCabecalho + 1;
        foreach (var item in itens)
        {
            aba.Cell(linha, 1).Value = item.Pergunta.Codigo;
            aba.Cell(linha, 2).Value = item.Dominio.Nome;
            aba.Cell(linha, 3).Value = string.Join(", ", item.Controles);
            aba.Cell(linha, 4).Value = item.Pergunta.Texto;
            aba.Cell(linha, 5).Value = ComoResponder(item.Pergunta);

            aba.Cell(linha, 1).Style.Font.SetFontColor(XLColor.FromHtml("#8A96AB"));
            aba.Cell(linha, 3).Style.Font.SetFontColor(XLColor.FromHtml("#0B45DD"));
            aba.Cell(linha, 4).Style.Alignment.SetWrapText(true);
            aba.Cell(linha, 5).Style.Alignment.SetWrapText(true)
               .Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#7F8FA6"));

            // As duas colunas que a pessoa escreve: destravadas e destacadas.
            foreach (var col in new[] { 6, 7 })
            {
                aba.Cell(linha, col).Style
                   .Protection.SetLocked(false)
                   .Fill.SetBackgroundColor(XLColor.FromHtml("#FFF9E6"))
                   .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                   .Border.SetOutsideBorderColor(XLColor.FromHtml("#D9C88A"));
            }

            // Lista fechada só onde ELA EXISTE. Pergunta de texto ou número com validação de lista
            // seria uma trava que impede a resposta certa.
            var opcoes = OpcoesDe(item.Pergunta);
            if (opcoes.Length > 0)
            {
                var validacao = aba.Cell(linha, 6).CreateDataValidation();
                validacao.List(string.Join(",", opcoes), inCellDropdown: true);
                validacao.IgnoreBlanks = true;
                // ⚠️ Aviso, não bloqueio: o Excel recusaria silenciosamente um valor colado, e
                // quem preenche cinquenta linhas colando perderia respostas sem ver.
                validacao.ErrorStyle = XLErrorStyle.Warning;
                validacao.ErrorTitle = "Valor fora da lista";
                validacao.ErrorMessage = "Use uma das opções sugeridas — outras não serão importadas.";
            }

            linha++;
        }

        aba.Column(1).Width = 26;
        aba.Column(2).Width = 18;
        aba.Column(3).Width = 22;
        aba.Column(4).Width = 62;
        aba.Column(5).Width = 30;
        aba.Column(6).Width = 18;
        aba.Column(7).Width = 42;
        aba.Row(LinhaDoCabecalho).Height = 22;
        aba.SheetView.FreezeRows(LinhaDoCabecalho);
        aba.Range(LinhaDoCabecalho, 1, Math.Max(linha - 1, LinhaDoCabecalho), cabecalhos.Length)
           .SetAutoFilter();

        // ⚠️ Proteção SEM SENHA. Não é segurança — é um guarda-costas contra o clique errado, e o
        // consultor precisa poder destravar quando o cliente pedir uma coluna a mais. Senha aqui só
        // criaria um segredo a mais para alguém perder.
        aba.Protect().AllowedElements = XLSheetProtectionElements.SelectEverything
                                      | XLSheetProtectionElements.FormatCells
                                      | XLSheetProtectionElements.AutoFilter;

        DesenharFicha(livro, empresa);

        using var memoria = new MemoryStream();
        livro.SaveAs(memoria);
        return memoria.ToArray();
    }

    // ── A segunda aba: o parque de máquinas e o que já existe ───────────────────

    private const string AbaFicha = "Parque e Serviço";

    // ⚠️ AS MESMAS PERGUNTAS DO ORÇAMENTO (06/09/2026, pedido do dono), literalmente — o texto de
    // ajuda de cada linha é copiado de `Pages/Admin/Orcamentos.cshtml`. Duas telas perguntando a
    // mesma coisa com palavras diferentes fariam o consultor decidir qual delas vale quando
    // divergissem, e aqui o objetivo é o oposto: quem preenche a planilha não precisa ser
    // perguntado de novo na hora de montar o orçamento.
    private static readonly (string Rotulo, string? Ajuda)[] LinhasDaFicha =
    [
        ("Já usa alguma ferramenta de monitoramento de segurança (SIEM/EDR)?",
            "Wazuh próprio, Microsoft Defender, outro. Responda Sim ou Não."),
        ("Qual ferramenta? (só se a resposta acima for Sim)", null),
        ("Estações Windows", null),
        ("Estações Linux / macOS", null),
        ("Servidores", null),
        ("Destes, quantos servidores respondem na internet?",
            "Servidor público gera tentativa de invasão diária — é o que dá trabalho."),
        ("Trata dados de crianças e adolescentes?",
            "Escola, creche, curso infantil, clínica pediátrica. Responda Sim ou Não."),
        ("Cobertura de atendimento desejada", "24 × 7 só faz sentido com plantão de verdade por trás."),
        ("Retenção de dados desejada (dias)", "90 é o padrão e cabe no disco contratado."),
        ("Quem hospeda o servidor de segurança?", null),
        ("Observações", null),
    ];

    private const string ColunaPergunta = "Pergunta";

    private void DesenharFicha(XLWorkbook livro, string empresa)
    {
        var aba = livro.Worksheets.Add(AbaFicha);

        aba.Cell(1, 1).Value = "Parque de máquinas e ferramenta atual";
        aba.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(15);
        aba.Cell(2, 1).Value = empresa;
        aba.Cell(2, 1).Style.Font.SetFontSize(12).Font.SetFontColor(XLColor.FromHtml("#44546A"));
        aba.Cell(3, 1).Value =
            "Sem isto não dá para calcular o preço nem saber se o serviço é implantar algo novo ou "
            + "administrar o que já existe. Responda apenas na coluna Resposta.";
        aba.Cell(3, 1).Style.Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#44546A"));

        const int linhaCab = 5;
        foreach (var (texto, col) in new[] { (ColunaPergunta, 1), ("Ajuda", 2), (ColunaResposta, 3) })
        {
            var celula = aba.Cell(linhaCab, col);
            celula.Value = texto;
            celula.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
                  .Fill.SetBackgroundColor(XLColor.FromHtml("#0B45DD"));
        }

        var linha = linhaCab + 1;
        foreach (var (rotulo, ajuda) in LinhasDaFicha)
        {
            aba.Cell(linha, 1).Value = rotulo;
            aba.Cell(linha, 1).Style.Alignment.SetWrapText(true);
            aba.Cell(linha, 2).Value = ajuda ?? "";
            aba.Cell(linha, 2).Style.Font.SetFontSize(8.5).Font.SetFontColor(XLColor.FromHtml("#7F8FA6"))
               .Alignment.SetWrapText(true);

            aba.Cell(linha, 3).Style
               .Protection.SetLocked(false)
               .Fill.SetBackgroundColor(XLColor.FromHtml("#FFF9E6"))
               .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
               .Border.SetOutsideBorderColor(XLColor.FromHtml("#D9C88A"));

            // Só nas perguntas Sim/Não/escolha — número e texto livre ficam sem lista fechada.
            if (rotulo.EndsWith("?") && !rotulo.StartsWith("Estações") && !rotulo.StartsWith("Servidores")
                && !rotulo.StartsWith("Destes") && !rotulo.StartsWith("Retenção"))
            {
                var validacao = aba.Cell(linha, 3).CreateDataValidation();
                validacao.List("Sim,Não", inCellDropdown: true);
                validacao.IgnoreBlanks = true;
                validacao.ErrorStyle = XLErrorStyle.Warning;
            }
            else if (rotulo.StartsWith("Cobertura"))
            {
                var validacao = aba.Cell(linha, 3).CreateDataValidation();
                validacao.List("Horário comercial,Estendida (12h),24 x 7", inCellDropdown: true);
                validacao.IgnoreBlanks = true;
                validacao.ErrorStyle = XLErrorStyle.Warning;
            }
            else if (rotulo.StartsWith("Quem hospeda"))
            {
                var validacao = aba.Cell(linha, 3).CreateDataValidation();
                validacao.List("Nós hospedamos,A empresa hospeda", inCellDropdown: true);
                validacao.IgnoreBlanks = true;
                validacao.ErrorStyle = XLErrorStyle.Warning;
            }

            linha++;
        }

        aba.Column(1).Width = 52;
        aba.Column(2).Width = 46;
        aba.Column(3).Width = 30;
        aba.Row(linhaCab).Height = 20;
        aba.SheetView.FreezeRows(linhaCab);

        aba.Protect().AllowedElements = XLSheetProtectionElements.SelectEverything
                                      | XLSheetProtectionElements.FormatCells;
    }

    private static string[] OpcoesDe(PerguntaDoDiagnostico p) => p.Tipo switch
    {
        TipoDePergunta.Controle => RespostasDeControle.Select(r => r.Palavra).ToArray(),
        TipoDePergunta.Escolha => p.Opcoes,
        _ => [],
    };

    private static string ComoResponder(PerguntaDoDiagnostico p)
    {
        var como = p.Tipo switch
        {
            TipoDePergunta.Controle => "Escolha na lista.",
            TipoDePergunta.Escolha => "Escolha na lista.",
            TipoDePergunta.Multipla => "Liste o que se aplica, separado por vírgula.",
            TipoDePergunta.Numero => "Um número.",
            _ => "Texto livre.",
        };
        return string.IsNullOrWhiteSpace(p.Ajuda) ? como : $"{como} {p.Ajuda}";
    }

    // ── Ler de volta ─────────────────────────────────────────────────────────

    /// <param name="Codigo">O código da pergunta, tal como veio da coluna Código.</param>
    /// <param name="Reconhecida">Falso = o código não existe no catálogo (planilha velha, ou editada).</param>
    public record LinhaLida(string Codigo, bool Reconhecida, string? Opcao, string? Texto,
                            int? Numero, string? Observacao, string? RespostaCrua);

    /// <summary>
    /// O que veio da aba "Parque e Serviço" — as mesmas perguntas do orçamento.
    ///
    /// ⚠️ TUDO ANULÁVEL, e por isso: planilhas geradas ANTES desta aba existir não a têm, e uma
    /// pessoa pode devolver o arquivo sem preenchê-la. Nulo aqui significa "não perguntado", nunca
    /// "zero" — é a mesma regra do resto do módulo.
    /// </summary>
    public record FichaTecnica(
        bool? JaTemFerramenta, string? FerramentaExistente,
        int? EstacoesWindows, int? EstacoesOutras, int? Servidores, int? ServidoresExpostos,
        bool? TrataDadosDeCriancas, CoberturaOrcamento? Cobertura, int? RetencaoDias,
        bool? HospedagemDoCliente, string? Observacoes);

    /// <param name="Aproveitadas">Linhas que viraram resposta.</param>
    /// <param name="EmBranco">Linhas que a pessoa não respondeu — normal, não é erro.</param>
    /// <param name="Recusadas">Respostas escritas que não deu para entender. É o que a tela precisa dizer.</param>
    /// <param name="Ficha">O parque de máquinas e a ferramenta atual, quando a aba existir e vier preenchida.</param>
    public record Leitura(List<LinhaLida> Aproveitadas, int EmBranco, List<LinhaLida> Recusadas,
                          string? Erro, FichaTecnica? Ficha = null);

    /// <summary>
    /// Lê a planilha devolvida.
    ///
    /// ⚠️ ACHA AS COLUNAS PELO NOME, não pela posição. Uma coluna a mais inserida pelo cliente — e
    /// isso acontece — deslocaria tudo, e a importação gravaria o domínio no lugar da resposta sem
    /// nenhum erro aparecer.
    ///
    /// ⚠️ O QUE NÃO DEU PARA ENTENDER VOLTA EM `Recusadas`, nunca vira "não". Interpretar um campo
    /// ilegível como resposta negativa inventa um achado — e achado inventado dentro de um
    /// levantamento de segurança é o pior tipo de erro que este módulo pode cometer.
    /// </summary>
    public Leitura Ler(Stream arquivo)
    {
        XLWorkbook livro;
        try
        {
            livro = new XLWorkbook(arquivo);
        }
        catch (Exception)
        {
            return new Leitura([], 0, [],
                "Não foi possível abrir o arquivo. Envie a planilha .xlsx que foi baixada aqui, "
                + "sem convertê-la para outro formato.");
        }

        using (livro)
        {
            var aba = livro.Worksheets.FirstOrDefault(w => w.Name == Aba) ?? livro.Worksheets.FirstOrDefault();
            if (aba is null)
            {
                return new Leitura([], 0, [], "A planilha está vazia.");
            }

            var (linhaCab, colunas) = AcharCabecalho(aba);
            if (linhaCab == 0)
            {
                return new Leitura([], 0, [],
                    $"Não encontrei a linha de cabeçalho (a que tem \"{ColunaCodigo}\" e "
                    + $"\"{ColunaResposta}\"). O arquivo enviado parece não ser a planilha gerada aqui.");
            }

            var aproveitadas = new List<LinhaLida>();
            var recusadas = new List<LinhaLida>();
            var brancos = 0;

            var ultima = aba.LastRowUsed()?.RowNumber() ?? linhaCab;
            for (var l = linhaCab + 1; l <= ultima; l++)
            {
                var codigo = Texto(aba, l, colunas[ColunaCodigo]);
                if (string.IsNullOrWhiteSpace(codigo)) { continue; }

                var resposta = Texto(aba, l, colunas[ColunaResposta]);
                var observacao = colunas.TryGetValue(ColunaObservacao, out var colObs)
                    ? Texto(aba, l, colObs) : null;

                if (string.IsNullOrWhiteSpace(resposta) && string.IsNullOrWhiteSpace(observacao))
                {
                    brancos++;
                    continue;
                }

                var pergunta = CatalogoDeDominios.BuscarPergunta(codigo);
                if (pergunta is null)
                {
                    recusadas.Add(new LinhaLida(codigo, false, null, null, null, observacao, resposta));
                    continue;
                }

                var lida = Interpretar(pergunta, codigo, resposta, observacao);
                if (lida is null)
                {
                    recusadas.Add(new LinhaLida(codigo, true, null, null, null, observacao, resposta));
                }
                else
                {
                    aproveitadas.Add(lida);
                }
            }

            var ficha = LerFicha(livro);

            return new Leitura(aproveitadas, brancos, recusadas, null, ficha);
        }
    }

    /// <summary>
    /// Lê a aba "Parque e Serviço", pelo RÓTULO da linha — nunca pela posição, mesma regra da
    /// aba principal. Devolve nulo quando a aba não existe (planilha de antes desta versão).
    /// </summary>
    private static FichaTecnica? LerFicha(XLWorkbook livro)
    {
        var aba = livro.Worksheets.FirstOrDefault(w => w.Name == AbaFicha);
        if (aba is null) { return null; }

        var porRotulo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ultimaLinha = aba.LastRowUsed()?.RowNumber() ?? 0;
        for (var l = 1; l <= ultimaLinha; l++)
        {
            var rotulo = aba.Cell(l, 1).GetString().Trim();
            if (rotulo.Length > 0 && !porRotulo.ContainsKey(rotulo)) { porRotulo[rotulo] = l; }
        }

        string? Valor(string rotulo) =>
            porRotulo.TryGetValue(rotulo, out var l) ? Texto(aba, l, 3) : null;

        bool? SimNao(string rotulo)
        {
            var v = Valor(rotulo);
            if (v is null) { return null; }
            return v.Trim().Equals("Sim", StringComparison.OrdinalIgnoreCase) ? true
                 : v.Trim().Equals("Não", StringComparison.OrdinalIgnoreCase) ? false
                 : (bool?)null;
        }

        int? Numero(string rotulo)
        {
            var v = Valor(rotulo)?.Replace(".", "").Replace(" ", "");
            return int.TryParse(v, out var n) ? n : null;
        }

        CoberturaOrcamento? Cobertura()
        {
            var v = Valor("Cobertura de atendimento desejada");
            if (v is null) { return null; }
            if (v.Contains("24", StringComparison.Ordinal)) { return CoberturaOrcamento.VinteQuatroPorSete; }
            if (v.Contains("Estendida", StringComparison.OrdinalIgnoreCase)) { return CoberturaOrcamento.Estendida; }
            if (v.Contains("comercial", StringComparison.OrdinalIgnoreCase)) { return CoberturaOrcamento.Comercial; }
            return null;
        }

        bool? Hospedagem()
        {
            var v = Valor("Quem hospeda o servidor de segurança?");
            if (v is null) { return null; }
            return v.Contains("empresa", StringComparison.OrdinalIgnoreCase) ? true
                 : v.Contains("Nós", StringComparison.OrdinalIgnoreCase) ? false
                 : (bool?)null;
        }

        var ficha = new FichaTecnica(
            SimNao("Já usa alguma ferramenta de monitoramento de segurança (SIEM/EDR)?"),
            Valor("Qual ferramenta? (só se a resposta acima for Sim)"),
            Numero("Estações Windows"),
            Numero("Estações Linux / macOS"),
            Numero("Servidores"),
            Numero("Destes, quantos servidores respondem na internet?"),
            SimNao("Trata dados de crianças e adolescentes?"),
            Cobertura(),
            Numero("Retenção de dados desejada (dias)"),
            Hospedagem(),
            Valor("Observações"));

        // Tudo nulo é a mesma coisa que a aba não ter sido preenchida — devolve nulo, não uma
        // ficha vazia, para o chamador não criar um orçamento em branco por engano.
        var vazia = ficha.JaTemFerramenta is null && ficha.EstacoesWindows is null
                 && ficha.EstacoesOutras is null && ficha.Servidores is null
                 && ficha.ServidoresExpostos is null && ficha.TrataDadosDeCriancas is null
                 && ficha.Cobertura is null && ficha.RetencaoDias is null
                 && ficha.HospedagemDoCliente is null && string.IsNullOrWhiteSpace(ficha.Observacoes);

        return vazia ? null : ficha;
    }

    /// <summary>Traduz o que a pessoa escreveu no formato que o catálogo espera. Nulo = não entendi.</summary>
    private static LinhaLida? Interpretar(PerguntaDoDiagnostico pergunta, string codigo,
                                          string? resposta, string? observacao)
    {
        // Só observação, sem resposta: vale. É o caso de quem comenta sem se comprometer, e jogar
        // fora esse texto perderia justamente o que a conversa tinha de mais útil.
        if (string.IsNullOrWhiteSpace(resposta))
        {
            return new LinhaLida(codigo, true, null, null, null, observacao, null);
        }

        var t = resposta.Trim();

        switch (pergunta.Tipo)
        {
            case TipoDePergunta.Controle:
                var op = CodigoDe(t);
                return op is null ? null : new LinhaLida(codigo, true, op, null, null, observacao, t);

            case TipoDePergunta.Escolha:
                var escolha = pergunta.Opcoes.FirstOrDefault(o =>
                    string.Equals(o, t, StringComparison.OrdinalIgnoreCase));
                return escolha is null ? null
                    : new LinhaLida(codigo, true, escolha, null, null, observacao, t);

            case TipoDePergunta.Numero:
                // Aceita "1.200" e "1200" — quem digita num Excel em português usa o separador de milhar.
                var limpo = t.Replace(".", "").Replace(" ", "");
                return int.TryParse(limpo, out var n)
                    ? new LinhaLida(codigo, true, null, null, n, observacao, t)
                    : null;

            default:
                return new LinhaLida(codigo, true, null, t, null, observacao, t);
        }
    }

    private static (int Linha, Dictionary<string, int> Colunas) AcharCabecalho(IXLWorksheet aba)
    {
        var ultima = Math.Min(aba.LastRowUsed()?.RowNumber() ?? 1, 40);
        for (var l = 1; l <= ultima; l++)
        {
            var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var ultimaCol = Math.Min(aba.LastColumnUsed()?.ColumnNumber() ?? 1, 40);
            for (var c = 1; c <= ultimaCol; c++)
            {
                var v = aba.Cell(l, c).GetString().Trim();
                if (v.Length > 0 && !mapa.ContainsKey(v)) { mapa[v] = c; }
            }

            if (mapa.ContainsKey(ColunaCodigo) && mapa.ContainsKey(ColunaResposta))
            {
                return (l, mapa);
            }
        }
        return (0, new Dictionary<string, int>());
    }

    private static string? Texto(IXLWorksheet aba, int linha, int coluna)
    {
        var v = aba.Cell(linha, coluna).GetString().Trim();
        return v.Length == 0 ? null : v;
    }
}
