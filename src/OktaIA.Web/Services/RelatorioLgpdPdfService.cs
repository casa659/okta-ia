using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

/// <summary>
/// O relatório de postura de LGPD de uma empresa — o documento que o gestor leva ao jurídico.
///
/// ⚠️ É UM RELATÓRIO DE POSTURA, NÃO UM CERTIFICADO. A diferença não é semântica: um certificado
/// afirma conformidade, e conformidade com a LGPD não se atesta a partir de dado técnico. Este
/// documento diz o que foi MEDIDO em relação a quatro obrigações, e diz por escrito tudo o que
/// continua faltando. Emitir algo com cara de selo seria criar, para quem assina, a
/// responsabilidade de um parecer que ninguém deu.
///
/// ⚠️ AS LACUNAS ENTRAM NO DOCUMENTO, e não numa conversa depois. É a parte que protege o cliente
/// — e a que abre a próxima venda, que é honesta justamente por estar escrita aqui.
/// </summary>
public class RelatorioLgpdPdfService
{
    private readonly byte[] _iconMonoBranco;

    static RelatorioLgpdPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public RelatorioLgpdPdfService(IWebHostEnvironment env)
    {
        _iconMonoBranco = File.ReadAllBytes(Path.Combine(env.WebRootPath, "img", "brand", "simbolo-mono-branco.png"));
    }

    private const string Muted = "#5A7191";
    private const string Text = "#1C2836";
    private const string Azul = "#0B45DD";
    private const string Fundo = "#0B1220";

    private static string CorImpressa(PosturaLgpd.Situacao s) => s switch
    {
        PosturaLgpd.Situacao.Atendido => "#12855C",
        PosturaLgpd.Situacao.Parcial => "#B36A12",
        PosturaLgpd.Situacao.NaoAtendido => "#C42D4A",
        _ => "#5A7191",
    };

    public byte[] Gerar(PosturaLgpd.Resultado r)
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
                    col.Item().Text("POSTURA DE SEGURANÇA PARA A LGPD").FontSize(15).Bold();
                    col.Item().PaddingTop(3).Text($"{r.Empresa} · Lei nº 13.709/2018")
                        .FontSize(10).FontColor(Muted);

                    // ── O que foi medido ───────────────────────────────────────────────────
                    col.Item().PaddingTop(16).Row(row =>
                    {
                        void Caixa(string rotulo, string valor, string cor)
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
                            {
                                c.Item().Text(rotulo).FontSize(7).Bold().FontColor(Muted);
                                c.Item().PaddingTop(3).Text(valor).FontSize(17).Bold().FontColor(cor);
                            });
                            row.ConstantItem(8);
                        }

                        Caixa("REQUISITOS TÉCNICOS", $"{r.Atendidos}/{r.Avaliados}",
                            r.Atendidos == r.Avaliados ? "#12855C" : "#B36A12");
                        Caixa("MÁQUINAS", r.MaquinasVistas.ToString(), Text);
                        Caixa("ALERTAS", r.AlertasTotal.ToString("N0"), Text);

