using OktaIA.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

/// <summary>
/// A proposta comercial EXCLUSIVA para LGPD — para quando o diagnóstico do cliente tocou um
/// framework só, e o pedido de venda é "segurança para enquadrar na LGPD", nada além disso.
///
/// POR QUE ESTE DOCUMENTO EXISTE, e não `PropostaComercialPdfService`: aquele é a proposta da
/// PLATAFORMA INTEIRA — 15 módulos em 4 camadas, ROI, comparativo, cronograma de 90 dias. O
/// próprio arquivo dele diz: "só a seção de diagnóstico usa dado real do tenant — o resto é
/// narrativa comercial fixa, igual em qualquer proposta emitida". Isso é exatamente o problema
/// quando a venda é estreita: mandar ao parceiro (ou ao cliente dele) um documento cheio de texto
/// sobre scanner de vulnerabilidade, superfície de ataque e módulos que ninguém contratou é
/// prometer o que não está em cima da mesa — e pior, o cliente lê aquilo comparando com as 8
/// perguntas que ele mesmo respondeu, e a conta não fecha.
///
/// ⚠️ REGRA DE OURO: TUDO AQUI VEM DO DIAGNÓSTICO. Nenhum parágrafo cita módulo, ferramenta ou
/// número que não tenha nascido de uma resposta do questionário (ou, quando a empresa já tem
/// Wazuh, do que foi de fato medido — ver <see cref="GerarMedido"/>). Sem scanner, sem
/// vulnerabilidade de porta, sem ROI de plataforma. É um documento pequeno de propósito.
///
/// ⚠️ MEDIDO VENCE DECLARADO. Se a empresa já está sendo monitorada, a proposta não pode ignorar
/// isso e seguir citando "o cliente declarou" — citaria uma opinião quando existe um fato. Quem
/// decide qual dos dois usar é <see cref="Pages.Admin.DiagnosticoResultadoModel"/>, perguntando a
/// `PosturaLgpd` se há conector antes de escolher o método.
/// </summary>
public class PropostaLgpdPdfService
{
    private readonly byte[] _iconMonoBranco;

    static PropostaLgpdPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PropostaLgpdPdfService(IWebHostEnvironment env)
    {
        _iconMonoBranco = File.ReadAllBytes(Path.Combine(env.WebRootPath, "img", "brand", "simbolo-mono-branco.png"));
    }

    private const string Muted = "#5A7191";
    private const string Text = "#1C2836";
    private const string Azul = "#0B45DD";
    private const string Fundo = "#0B1220";

    /// <summary>O preço, quando já existe um orçamento para esta empresa. Nulo = ainda não orçado.</summary>
    /// <param name="Itens">
    /// O detalhamento, item a item — gravado no orçamento no momento do cálculo, nunca
    /// recalculado aqui. Nulo/vazio = orçamento de antes desta versão, sem detalhamento salvo.
    /// </param>
    /// <param name="Maquinas">Para o fluxograma. Zero quando não há orçamento, e o desenho some.</param>
    /// <param name="HospedagemDoCliente">Idem — decide o texto da caixa do servidor no fluxograma.</param>
    public record Preco(string Numero, decimal ValorImplantacao, decimal ValorMensal,
        List<CalculadoraDeOrcamento.ItemDeCusto>? Itens = null,
        int Maquinas = 0, bool HospedagemDoCliente = false);

    // ── Declarado: a partir das respostas da planilha ───────────────────────────────────────

