using OktaIA.Web.Models;
using OktaIA.Web.Services.Diagnostico;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

/// <summary>
/// O relatório do diagnóstico como documento — o que fica com o cliente depois da reunião.
///
/// Não confundir com os outros dois PDFs do projeto:
///   • PropostaComercialPdfService — documento de VENDA. Traz o diagnóstico resumido dentro da
///     seção 02, escolhendo o que sustenta a proposta.
///   • RelatorioPdfService — relatório do SCANNER, que mede a superfície externa.
///   • este — o levantamento inteiro, incluindo o que não favorece ninguém: a matriz completa de
///     controles, os riscos todos e a origem de cada informação.
///
/// A separação importa porque os documentos têm leitores diferentes. A proposta vai para quem
/// assina; este vai para quem vai ter que executar, e essa pessoa precisa do detalhe — inclusive
/// dos itens marcados "não sei", que numa proposta seriam ruído e aqui são o mapa do que falta
/// levantar.
///
/// ⚠️ A regra do módulo vale aqui com mais força que na tela: declarado ≠ evidenciado ≠ validado.
/// Um PDF sai da sala, circula por e-mail e é lido meses depois sem ninguém por perto para
/// explicar. Por isso a ressalva de origem aparece na capa, no sumário e em cada linha da matriz —
/// não como rodapé miúdo.
/// </summary>
public class DiagnosticoPdfService
{
    private readonly byte[] _iconMono;

    static DiagnosticoPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DiagnosticoPdfService(IWebHostEnvironment env)
    {
        _iconMono = File.ReadAllBytes(Path.Combine(env.WebRootPath, "img", "brand", "simbolo-mono.png"));
    }

    private const string BrandBg = "#0B1220";
    private const string BrandBlue = "#4D9BFF";
    private const string BrandGreen = "#00E0A4";
    private const string BrandRed = "#FF3B5C";
    private const string BrandYellow = "#F5D547";
    private const string TextDark = "#1C2836";
    private const string TextMuted = "#7A8FAB";
    private const string TextMuted2 = "#5A7191";
    private const string Fundo = "#F6F8FB";
    private const string Linha = "#E3E9F2";

    public byte[] Gerar(
        Models.Diagnostico diagnostico,
        string empresaNome,
        ResultadoDoDiagnostico r,
        List<CamadaDaArquitetura> mapa,
        List<DiagnosticoRisco> riscos,
        DiagnosticoAnalise? analise,
        string? narrativa)
    {
        var geradoEm = DateTimeOffset.Now;
        var referencia = $"DIAG-{geradoEm:yyyy}-{Slug(empresaNome)}-{diagnostico.Id:000}";

        var declaradas = r.PorOrigem.GetValueOrDefault(OrigemDaInformacao.Declarado);
        var totalOrigens = r.PorOrigem.Values.Sum();
        var tudoDeclarado = totalOrigens > 0 && declaradas == totalOrigens;

        var documento = Document.Create(container =>
        {
            Capa(container, diagnostico, empresaNome, r, geradoEm, referencia, tudoDeclarado, totalOrigens);

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(38);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily(Fonts.Calibri).FontColor(TextDark));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Text("L'OKTA IA").FontSize(7.5f).Bold().FontColor(TextMuted2).LetterSpacing(0.1f);
                    row.AutoItem().Text($"DIAGNÓSTICO · {empresaNome} · {geradoEm:MMM yyyy}".ToUpperInvariant())
                        .FontSize(7.5f).FontColor(TextMuted2).LetterSpacing(0.06f);
                });