                        row.RelativeItem().Border(1)
                            .BorderColor(r.GravesAbertos > 0 ? "#C42D4A" : Colors.Grey.Lighten2)
                            .Padding(10).Column(c =>
                        {
                            c.Item().Text("GRAVES EM ABERTO").FontSize(7).Bold().FontColor(Muted);
                            c.Item().PaddingTop(3).Text(r.GravesAbertos.ToString()).FontSize(17).Bold()
                                .FontColor(r.GravesAbertos > 0 ? "#C42D4A" : "#12855C");
                        });
                    });

                    if (r.UltimoSync is { } s)
                    {
                        col.Item().PaddingTop(8).Text($"Último dado recebido em "
                            + $"{s.ToLocalTime():dd/MM/yyyy 'às' HH:mm}"
                            + (r.Conector is { Length: > 0 } c ? $" · via {c}" : "") + ".")
                            .FontSize(9).FontColor(Muted);
                    }
                    else
                    {
                        col.Item().PaddingTop(8).Background("#FCE7EB").Padding(9)
                            .Text("Nenhum dado recebido: não há conector reportando nesta empresa. "
                                + "As conclusões abaixo refletem essa ausência.")
                            .FontSize(9).FontColor("#C42D4A");
                    }

                    // ── Artigo a artigo ────────────────────────────────────────────────────
                    col.Item().PaddingTop(20).Text("O QUE A LEI EXIGE, E O QUE FOI MEDIDO")
                        .FontSize(9).Bold().FontColor(Muted);

                    foreach (var q in r.Requisitos)
                    {
                        col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten2)
                            .Padding(12).Column(c =>
                        {
                            c.Item().Row(row =>
                            {
                                row.AutoItem().Text(q.Artigo).FontSize(9).Bold().FontColor(Azul);
                                row.RelativeItem().PaddingLeft(8).Text(q.Titulo).FontSize(11).Bold();
                                row.AutoItem().Text(PosturaLgpd.Rotulo(q.Como).ToUpperInvariant())
                                    .FontSize(7.5f).Bold().FontColor(CorImpressa(q.Como));
                            });

                            c.Item().PaddingTop(6).Text(q.Exige).FontSize(9).FontColor(Muted).LineHeight(1.4f);

                            c.Item().PaddingTop(6).BorderLeft(2).BorderColor(CorImpressa(q.Como))
                                .PaddingLeft(8).Text(q.Medido).FontSize(9.5f).LineHeight(1.4f);

                            // ⚠️ O "o que fazer" entra no PDF do cliente também. Relatório que
                            // aponta pendência sem dizer o caminho vira cobrança; com o caminho,
                            // vira plano — e é assim que ele volta para a próxima reunião.
                            if (!string.IsNullOrWhiteSpace(q.OQueFazer))
                            {
                                c.Item().PaddingTop(8).Text(tt =>
                                {
                                    tt.Span("O que fazer: ").FontSize(9).Bold().FontColor(Muted);
                                    tt.Span(q.OQueFazer).FontSize(9).FontColor(Muted).LineHeight(1.4f);
                                });
                            }
                        });
                    }

                    // ── As lacunas ─────────────────────────────────────────────────────────
                    col.Item().PageBreak();

                    col.Item().Text("O QUE ESTE MONITORAMENTO NÃO COBRE").FontSize(14).Bold();
                    col.Item().PaddingTop(6).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10).LineHeight(1.5f));
                        t.Span("As obrigações abaixo continuam sendo do ");
                        t.Span("controlador").Bold();
                        t.Span(" e não são medidas por esta plataforma. Estão listadas aqui de "
                             + "propósito: um relatório de conformidade que mostra apenas o que "
                             + "está verde induz a erro justamente quem confia nele.");
                    });

                    col.Item().PaddingTop(12).Column(c =>
                    {
                        foreach (var l in r.Lacunas)
                        {
                            c.Item().PaddingBottom(5).Row(row =>
                            {
                                row.ConstantItem(14).Text("○").FontSize(9).FontColor(Muted);
                                row.RelativeItem().Text(l).FontSize(9.5f).LineHeight(1.4f);
                            });
                        }
                    });

                    col.Item().PaddingTop(18).Background("#FFF8EE").Padding(11).Text(t =>
                    {
                        t.Span("Natureza deste documento: ").FontSize(8.5f).Bold();
                        t.Span("é um relatório de postura técnica, não um certificado de "
                             + "conformidade nem parecer jurídico. Ele registra o que foi medido "
                             + "nas máquinas monitoradas, na data indicada, em relação a quatro "
                             + "obrigações da Lei nº 13.709/2018. A adequação à LGPD depende "
                             + "também dos itens listados acima.")
                            .FontSize(8.5f).FontColor(Muted);
                    });
                });

                page.Footer().PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"L'okta IA · {r.Empresa} · emitido em {agora:dd/MM/yyyy HH:mm}")
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