    /// <summary>
    /// A proposta quando a empresa NÃO tem monitoramento implantado — o que se sabe vem do que o
    /// cliente respondeu na planilha, e nada mais.
    /// </summary>
    public byte[] GerarDeclarado(string empresa, string? cnpj, string? parceiroNome,
        Models.Diagnostico diagnostico, List<DiagnosticoRisco> riscos, Preco? preco,
        bool trataDadosDeCriancas = false, bool jaTemFerramenta = false, string? ferramentaExistente = null)
    {
        var totalRespondidas = diagnostico.Respostas.Count;
        var quando = diagnostico.RealizadoEm
            ?? DateOnly.FromDateTime((diagnostico.ConcluidoEm ?? diagnostico.CriadoEm).LocalDateTime);

        return Montar(empresa, cnpj, parceiroNome, preco, trataDadosDeCriancas, jaTemFerramenta, ferramentaExistente,
            corpo =>
            {
                // ⚠️ A FRASE DE ABERTURA CITA A FONTE, e não afirma sobre a empresa. "A Empresa X
                // não tem DPO" seria uma afirmação nossa; "a Empresa X respondeu que não tem" é o
                // que de fato aconteceu — e é essa a frase que sobra depois que um advogado lê.
                corpo.Item().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(9.5f).LineHeight(1.5f));
                    t.Span("Levantamento conduzido");
                    if (diagnostico.Respondente is { Length: > 0 } resp)
                    {
                        t.Span(" com ");
                        t.Span(resp).Bold();
                        if (diagnostico.RespondenteCargo is { Length: > 0 } cargo) { t.Span($" ({cargo})"); }
                    }
                    t.Span($", em {quando:dd/MM/yyyy}, a partir de ");
                    t.Span($"{totalRespondidas} pergunta{(totalRespondidas == 1 ? "" : "s")} respondida{(totalRespondidas == 1 ? "" : "s")}")
                        .Bold();
                    t.Span(" sobre os controles exigidos pela LGPD. ");
                    t.Span("As respostas foram declaradas pela empresa, sem verificação técnica nossa.")
                        .FontColor(Muted);
                });

                DesenharAchados(corpo, riscos,
                    vazio: "A empresa respondeu sem apontar lacuna em nenhum dos controles perguntados. "
                         + "Isto não substitui a medição contínua — ver a proposta de monitoramento.");

