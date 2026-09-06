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
    public record Preco(string Numero, decimal ValorImplantacao, decimal ValorMensal);

    // ── Declarado: a partir das respostas da planilha ───────────────────────────────────────

    /// <summary>
    /// A proposta quando a empresa NÃO tem monitoramento implantado — o que se sabe vem do que o
    /// cliente respondeu na planilha, e nada mais.
    /// </summary>
    public byte[] GerarDeclarado(string empresa, string? cnpj, string? parceiroNome,
        Models.Diagnostico diagnostico, List<DiagnosticoRisco> riscos, Preco? preco,
        bool trataDadosDeCriancas = false)
    {
        var totalRespondidas = diagnostico.Respostas.Count;
        var quando = diagnostico.RealizadoEm
            ?? DateOnly.FromDateTime((diagnostico.ConcluidoEm ?? diagnostico.CriadoEm).LocalDateTime);

        return Montar(empresa, cnpj, parceiroNome, preco, trataDadosDeCriancas,
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
        return Montar(empresa, cnpj, parceiroNome, preco, trataDadosDeCriancas, corpo =>
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
        bool trataDadosDeCriancas, Action<ColumnDescriptor> corpo)
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

                    // ── O preço, quando já existe orçamento ─────────────────────────────────
                    if (preco is { } p)
                    {
                        col.Item().PaddingTop(18).Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                            {
                                c.Item().Text("IMPLANTAÇÃO · UMA VEZ").FontSize(7.5f).Bold().FontColor(Muted);
                                c.Item().PaddingTop(4).Text($"R$ {p.ValorImplantacao:N2}").FontSize(19).Bold();
                            });
                            row.ConstantItem(12);
                            row.RelativeItem().Border(1).BorderColor(Azul).Background("#F4F7FE").Padding(12).Column(c =>
                            {
                                c.Item().Text("MENSALIDADE").FontSize(7.5f).Bold().FontColor(Azul);
                                c.Item().PaddingTop(4).Text($"R$ {p.ValorMensal:N2}").FontSize(19).Bold();
                            });
                        });
                        col.Item().PaddingTop(4).Text($"Ref. orçamento {p.Numero}. Detalhamento do escopo no documento anexo.")
                            .FontSize(8).FontColor(Muted);
                    }
                    else
                    {
                        col.Item().PaddingTop(16).Text(
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
