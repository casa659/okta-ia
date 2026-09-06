using OktaIA.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

/// <summary>
/// O relatório do mês de um cliente monitorado.
///
/// POR QUE ELE EXISTE: é o que justifica a mensalidade. Monitoramento bem feito é invisível — o
/// cliente paga todo mês e não vê nada acontecer, e serviço que não se vê é o primeiro a ser
/// cortado. Este documento é a prova mensal de que alguém esteve olhando.
///
/// ⚠️ DIZ O QUE FOI VISTO, NÃO O QUE FOI RESOLVIDO. Um relatório que conta só o que foi tratado
/// esconde a fila — e a fila é justamente a conversa que precisa acontecer com o cliente.
///
/// ⚠️ NÃO INVENTA MÊS VAZIO. Se não entrou nada, o documento diz isso com todas as letras em vez
/// de mostrar zeros com cara de resultado: "nenhum alerta no período" é informação, e pode
/// significar tanto tranquilidade quanto agente parado — e o relatório separa os dois.
/// </summary>
public class RelatorioMensalPdfService
{
    private readonly byte[] _iconMonoBranco;

    static RelatorioMensalPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public RelatorioMensalPdfService(IWebHostEnvironment env)
    {
        _iconMonoBranco = File.ReadAllBytes(Path.Combine(env.WebRootPath, "img", "brand", "simbolo-mono-branco.png"));
    }

    private const string Muted = "#5A7191";
    private const string Text = "#1C2836";
    private const string Azul = "#0B45DD";
    private const string Fundo = "#0B1220";

    public record Dados(
        string Empresa,
        DateOnly Inicio,
        DateOnly Fim,
        List<AlertaUnificado> Alertas,
        int MaquinasVistas,
        DateTimeOffset? UltimoSync,
        string? Conector);

    public byte[] Gerar(Dados d)
    {
        var criticos = d.Alertas.Count(a => a.Severidade == Severidade.Critica);
        var altos = d.Alertas.Count(a => a.Severidade == Severidade.Alta);
        var medios = d.Alertas.Count(a => a.Severidade == Severidade.Media);
        var baixos = d.Alertas.Count - criticos - altos - medios;

        var tratados = d.Alertas.Count(a => a.Status is StatusTriagem.Resolvido or StatusTriagem.FalsoPositivo);
        var abertos = d.Alertas.Count - tratados;

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
                        row.AutoItem().AlignMiddle().Text($"{d.Inicio:MM/yyyy}").FontSize(9).Bold().FontColor(Azul);
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Item().Text("RELATÓRIO MENSAL DE MONITORAMENTO").FontSize(15).Bold();
                    col.Item().PaddingTop(3).Text($"{d.Empresa} · {d.Inicio:dd/MM/yyyy} a {d.Fim:dd/MM/yyyy}")
                        .FontSize(10).FontColor(Muted);

                    // ── O que o mês teve ───────────────────────────────────────────────────
                    col.Item().PaddingTop(18).Row(row =>
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

                        Caixa("CRÍTICOS", criticos.ToString(), criticos > 0 ? "#C42D4A" : Text);
                        Caixa("ALTOS", altos.ToString(), altos > 0 ? "#B36A12" : Text);
                        Caixa("MÉDIOS", medios.ToString(), Text);
                        Caixa("BAIXOS", baixos.ToString(), Text);

                        row.RelativeItem().Border(1).BorderColor(Azul).Background("#F4F7FE").Padding(10).Column(c =>
                        {
                            c.Item().Text("MÁQUINAS").FontSize(7).Bold().FontColor(Azul);
                            c.Item().PaddingTop(3).Text(d.MaquinasVistas.ToString()).FontSize(17).Bold();
                        });
                    });