                DesenharMapaDoAmbiente(corpo, diagnostico);
            });
    }

    /// <summary>
    /// O ambiente, camada a camada — Internet → Firewall → Rede → Servidores → Endpoints →
    /// Nuvem → Aplicações → Identidades → Backup → SIEM/SOC.
    ///
    /// ⚠️ O MESMO GERADOR que a tela e a proposta da plataforma inteira usam
    /// (`MapaDaArquitetura.Montar` + `DiagramaDeRede.Gerar`), e pelo mesmo motivo: dois desenhos
    /// do mesmo diagnóstico, por caminhos de código diferentes, divergiriam no primeiro ajuste — e
    /// o cliente veria uma coisa na reunião e outra no papel.
    ///
    /// ⚠️ AS 8 PERGUNTAS DE UMA PLANILHA DE FRAMEWORK NÃO COBREM O DESENHO INTEIRO. A maioria das
    /// camadas sai como "não avaliada" — cinza, não vermelha. É a mesma regra do resto do módulo:
    /// camada sem pergunta é camada que não se olhou, nunca uma camada com problema.
    /// </summary>
    private static void DesenharMapaDoAmbiente(ColumnDescriptor corpo, Models.Diagnostico diagnostico)
    {
        var mapa = Services.Diagnostico.MapaDaArquitetura.Montar(diagnostico);
        if (mapa.Count == 0) { return; }

        corpo.Item().PaddingTop(16).Text("O AMBIENTE, CAMADA A CAMADA").FontSize(9).Bold().FontColor(Muted);
        corpo.Item().PaddingTop(3).Text(
            "O que este levantamento tocou, e o que ficou de fora — não avaliado é cinza, nunca vermelho.")
            .FontSize(8.5f).FontColor(Muted);

        corpo.Item().PaddingTop(8)
            .Svg(Services.Diagnostico.DiagramaDeRede.Gerar(mapa, Services.Diagnostico.TemaDoDiagrama.Claro));

        corpo.Item().PaddingTop(6).Row(row =>
        {
            foreach (var estado in new[]
            {
                Services.Diagnostico.EstadoDaCamada.Protegido,
                Services.Diagnostico.EstadoDaCamada.Parcial,
                Services.Diagnostico.EstadoDaCamada.Descoberto,
                Services.Diagnostico.EstadoDaCamada.NaoAvaliado,
            })
            {
                var cor = Services.Diagnostico.MapaDaArquitetura.Cor(estado);
                row.AutoItem().PaddingRight(14).Row(r =>
                {
                    r.AutoItem().PaddingTop(2).Width(7).Height(7).Background(cor);
                    r.AutoItem().PaddingLeft(4)
                        .Text(Services.Diagnostico.MapaDaArquitetura.Rotulo(estado).ToLowerInvariant())
                        .FontSize(7.5f).FontColor(Muted);
                });
            }
        });
    }

    // ── Medido: a partir do que o Wazuh já mostrou ──────────────────────────────────────────

    /// <summary>
    /// A proposta quando a empresa JÁ tem monitoramento — o que se sabe vem do que a plataforma
    /// mediu (conector, alertas, benchmark), não do que alguém respondeu numa planilha.
    /// </summary>
    public byte[] GerarMedido(string empresa, string? cnpj, string? parceiroNome,
        PosturaLgpd.Resultado r, Preco? preco, bool trataDadosDeCriancas = false)
    {
        // ⚠️ AQUI, "JÁ TEM FERRAMENTA" É SEMPRE VERDADE — é o próprio conector medido que prova
        // isso. Diferente do caso declarado, onde a resposta vem de uma pergunta no orçamento.
        return Montar(empresa, cnpj, parceiroNome, preco, trataDadosDeCriancas,
            jaTemFerramenta: true, ferramentaExistente: r.Conector, corpo =>
        {
            corpo.Item().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(9.5f).LineHeight(1.5f));
                t.Span("Monitoramento em operação");
                if (r.UltimoSync is { } s)
                {
                    t.Span($" — último dado recebido em {s.ToLocalTime():dd/MM/yyyy 'às' HH:mm}");
                }
                t.Span($", com {r.MaquinasVistas} máquina(s) reportando e {r.AlertasTotal:N0} alerta(s) no histórico. ");
                t.Span("Os números abaixo são medidos pela plataforma, não declarados pela empresa.")
                    .FontColor(Muted);
            });

            corpo.Item().PaddingTop(14).Text("O QUE FOI MEDIDO, ARTIGO POR ARTIGO")
                .FontSize(9).Bold().FontColor(Muted);

            foreach (var q in r.Requisitos.Where(x => x.Como != PosturaLgpd.Situacao.ForaDoEscopo))
            {
                var cor = PosturaLgpd.Cor(q.Como);
                corpo.Item().PaddingTop(8).BorderLeft(2).BorderColor(cor).PaddingLeft(10).Column(c =>
                {
                    c.Item().Row(row =>
                    {
                        row.AutoItem().Text(q.Artigo).FontSize(9).Bold().FontColor(Azul);
                        row.RelativeItem().PaddingLeft(8).Text(q.Titulo).FontSize(10.5f).Bold();
                        row.AutoItem().Text(PosturaLgpd.Rotulo(q.Como).ToUpperInvariant())
                            .FontSize(7.5f).Bold().FontColor(cor);
                    });
                    c.Item().PaddingTop(4).Text(q.Medido).FontSize(9.5f).LineHeight(1.4f);
                    if (!string.IsNullOrWhiteSpace(q.OQueFazer))
                    {
                        c.Item().PaddingTop(4).Text(tt =>
                        {
                            tt.Span("O que fazer: ").FontSize(8.5f).Bold().FontColor(Muted);
                            tt.Span(q.OQueFazer).FontSize(8.5f).FontColor(Muted).LineHeight(1.35f);
                        });
                    }
                });
            }

            if (r.Lacunas.Count > 0)
            {
                corpo.Item().PaddingTop(16).Text("O QUE ESTE MONITORAMENTO NÃO COBRE")
                    .FontSize(9).Bold().FontColor(Muted);
                corpo.Item().PaddingTop(6).Column(c =>
                {
                    foreach (var l in r.Lacunas)
                    {
                        c.Item().PaddingBottom(4).Row(row =>
                        {
                            row.ConstantItem(12).Text("○").FontSize(8).FontColor(Muted);
                            row.RelativeItem().Text(l).FontSize(8.5f).FontColor(Muted).LineHeight(1.4f);
                        });
                    }
                });
            }
        });
    }

    // ── O achado, para o caso declarado ──────────────────────────────────────────────────────

    private static void DesenharAchados(ColumnDescriptor corpo, List<DiagnosticoRisco> riscos, string vazio)
    {
        corpo.Item().PaddingTop(14).Text("O QUE A EMPRESA DECLAROU")
            .FontSize(9).Bold().FontColor(Muted);

        if (riscos.Count == 0)
        {
            corpo.Item().PaddingTop(8).Text(vazio).FontSize(9.5f).FontColor(Muted).LineHeight(1.4f);
            return;
        }

        var ordenados = riscos.OrderByDescending(r => r.Gravidade).ToList();
        foreach (var r in ordenados)
        {
            var cor = Services.Diagnostico.RotulosDoDiagnostico.ParaImpressao(
                Services.Diagnostico.RotulosDoDiagnostico.CorDaGravidade(r.Gravidade));
            var rotuloGravidade = r.Gravidade switch
            {
                GravidadeRisco.Critico => "CRÍTICO",
                GravidadeRisco.Alto => "ALTO",
                GravidadeRisco.Medio => "MÉDIO",
                _ => "BAIXO",
            };

            var pergunta = Services.Diagnostico.CatalogoDeDominios.BuscarPergunta(r.PerguntaCodigo);
            var artigos = pergunta?.Frameworks
                .Where(f => f.StartsWith("LGPD", StringComparison.OrdinalIgnoreCase))
                .ToArray() ?? [];

            corpo.Item().PaddingTop(8).BorderLeft(2).BorderColor(cor).PaddingLeft(10).Column(c =>
            {
                c.Item().Row(row =>
                {
                    row.RelativeItem().Text(r.Titulo).FontSize(10.5f).Bold();
                    row.AutoItem().Text(rotuloGravidade).FontSize(7.5f).Bold().FontColor(cor);
                });

                if (artigos.Length > 0)
                {
                    c.Item().PaddingTop(2).Text(string.Join(" · ", artigos))
                        .FontSize(7.5f).Bold().FontColor(Azul);
                }

                c.Item().PaddingTop(4).Text(r.Descricao).FontSize(9.5f).LineHeight(1.4f);

                if (!string.IsNullOrWhiteSpace(r.Recomendacao))
                {
                    c.Item().PaddingTop(4).Text(tt =>
                    {
                        tt.Span("O que fazer: ").FontSize(8.5f).Bold().FontColor(Muted);
                        tt.Span(r.Recomendacao).FontSize(8.5f).FontColor(Muted).LineHeight(1.35f);
                    });
                }
            });
        }
    }

    // ── O esqueleto comum aos dois documentos ───────────────────────────────────────────────

    private byte[] Montar(string empresa, string? cnpj, string? parceiroNome, Preco? preco,
        bool trataDadosDeCriancas, bool jaTemFerramenta, string? ferramentaExistente,
        Action<ColumnDescriptor> corpo)
    {
        var agora = DateTimeOffset.Now;

        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(42);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri).FontColor(Text));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.AutoItem().Width(26).Height(26).Background(Fundo).AlignMiddle().AlignCenter()
                            .Padding(5).Image(_iconMonoBranco);
                        row.RelativeItem().PaddingLeft(9).AlignMiddle().Text(t =>
                        {
                            t.Span("L'okta ").FontSize(13).Bold().FontColor(Fundo);
                            t.Span("IA").FontSize(13).Bold().FontColor("#4D9BFF");
                        });
                        row.AutoItem().AlignMiddle().Text($"{agora:dd/MM/yyyy}").FontSize(9).FontColor(Muted);
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Item().Text("PROPOSTA — SEGURANÇA PARA A LGPD").FontSize(15).Bold();
                    col.Item().PaddingTop(3).Text("Escopo exclusivo: os controles da Lei nº 13.709/2018 tocados pelo levantamento")
                        .FontSize(9.5f).FontColor(Muted);

                    // ── Cliente ────────────────────────────────────────────────────────────
                    col.Item().PaddingTop(16).Background("#F3F6FB").Padding(12).Column(c =>
                    {
                        c.Item().Text(empresa).FontSize(13).Bold();
                        if (!string.IsNullOrWhiteSpace(cnpj))
                        {
                            c.Item().PaddingTop(3).Text($"CNPJ {cnpj}").FontSize(9).FontColor(Muted);
                        }

                        // ⚠️ MESMA REGRA do orçamento: a ressalva vai DENTRO do arquivo. Este PDF
                        // é o que circula até quem vai pagar, e sem a frase o valor pareceria o
                        // preço ao cliente final — que o parceiro é quem define.
                        if (!string.IsNullOrWhiteSpace(parceiroNome))
                        {
                            c.Item().PaddingTop(6).Text(t =>
                            {
                                t.Span("Proposta encaminhada por ").FontSize(8.5f).FontColor(Muted);
                                t.Span(parceiroNome).FontSize(8.5f).Bold().FontColor(Muted);
                                t.Span(" — o valor abaixo é o repasse ao parceiro; o preço ao cliente final é definido por ele.")
                                    .FontSize(8.5f).FontColor(Muted);
                            });
                        }
                    });

                    // ── O corpo: declarado ou medido, conforme o caso ───────────────────────
                    corpo(col);

                    // ⚠️ COMO FUNCIONA, ANTES DO PREÇO — pedido do dono: "o fluxograma mostrando
                    // como vai funcionar a estrutura, a internet, nuvem, estações, a API, o
                    // Wazuh". Mesmo desenho do orçamento (FluxogramaDaSolucao) — explica o
                    // MECANISMO do serviço, nunca confundir com o mapa de risco acima.
                    FluxogramaDaSolucao.Desenhar(col, preco?.Maquinas ?? 0, jaTemFerramenta,
                        ferramentaExistente, preco?.HospedagemDoCliente ?? false, Muted, Azul);

                    // ── O serviço proposto: uma frase que muda com o que já existe ──────────
                    //
                    // ⚠️ DUAS FRASES, NUNCA UMA SÓ (06/09/2026, pedido do dono). "Implantar" e
                    // "administrar o que já existe" são serviços DIFERENTES — preço diferente,
                    // trabalho diferente, e dizer "implantação" para quem já tem Wazuh venderia
                    // uma instalação que não vai acontecer.
                    col.Item().PaddingTop(18).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(9.5f).LineHeight(1.5f));
                        if (jaTemFerramenta)
                        {
                            t.Span("Serviço proposto: ").Bold();
                            t.Span("administração, configuração e suporte contínuo sobre ");
                            t.Span(string.IsNullOrWhiteSpace(ferramentaExistente) ? "a ferramenta já em uso" : ferramentaExistente!)
                                .Bold();
                            t.Span(". Não há instalação de servidor novo — a plataforma passa a ler o que a "
                                 + "ferramenta já produz e assume a triagem dos alertas e o relatório mensal.");
                        }
                        else
                        {
                            t.Span("Serviço proposto: ").Bold();
                            t.Span("implantação de um servidor de segurança dedicado, com agente em cada "
                                 + "máquina, triagem de alertas e relatório mensal.");
                        }
                    });

                    // ── O preço, quando já existe orçamento ─────────────────────────────────
                    if (preco is { } p)
                    {
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                            {
                                c.Item().Text(jaTemFerramenta ? "ONBOARDING · UMA VEZ" : "IMPLANTAÇÃO · UMA VEZ")
                                    .FontSize(7.5f).Bold().FontColor(Muted);
                                c.Item().PaddingTop(4).Text($"R$ {p.ValorImplantacao:N2}").FontSize(19).Bold();
                            });
                            row.ConstantItem(12);
                            row.RelativeItem().Border(1).BorderColor(Azul).Background("#F4F7FE").Padding(12).Column(c =>
                            {
                                c.Item().Text(jaTemFerramenta ? "ADMINISTRAÇÃO MENSAL" : "MENSALIDADE")
                                    .FontSize(7.5f).Bold().FontColor(Azul);
                                c.Item().PaddingTop(4).Text($"R$ {p.ValorMensal:N2}").FontSize(19).Bold();
                            });
                        });
                        col.Item().PaddingTop(4).Text($"Ref. orçamento {p.Numero}.").FontSize(8).FontColor(Muted);

                        // ⚠️ ITEM A ITEM — pedido do dono: "detalhar o orçamento... valor por
                        // estação". Nunca recalculado aqui: vem do que foi gravado no orçamento
                        // no momento do cálculo. Ver o comentário de `ItensDeCustoJson`.
                        if (p.Itens is { Count: > 0 } itensDeCusto)
                        {
                            DetalhamentoDeCustoPdf.Desenhar(col, jaTemFerramenta ? "ONBOARDING · DETALHAMENTO" : "IMPLANTAÇÃO · DETALHAMENTO",
                                itensDeCusto.Where(i => i.Grupo == "Implantação").ToList(),
                                itensDeCusto.Where(i => i.Grupo == "Implantação").Sum(i => i.Valor),
                                p.ValorImplantacao, Muted, Azul);
                            DetalhamentoDeCustoPdf.Desenhar(col, jaTemFerramenta ? "ADMINISTRAÇÃO MENSAL · DETALHAMENTO" : "MENSALIDADE · DETALHAMENTO",
                                itensDeCusto.Where(i => i.Grupo == "Mensalidade").ToList(),
                                itensDeCusto.Where(i => i.Grupo == "Mensalidade").Sum(i => i.Valor),
                                p.ValorMensal, Muted, Azul);
                        }

                        // ⚠️ O FECHO EXPLICA A CONSEQUÊNCIA DE ASSINAR, e é PRECISO no que promete
                        // (pedido do dono: "explicando que se implantar, estaremos dentro do que a
                        // lei exige"). "Atende ao art. 46" é conferível — o artigo pede medida
                        // técnica de segurança, e é isso que o serviço entrega. "Em conformidade
                        // com a LGPD", sem o artigo, é a frase que a ressalva abaixo proíbe: a lei
                        // tem mais exigências do que UMA medida técnica cobre.
                        col.Item().PaddingTop(10).Background("#EEF4FF").Padding(11).Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(9).LineHeight(1.5f));
                            t.Span(jaTemFerramenta
                                ? "Ao contratar esta administração, "
                                : "Ao implantar este serviço, ");
                            t.Span($"{empresa} passa a ter em operação, com evidência mensal, a medida "
                                 + "técnica de segurança que o ").FontColor("#1C2836");
                            t.Span("art. 46 da LGPD").Bold();
                            t.Span(" exige — o requisito que hoje está em aberto no levantamento acima.")
                                .FontColor("#1C2836");
                        });
                    }
                    else
                    {
                        col.Item().PaddingTop(10).Text(
                            "O valor do serviço é enviado em documento à parte, após confirmação do "
                            + "parque de máquinas.").FontSize(9).FontColor(Muted);
                    }

                    // ⚠️ A LEI, SEMPRE — mesmo bloco do orçamento, mesma fonte única.
                    SecaoLeiLgpd.Desenhar(col, trataDadosDeCriancas, Muted, Azul);
                });

                page.Footer().PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"L'okta IA · {empresa} · gerado em {agora:dd/MM/yyyy HH:mm}")
                            .FontSize(7.5f).FontColor(Muted);
                        row.AutoItem().Text(t =>
                        {
                            t.CurrentPageNumber().FontSize(7.5f).FontColor(Muted);
                            t.Span(" / ").FontSize(7.5f).FontColor(Muted);
                            t.TotalPages().FontSize(7.5f).FontColor(Muted);
                        });
                    });
            });
        });

        return documento.GeneratePdf();
    }
}