                page.Footer().PaddingTop(6).Column(col =>
                {
                    col.Item().LineHorizontal(0.7f).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text("DOCUMENTO CONFIDENCIAL — USO RESTRITO").FontSize(6.5f).FontColor(TextMuted2);
                        row.AutoItem().Text(x =>
                        {
                            x.Span($"{referencia} · ").FontSize(6.5f).FontColor(TextMuted2);
                            x.CurrentPageNumber().FontSize(6.5f).FontColor(TextMuted2);
                            x.Span(" / ").FontSize(6.5f).FontColor(TextMuted2);
                            x.TotalPages().FontSize(6.5f).FontColor(TextMuted2);
                        });
                    });
                });

                page.Content().PaddingTop(18).Column(body =>
                {
                    body.Spacing(4);

                    Sumario(body, empresaNome, r, declaradas, totalOrigens, tudoDeclarado);
                    Ambiente(body, mapa, narrativa);
                    PorDominio(body, r);
                    Riscos(body, riscos);
                    Leitura(body, analise);
                    Matriz(body, r);
                    ComoLer(body, r);
                });
            });
        });

        return documento.GeneratePdf();
    }

    // ── Capa ─────────────────────────────────────────────────────────────────

    private void Capa(IDocumentContainer container, Models.Diagnostico d, string empresaNome,
        ResultadoDoDiagnostico r, DateTimeOffset geradoEm, string referencia,
        bool tudoDeclarado, int totalOrigens)
    {
        container.Page(capa =>
        {
            capa.Size(PageSizes.A4);
            capa.Margin(0);
            capa.DefaultTextStyle(x => x.FontFamily(Fonts.Calibri).FontColor(Colors.White));
            capa.PageColor(BrandBg);

            capa.Content().Padding(46).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.AutoItem().Width(30).Height(30).Background(BrandBlue).AlignMiddle().AlignCenter()
                        .Padding(6).Image(_iconMono);
                    row.RelativeItem().PaddingLeft(10).Column(brand =>
                    {
                        brand.Item().Text(t =>
                        {
                            t.Span("L'okta ").FontSize(15).Bold().FontColor(Colors.White);
                            t.Span("IA").FontSize(15).Bold().FontColor(BrandBlue);
                        });
                        brand.Item().Text("SEGURANÇA CONTÍNUA").FontSize(7).FontColor(TextMuted).LetterSpacing(0.15f);
                    });
                });

                col.Item().PaddingTop(64).Text("DIAGNÓSTICO DE SEGURANÇA").FontSize(10).Bold()
                    .FontColor(BrandBlue).LetterSpacing(0.1f);
                col.Item().PaddingTop(10).Text(empresaNome).FontSize(30).Bold().LineHeight(1.15f);
                col.Item().PaddingTop(6).Text(d.Titulo).FontSize(13).FontColor("#C4D3E6");

                // Os dois números que resumem o levantamento, lado a lado de propósito: é a
                // diferença entre eles que conta a história, não cada um isolado.
                col.Item().PaddingTop(34).Row(row =>
                {
                    void Numero(string valor, string sufixo, string label, string cor)
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Row(rr =>
                            {
                                rr.AutoItem().Text(valor).FontSize(34).Bold().FontColor(cor);
                                rr.AutoItem().PaddingTop(16).PaddingLeft(2).Text(sufixo).FontSize(11).FontColor(TextMuted);
                            });
                            c.Item().PaddingTop(2).Text(label).FontSize(7.5f).FontColor(TextMuted2).LetterSpacing(0.1f);
                        });
                    }

                    Numero(r.Cobertura.ToString(), "%", "COBERTURA",
                        r.Cobertura >= 70 ? BrandGreen : r.Cobertura >= 40 ? BrandYellow : BrandRed);
                    Numero(r.Maturidade?.ToString("0.0") ?? "—", "/5", "MATURIDADE", Colors.White);
                    Numero(r.UsoDoInvestimento?.ToString() ?? "—", "%", "USO DO INVESTIMENTO", Colors.White);
                    Numero(r.Completude.ToString(), "%", "PREENCHIDO", Colors.White);
                });

                // A ressalva na CAPA, não no rodapé: um PDF circula por e-mail e é lido meses
                // depois sem ninguém por perto para explicar de onde vieram os números.
                if (totalOrigens > 0)
                {
                    var declaradas = r.PorOrigem.GetValueOrDefault(OrigemDaInformacao.Declarado);
                    var ressalva = tudoDeclarado
                        // "da {empresa}" assume gênero: com "Grupo Vector" sai "da Grupo Vector".
                        // O nome do cliente é dado livre, então nenhum artigo pode ser presumido.
                        ? $"As {totalOrigens} respostas deste levantamento foram informadas pela equipe de {empresaNome} "
                          + "e não passaram por verificação técnica independente. Este documento registra o que a empresa "
                          + "relata sobre o próprio ambiente — é um levantamento, não uma medição."
                        : $"Das {totalOrigens} respostas deste levantamento, {declaradas} foram declaradas pela equipe e as "
                          + "demais têm evidência anexada ou verificação técnica. Cada item da matriz de controles indica "
                          + "a própria origem.";

                    col.Item().PaddingTop(26).Background("#132133").Padding(14)
                        .Text(ressalva).FontSize(9).FontColor("#C4D3E6").LineHeight(1.6f);
                }

                col.Item().PaddingTop(30).LineHorizontal(1).LineColor("#22334D");
                col.Item().PaddingTop(18).Row(row =>
                {
                    void Meta(string label, string valor)
                    {
                        // A folga à direita não é estética: o nome do respondente com cargo quebra
                        // em duas linhas e, sem ela, encosta na coluna seguinte — na conferência
                        // apareceu como "Dados de demonstração (não06/08/2026".
                        row.RelativeItem().PaddingRight(12).Column(c =>
                        {
                            c.Item().Text(label).FontSize(7.5f).FontColor(TextMuted2).LetterSpacing(0.1f);
                            c.Item().PaddingTop(3).Text(valor).FontSize(10.5f).FontColor(Colors.White).LineHeight(1.3f);
                        });
                    }

                    Meta("RESPONDENTE", d.Respondente is { Length: > 0 } resp
                        ? (d.RespondenteCargo is { Length: > 0 } cargo ? $"{resp} ({cargo})" : resp)
                        : "não registrado");
                    Meta("LEVANTAMENTO", d.RealizadoEm?.ToString("dd/MM/yyyy") ?? "não informado");
                    Meta("EMISSÃO", geradoEm.ToString("dd/MM/yyyy"));
                    Meta("REFERÊNCIA", referencia);
                });
            });

            capa.Footer().Background(BrandBg).PaddingHorizontal(46).PaddingVertical(14).Row(row =>
            {
                row.RelativeItem().Text("DOCUMENTO CONFIDENCIAL — USO RESTRITO").FontSize(7).FontColor(TextMuted2);
                row.AutoItem().Text("L'OKTA IA").FontSize(7).FontColor(TextMuted2);
            });
        });
    }

    // ── Seções ───────────────────────────────────────────────────────────────

    private static void Sumario(ColumnDescriptor body, string empresaNome, ResultadoDoDiagnostico r,
        int declaradas, int totalOrigens, bool tudoDeclarado)
    {
        Titulo(body, "01", "Sumário");

        body.Item().PaddingTop(6).Text(
            $"Este documento registra o diagnóstico de segurança conduzido junto à equipe de {empresaNome}, cobrindo "
            + $"{CatalogoDeDominios.Todos.Count} domínios. Ele responde a duas perguntas separadas: quais controles "
            + "EXISTEM (cobertura) e quão bem os que existem são OPERADOS (maturidade).")
            .FontSize(9.5f).LineHeight(1.6f);

        // Este é o parágrafo que justifica o produto. Cobertura alta com maturidade baixa é o
        // achado mais comum e o mais mal interpretado: parece que está tudo bem porque as
        // ferramentas estão lá.
        if (r.Maturidade is { } mat && r.Cobertura >= 60 && mat < 3m)
        {
            body.Item().PaddingTop(10).Background("#EEF4FF").Padding(12).Text(
                $"{empresaNome} tem a maior parte dos controles esperados para o seu porte — a cobertura de "
                + $"{r.Cobertura}% mostra isso. A maturidade de {mat:0.0} indica outra coisa: essas ferramentas não "
                + "estão sendo operadas. Licença vencida, alerta que ninguém lê, backup que nunca foi restaurado "
                + "para teste. O problema não é falta de tecnologia comprada.")
                .FontSize(9.5f).LineHeight(1.6f);
        }
        else if (r.Maturidade is null)
        {
            body.Item().PaddingTop(10).Background(Fundo).Padding(12).Text(
                "A maturidade aparece em branco, e isso não é zero: significa que ainda não há controle implantado "
                + "o suficiente para julgar como ele é gerenciado. Primeiro é preciso ter, depois se avalia o quanto "
                + "se opera bem.")
                .FontSize(9.5f).LineHeight(1.6f);
        }

        body.Item().PaddingTop(10).Row(row =>
        {
            void Caixa(string valor, string sufixo, string label, string descricao, string cor)
            {
                row.RelativeItem().PaddingRight(8).Background(Fundo).Padding(11).Column(c =>
                {
                    c.Item().Row(rr =>
                    {
                        rr.AutoItem().Text(valor).FontSize(20).Bold().FontColor(cor);
                        rr.AutoItem().PaddingTop(9).PaddingLeft(1).Text(sufixo).FontSize(9).FontColor(TextMuted2);
                    });
                    c.Item().PaddingTop(3).Text(label).FontSize(7.5f).Bold().FontColor(TextMuted2).LetterSpacing(0.06f);
                    c.Item().PaddingTop(2).Text(descricao).FontSize(8).FontColor(TextMuted).LineHeight(1.35f);
                });
            }

            Caixa(r.Cobertura.ToString(), "%", "COBERTURA", "dos controles esperados existem",
                RotulosDoDiagnostico.ParaImpressao(r.Cobertura >= 70 ? BrandGreen : r.Cobertura >= 40 ? BrandYellow : BrandRed));
            Caixa(r.Maturidade?.ToString("0.0") ?? "—", "/5", "MATURIDADE", "quão bem gerenciado é o que existe", BrandBg);
            Caixa(r.UsoDoInvestimento?.ToString() ?? "—", "%", "USO DO INVESTIMENTO", "do que já foi pago está em uso", BrandBg);
            Caixa(r.Completude.ToString(), "%", "PREENCHIDO", "das perguntas aplicáveis", BrandBg);
        });

        if (totalOrigens > 0)
        {
            body.Item().PaddingTop(10).BorderLeft(2)
                .BorderColor(tudoDeclarado ? "#B54708" : BrandBlue)
                .PaddingLeft(10).PaddingVertical(4).Column(c =>
                {
                    c.Item().Text("De onde vêm estes números").FontSize(8).Bold().FontColor(TextMuted2).LetterSpacing(0.06f);
                    c.Item().PaddingTop(3).Text(string.Join(" · ", r.PorOrigem
                        .OrderByDescending(x => x.Value)
                        .Select(x => $"{x.Value} {RotulosDoDiagnostico.Origem(x.Key)}")))
                        .FontSize(9).FontColor(TextDark).LineHeight(1.55f);
                });
        }
    }

    private static void Ambiente(ColumnDescriptor body, List<CamadaDaArquitetura> mapa, string? narrativa)
    {
        if (mapa.Count == 0) { return; }

        Titulo(body, "02", "O ambiente, camada a camada");

        body.Item().PaddingTop(6).Text(
            "Um percentual não diz se o buraco é o backup ou a borda. O desenho diz.")
            .FontSize(9.5f).LineHeight(1.6f);

        // O MESMO SVG da tela, no tema claro. Dois desenhos por caminhos diferentes divergiriam na
        // primeira alteração, e o cliente veria uma coisa na reunião e outra no documento.
        body.Item().PaddingTop(10).Svg(DiagramaDeRede.Gerar(mapa, TemaDoDiagrama.Claro));

        body.Item().PaddingTop(6).Row(row =>
        {
            foreach (var estado in new[]
            {
                EstadoDaCamada.Protegido, EstadoDaCamada.Parcial,
                EstadoDaCamada.Descoberto, EstadoDaCamada.NaoAvaliado,
            })
            {
                row.AutoItem().PaddingRight(14).Row(r =>
                {
                    r.AutoItem().PaddingTop(2).Width(7).Height(7).Background(MapaDaArquitetura.Cor(estado));
                    r.AutoItem().PaddingLeft(4).Text(MapaDaArquitetura.Rotulo(estado).ToLowerInvariant())
                        .FontSize(7.5f).FontColor(TextMuted);
                });
            }
        });

        if (narrativa is { Length: > 0 })
        {
            body.Item().PaddingTop(10).Background("#EEF4FF").Padding(12)
                .Text(narrativa).FontSize(9.5f).LineHeight(1.65f);
        }

        var comLacuna = mapa.Where(c => c.MaiorLacuna is not null).ToList();
        if (comLacuna.Count > 0)
        {
            body.Item().PaddingTop(12).Text("A maior lacuna de cada camada")
                .FontSize(9).Bold().FontColor(BrandBg);

            foreach (var camada in comLacuna)
            {
                body.Item().PaddingTop(6).Row(row =>
                {
                    row.ConstantItem(74).PaddingTop(1).Text(camada.Nome.ToUpperInvariant())
                        .FontSize(6.5f).Bold().LetterSpacing(0.05f)
                        .FontColor(RotulosDoDiagnostico.ParaImpressao(MapaDaArquitetura.Cor(camada.Estado)));
                    row.RelativeItem().Text(camada.MaiorLacuna!).FontSize(9).FontColor(TextDark).LineHeight(1.5f);
                });
            }
        }

        var naoAvaliadas = mapa.Count(c => c.Estado == EstadoDaCamada.NaoAvaliado);
        if (naoAvaliadas > 0)
        {
            // Dito na cara, e no plural certo — "1 camadas" num documento de conformidade
            // é o tipo de detalhe que faz o leitor duvidar do resto.
            body.Item().PaddingTop(10).Text(naoAvaliadas == 1
                ? "1 camada não foi avaliada neste levantamento. Isso não significa que esteja bem — significa que "
                  + "ainda não olhamos."
                : $"{naoAvaliadas} camadas não foram avaliadas neste levantamento. Isso não significa que estejam "
                  + "bem — significa que ainda não olhamos.")
                .FontSize(8.5f).FontColor(TextMuted).Italic();
        }
    }

    private static void PorDominio(ColumnDescriptor body, ResultadoDoDiagnostico r)
    {
        Titulo(body, "03", "Cobertura por domínio");

        foreach (var dom in r.Dominios)
        {
            body.Item().PaddingTop(7).Row(row =>
            {
                row.ConstantItem(148).Text(dom.Dominio.Nome).FontSize(9).FontColor(TextDark);

                row.RelativeItem().PaddingRight(10).PaddingTop(4).Height(5)
                    .Background(Linha).Row(barra =>
                    {
                        var cor = dom.Cobertura >= 70 ? "#00755A" : dom.Cobertura >= 40 ? "#8A6D00" : BrandRed;
                        if (dom.Cobertura > 0)
                        {
                            barra.RelativeItem(dom.Cobertura).Background(cor);
                        }
                        if (dom.Cobertura < 100)
                        {
                            barra.RelativeItem(100 - dom.Cobertura);
                        }
                    });

                row.ConstantItem(34).AlignRight().Text($"{dom.Cobertura}%")
                    .FontSize(8.5f).FontColor(TextDark).FontFamily(Fonts.CourierNew);
                row.ConstantItem(58).AlignRight().Text($"mat {dom.Maturidade?.ToString("0.0") ?? "—"}")
                    .FontSize(8).FontColor(TextMuted);
                row.ConstantItem(40).AlignRight().Text($"{dom.PerguntasRespondidas}/{dom.PerguntasVisiveis}")
                    .FontSize(7.5f).FontColor(TextMuted2).FontFamily(Fonts.CourierNew);
            });
        }
    }

    private static void Riscos(ColumnDescriptor body, List<DiagnosticoRisco> riscos)
    {
        Titulo(body, "04", "Riscos priorizados");

        if (riscos.Count == 0)
        {
            body.Item().PaddingTop(6).Text("Nenhuma lacuna identificada com as respostas atuais.")
                .FontSize(9.5f).FontColor(TextMuted);
            return;
        }

        body.Item().PaddingTop(6).Text(
            $"{riscos.Count} achados, ordenados por gravidade. Cada um nasce de uma resposta específica — "
            + "nada aqui é inferido sem dado.")
            .FontSize(9).FontColor(TextMuted).LineHeight(1.5f);

        foreach (var risco in riscos)
        {
            var cor = RotulosDoDiagnostico.ParaImpressao(RotulosDoDiagnostico.CorDaGravidade(risco.Gravidade));

            // Cada risco inteiro numa página só: um achado partido ao meio, com a consequência numa
            // folha e a recomendação na outra, é lido como duas coisas soltas.
            body.Item().ShowEntire().PaddingTop(9).Row(row =>
            {
                row.ConstantItem(24).PaddingTop(1).Text(risco.Prioridade.ToString("00"))
                    .FontSize(8).FontColor(TextMuted2).FontFamily(Fonts.CourierNew);
                row.ConstantItem(50).PaddingTop(1).Text(risco.Gravidade.ToString().ToUpperInvariant())
                    .FontSize(6.5f).Bold().FontColor(cor).LetterSpacing(0.05f);

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(risco.Titulo).FontSize(9.5f).Bold().FontColor(TextDark);

                    if (risco.SeNaoTratar is { Length: > 0 } consequencia)
                    {
                        c.Item().PaddingTop(2).Text(consequencia).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.5f);
                    }
                    if (risco.Recomendacao is { Length: > 0 } recomendacao)
                    {
                        c.Item().PaddingTop(2).Text($"→ {recomendacao}").FontSize(8.5f)
                            .FontColor("#00755A").LineHeight(1.5f);
                    }
                    c.Item().PaddingTop(2).Text(RotulosDoDiagnostico.Origem(risco.Origem))
                        .FontSize(7).FontColor(TextMuted2);
                });
            });
        }
    }

    private static void Leitura(ColumnDescriptor body, DiagnosticoAnalise? analise)
    {
        // Sem leitura de IA o documento não perde nada essencial — os números, o mapa e os riscos
        // não dependem do modelo. Por isso a seção simplesmente não existe em vez de aparecer
        // vazia pedindo desculpas.
        if (analise is null || analise.Resultado != ResultadoAnalise.Sucesso) { return; }
        if (analise.ResumoExecutivo is not { Length: > 0 }) { return; }

        Titulo(body, "05", "Leitura do diagnóstico");

        body.Item().PaddingTop(6).Text(analise.ResumoExecutivo).FontSize(9.5f).LineHeight(1.7f);

        void Bloco(string rotulo, string? texto, string corRotulo)
        {
            if (texto is not { Length: > 0 }) { return; }
            body.Item().PaddingTop(10).Text(rotulo).FontSize(7.5f).Bold()
                .FontColor(corRotulo).LetterSpacing(0.06f);
            body.Item().PaddingTop(3).Text(texto).FontSize(9).FontColor(TextDark).LineHeight(1.65f);
        }

        Bloco("O QUE JÁ FOI PAGO", analise.LeituraDoInvestimento, TextMuted2);
        Bloco("CONTRADIÇÕES ENTRE RESPOSTAS", analise.Inconsistencias, "#8A6D00");
        Bloco("LEITURA TÉCNICA", analise.ResumoTecnico, TextMuted2);
        Bloco("O QUE FALTOU PERGUNTAR", analise.PerguntasAdicionais, TextMuted2);

        // Quem assina o texto precisa estar no documento. Análise de máquina apresentada sem
        // procedência vira "o relatório disse", e ninguém consegue conferir depois.
        body.Item().PaddingTop(10).Text(
            $"Trecho gerado por {analise.Modelo ?? "modelo de linguagem"} em "
            + $"{analise.GeradaEm.ToLocalTime():dd/MM/yyyy HH:mm}, a partir das respostas deste levantamento. "
            + "Os números, o mapa e os riscos das seções anteriores são calculados e não dependem do modelo.")
            .FontSize(7.5f).FontColor(TextMuted2).Italic().LineHeight(1.45f);
    }

    private static void Matriz(ColumnDescriptor body, ResultadoDoDiagnostico r)
    {
        var comMatriz = r.Dominios.Where(x => x.Matriz.Count > 0).ToList();
        if (comMatriz.Count == 0) { return; }

        body.Item().PageBreak();
        Titulo(body, "06", "Matriz de controles");

        body.Item().PaddingTop(6).Text(
            $"O levantamento inteiro, pergunta a pergunta: {r.ControlesAusentes} controles ausentes e "
            + $"{r.ControlesParciais} parciais. A coluna da direita diz de onde veio cada informação — é o que "
            + "permite conferir este documento no futuro, em vez de ter que confiar nele.")
            .FontSize(9).FontColor(TextMuted).LineHeight(1.55f);

        foreach (var dom in comMatriz)
        {
            body.Item().PaddingTop(12).Text(dom.Dominio.Nome.ToUpperInvariant())
                .FontSize(7.5f).Bold().FontColor(BrandBg).LetterSpacing(0.08f);
            body.Item().PaddingTop(3).LineHorizontal(0.7f).LineColor(Linha);

            foreach (var linha in dom.Matriz)
            {
                body.Item().PaddingTop(4).Row(row =>
                {
                    row.ConstantItem(48).PaddingTop(1).Text(RotulosDoDiagnostico.Situacao(linha.Situacao))
                        .FontSize(6.5f).Bold().LetterSpacing(0.05f)
                        .FontColor(RotulosDoDiagnostico.ParaImpressao(RotulosDoDiagnostico.CorDaSituacao(linha.Situacao)));
                    row.RelativeItem().Text(linha.Pergunta.Texto).FontSize(8.5f)
                        .FontColor(TextDark).LineHeight(1.4f);
                    row.ConstantItem(98).AlignRight().Text(RotulosDoDiagnostico.Origem(linha.Origem))
                        .FontSize(7).FontColor(TextMuted2);
                });
            }
        }
    }

    private static void ComoLer(ColumnDescriptor body, ResultadoDoDiagnostico r)
    {
        body.Item().PaddingTop(18).Background(Fundo).Padding(14).Column(c =>
        {
            c.Item().Text("Como ler este documento").FontSize(9).Bold().FontColor(BrandBg);

            void Item(string termo, string texto)
            {
                c.Item().PaddingTop(6).Text(t =>
                {
                    t.Span($"{termo} — ").FontSize(8.5f).Bold().FontColor(TextDark);
                    t.Span(texto).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.5f);
                });
            }

            Item("Cobertura",
                "quantos dos controles esperados para um ambiente deste porte existem. Diz o que há, não o que funciona.");
            Item("Maturidade",
                "quão bem é operado o que existe. Em branco não é zero: é a ausência de base para julgar.");
            Item("Uso do investimento",
                "quanto do que a empresa já paga está efetivamente em uso. Ferramenta comprada e não operada aparece aqui.");
            Item("Declarado, evidenciado, validado",
                "declarado é o que a equipe informou; evidenciado tem documento anexado; validado foi verificado "
                + "tecnicamente por nós. As três coisas convivem no mesmo relatório e não devem ser lidas como iguais.");
            Item("Não avaliado",
                "pergunta sem resposta, geralmente porque ninguém na sala sabia. Não é sinônimo de problema — nem de "
                + "ausência dele.");

            if (r.Completude < 100)
            {
                c.Item().PaddingTop(8).Text(
                    $"Este levantamento está {r.Completude}% preenchido. As perguntas em aberto são o roteiro natural "
                    + "da próxima conversa.")
                    .FontSize(8.5f).FontColor(TextDark).LineHeight(1.5f);
            }
        });
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    private static void Titulo(ColumnDescriptor body, string numero, string texto)
    {
        body.Item().PaddingTop(20).Row(row =>
        {
            row.AutoItem().Text(numero).FontSize(8).Bold().FontColor(BrandBlue).LetterSpacing(0.1f);
            row.RelativeItem().PaddingLeft(9).Text(texto).FontSize(13).Bold().FontColor(BrandBg);
        });
        body.Item().PaddingTop(5).LineHorizontal(1).LineColor(Linha);
    }

    private static string Slug(string nome)
    {
        var limpo = new string(nome.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
        var partes = limpo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? "CLIENTE" : string.Join("", partes.Take(2)).ToUpperInvariant();
    }
}