                    // ── A leitura, em uma frase honesta ────────────────────────────────────
                    col.Item().PaddingTop(16).Background("#F3F6FB").Padding(12).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10).LineHeight(1.45f));

                        if (d.Alertas.Count == 0)
                        {
                            t.Span("Nenhum alerta foi registrado no período. ").Bold();
                            t.Span("Isso pode significar um mês tranquilo — ou um agente parado. "
                                 + "A data do último recebimento, abaixo, é o que separa os dois casos.");
                        }
                        else if (criticos + altos == 0)
                        {
                            t.Span($"{d.Alertas.Count} alerta(s) no período, nenhum grave. ").Bold();
                            t.Span("O que entrou é de conformidade e configuração — importa para o "
                                 + "endurecimento das máquinas, não exige ação imediata.");
                        }
                        else
                        {
                            t.Span($"{criticos + altos} alerta(s) grave(s) no período").Bold();
                            t.Span($", de um total de {d.Alertas.Count}. ");
                            t.Span(abertos > 0
                                ? $"{abertos} seguem em aberto e precisam de decisão."
                                : "Todos foram tratados.");
                        }
                    });

                    if (d.UltimoSync is { } sync)
                    {
                        col.Item().PaddingTop(8).Text($"Último dado recebido da ferramenta em "
                            + $"{sync.ToLocalTime():dd/MM/yyyy 'às' HH:mm}"
                            + (d.Conector is { Length: > 0 } cn ? $" · via {cn}" : "") + ".")
                            .FontSize(9).FontColor(Muted);
                    }

                    // ── Os graves, um por um ───────────────────────────────────────────────
                    var graves = d.Alertas
                        .Where(a => a.Severidade is Severidade.Critica or Severidade.Alta)
                        .OrderByDescending(a => a.Severidade)
                        .ThenByDescending(a => a.OcorridoEm)
                        .Take(25)
                        .ToList();

                    if (graves.Count > 0)
                    {
                        col.Item().PaddingTop(20).Text("ALERTAS GRAVES DO PERÍODO").FontSize(9).Bold().FontColor(Muted);
                        col.Item().PaddingTop(6).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(52);
                                c.RelativeColumn(4);
                                c.RelativeColumn(2);
                                c.ConstantColumn(58);
                            });

                            void Cabecalho(string s)
                            {
                                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(4)
                                    .Text(s).FontSize(7.5f).Bold().FontColor(Muted);
                            }

                            Cabecalho("NÍVEL"); Cabecalho("ALERTA"); Cabecalho("MÁQUINA"); Cabecalho("QUANDO");

                            foreach (var a in graves)
                            {
                                var cor = a.Severidade == Severidade.Critica ? "#C42D4A" : "#B36A12";
                                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5)
                                    .Text(a.Severidade == Severidade.Critica ? "crítico" : "alto")
                                    .FontSize(8.5f).Bold().FontColor(cor);
                                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5)
                                    .PaddingRight(6).Text(a.Titulo).FontSize(8.5f);
                                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5)
                                    .Text(a.AtivoNome ?? "—").FontSize(8.5f).FontColor(Muted);
                                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5)
                                    .Text(a.OcorridoEm.ToLocalTime().ToString("dd/MM")).FontSize(8.5f).FontColor(Muted);
                            }
                        });

                        var sobraram = d.Alertas.Count(a => a.Severidade is Severidade.Critica or Severidade.Alta) - graves.Count;
                        if (sobraram > 0)
                        {
                            col.Item().PaddingTop(6).Text($"… e mais {sobraram} grave(s) no período, na plataforma.")
                                .FontSize(8.5f).FontColor(Muted);
                        }
                    }

                    // ── O que mais apareceu ────────────────────────────────────────────────
                    var porTipo = d.Alertas
                        .GroupBy(a => a.Titulo)
                        .OrderByDescending(g => g.Count())
                        .Take(8)
                        .ToList();

                    if (porTipo.Count > 0)
                    {
                        col.Item().PaddingTop(20).Text("O QUE MAIS APARECEU").FontSize(9).Bold().FontColor(Muted);
                        col.Item().PaddingTop(6).Column(c =>
                        {
                            foreach (var g in porTipo)
                            {
                                c.Item().PaddingBottom(3).Row(r =>
                                {
                                    r.ConstantItem(34).Text($"{g.Count()}×").FontSize(9).Bold().FontColor(Azul);
                                    r.RelativeItem().Text(g.Key).FontSize(9);
                                });
                            }
                        });
                    }

                    // ⚠️ O limite do serviço, dito no documento e não só na venda. Cliente que
                    // entende "monitoramento" como "vocês impedem o ataque" cobra exatamente isso
                    // no primeiro incidente.
                    col.Item().PaddingTop(20).Background("#FFF8EE").Padding(10).Text(t =>
                    {
                        t.Span("Sobre este serviço: ").FontSize(8.5f).Bold();
                        t.Span("detectamos, registramos e avisamos. O monitoramento não substitui "
                             + "antivírus, firewall ou backup, e não bloqueia ataques automaticamente.")
                            .FontSize(8.5f).FontColor(Muted);
                    });
                });

                page.Footer().PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"L'okta IA · {d.Empresa} · {d.Inicio:MM/yyyy}")
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
