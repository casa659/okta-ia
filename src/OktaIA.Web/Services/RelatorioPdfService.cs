using OktaIA.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

// Gera o PDF de "relatório de segurança" a partir dos achados reais (FonteScan=true) de uma
// empresa — é o "relatório" que o cliente PME mostra pro seguro/LGPD/questionário do cliente
// maior. Só cobre achados reais do scanner, não as CVEs fixas do mockup.
public class RelatorioPdfService
{
    static RelatorioPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // paraCliente=true omite o bloco "Como corrigir" (Vulnerability.Instrucoes) — é o passo a passo
    // técnico de remediação, que só faz sentido pra quem vai executar a correção (Admin/equipe
    // interna) ou pra consultoria vendida à parte; o relatório que vai pro cliente final mostra o
    // achado e o risco de negócio, mas não entrega de graça o "como resolver" que é justamente o
    // que a L'okta IA vende como serviço. Risco e Recomendação (1 linha) continuam visíveis nos dois.
    public byte[] Gerar(string empresaNome, List<Vulnerability> achados, List<(string Nome, string Ip, DateTimeOffset? UltimoScan)> ativosEscaneados, string lang, bool paraCliente = false)
    {
        var pt = lang != "en";
        var geradoEm = DateTimeOffset.Now;

        string CorSeveridade(Severidade s) => s switch
        {
            Severidade.Critica => Colors.Red.Darken1,
            Severidade.Alta => Colors.Orange.Darken1,
            Severidade.Media => Colors.Amber.Darken2,
            _ => Colors.Blue.Darken1,
        };

        string LabelSeveridade(Severidade s) => pt
            ? s switch { Severidade.Critica => "CRÍTICA", Severidade.Alta => "ALTA", Severidade.Media => "MÉDIA", _ => "BAIXA" }
            : s switch { Severidade.Critica => "CRITICAL", Severidade.Alta => "HIGH", Severidade.Media => "MEDIUM", _ => "LOW" };

        var porAtivo = achados.GroupBy(a => a.AssetNome).ToList();

        var portasAbertas = achados.Count(a => a.CategoriaScan == SecurityScanService.CategoriaPortas);
        var score = CompanySecurityScoreCalculator.Calcular(achados, ativosEscaneados.Count, portasAbertas);
        var corScore = score.Score >= 80 ? Colors.Green.Darken1 : score.Score >= 50 ? Colors.Amber.Darken2 : Colors.Red.Darken1;

        var documento = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(capa =>
            {
                capa.Size(PageSizes.A4);
                capa.Margin(40);
                capa.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri));

                capa.Content().Column(col =>
                {
                    col.Item().Text(pt ? "Cyber Security Score" : "Cyber Security Score").FontSize(14).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(2).Text(empresaNome).FontSize(24).Bold();
                    col.Item().PaddingTop(2).Text($"{(pt ? "Gerado em" : "Generated on")} {geradoEm:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Medium);

                    col.Item().PaddingTop(24).Row(row =>
                    {
                        row.RelativeItem().Column(scoreCol =>
                        {
                            scoreCol.Item().Row(r =>
                            {
                                r.AutoItem().Text(score.Score.ToString()).FontSize(56).Bold().FontColor(corScore);
                                r.AutoItem().PaddingTop(28).PaddingLeft(4).Text("/100").FontSize(16).FontColor(Colors.Grey.Medium);
                            });
                            scoreCol.Item().PaddingTop(4).Height(10).Background(Colors.Grey.Lighten3).Row(barRow =>
                            {
                                barRow.RelativeItem(Math.Max(score.Score, 0.001f)).Background(corScore);
                                barRow.RelativeItem(Math.Max(100 - score.Score, 0.001f));
                            });
                            scoreCol.Item().PaddingTop(6).Text((pt ? "Classificação: " : "Rating: ") + score.Classificacao).FontSize(11).Bold();
                        });

                        row.ConstantItem(160).Column(riscoCol =>
                        {
                            void Linha(string label, string valor, string cor)
                            {
                                riscoCol.Item().PaddingBottom(10).Column(c =>
                                {
                                    c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Medium);
                                    c.Item().Text(valor).FontSize(13).Bold().FontColor(cor);
                                });
                            }

                            Linha(pt ? "Risco atual" : "Current risk", $"{score.RiscoEmoji} {(pt ? score.RiscoLabelPt : score.RiscoLabelEn)}", score.RiscoCor);
                            Linha(pt ? "Superfície de ataque" : "Attack surface", pt ? score.SuperficiePt : score.SuperficieEn, Colors.Grey.Darken3);
                            Linha(pt ? "Risco de phishing" : "Phishing risk", pt ? score.PhishingPt : score.PhishingEn, Colors.Grey.Darken3);
                        });
                    });

                    col.Item().PaddingTop(28).Text(pt ? "Verificações realizadas" : "Checks performed").FontSize(11).Bold();
                    (string Pt, string En)[] categorias =
                    [
                        ("Certificado e protocolo TLS", "TLS certificate and protocol"),
                        ("Cabeçalhos de segurança HTTP (HSTS, CSP, X-Frame-Options...)", "HTTP security headers (HSTS, CSP, X-Frame-Options...)"),
                        ("Autenticação de e-mail (SPF, DMARC)", "Email authentication (SPF, DMARC)"),
                        ("Portas comumente exploradas expostas publicamente", "Commonly exploited ports exposed publicly"),
                    ];
                    col.Item().PaddingTop(6).Column(catCol =>
                    {
                        foreach (var cat in categorias)
                        {
                            catCol.Item().PaddingBottom(4).Text("✓ " + (pt ? cat.Pt : cat.En)).FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                        }
                    });

                    col.Item().PaddingTop(10).Text(pt
                        ? $"Baseado em {ativosEscaneados.Count} ativo(s) verificado(s) e {achados.Count} achado(s) — detalhe completo nas páginas seguintes."
                        : $"Based on {ativosEscaneados.Count} asset(s) checked and {achados.Count} finding(s) — full detail on the following pages.")
                        .FontSize(9).FontColor(Colors.Grey.Medium).Italic();
                });

                capa.Footer().AlignCenter().Text(x =>
                {
                    x.Span(pt ? "Página " : "Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Calibri));

                page.Header().Column(col =>
                {
                    col.Item().Text(pt ? "Relatório de Segurança Externa" : "External Security Report").FontSize(18).Bold();
                    col.Item().Text(empresaNome).FontSize(13).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(4).Text($"{(pt ? "Gerado em" : "Generated on")} {geradoEm:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Item().Text(pt ? "Resumo" : "Summary").FontSize(13).Bold();
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        void Kpi(string label, int valor, string cor)
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                            {
                                c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Medium);
                                c.Item().Text(valor.ToString()).FontSize(18).Bold().FontColor(cor);
                            });
                        }

                        Kpi(pt ? "Ativos verificados" : "Assets checked", ativosEscaneados.Count, Colors.Blue.Darken1);
                        Kpi(pt ? "Achados totais" : "Total findings", achados.Count, Colors.Grey.Darken2);
                        Kpi(pt ? "Críticos" : "Critical", achados.Count(a => a.Severidade == Severidade.Critica), Colors.Red.Darken1);
                        Kpi(pt ? "Altos" : "High", achados.Count(a => a.Severidade == Severidade.Alta), Colors.Orange.Darken1);
                    });

                    col.Item().PaddingTop(20).Text(pt ? "Ativos verificados" : "Assets checked").FontSize(13).Bold();
                    foreach (var (nome, ip, ultimoScan) in ativosEscaneados)
                    {
                        var achadosDoAtivo = achados.Where(a => a.AssetNome == nome).ToList();
                        col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(ativoCol =>
                        {
                            ativoCol.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text(nome).FontSize(11).Bold();
                                    if (!string.IsNullOrWhiteSpace(ip) && ip != "—")
                                    {
                                        c.Item().Text($"IP verificado: {ip}").FontSize(7.5f).FontColor(Colors.Grey.Medium);
                                    }
                                });
                                row.AutoItem().Text(ultimoScan is null ? (pt ? "nunca escaneado" : "never scanned") : ultimoScan.Value.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium);
                            });

                            if (achadosDoAtivo.Count == 0)
                            {
                                ativoCol.Item().PaddingTop(6).Text(pt
                                    ? "✓ Nenhum achado — TLS/certificado, cabeçalhos de segurança, SPF/DMARC e portas comuns verificados sem problemas."
                                    : "✓ No findings — TLS/certificate, security headers, SPF/DMARC and common ports checked with no issues.")
                                    .FontSize(9).FontColor(Colors.Green.Darken1);
                            }
                            else
                            {
                                foreach (var achado in achadosDoAtivo.OrderByDescending(a => a.Severidade))
                                {
                                    ativoCol.Item().PaddingTop(8).BorderLeft(3).BorderColor(CorSeveridade(achado.Severidade)).PaddingLeft(8).Column(achadoCol =>
                                    {
                                        achadoCol.Item().Row(row =>
                                        {
                                            row.AutoItem().Background(CorSeveridade(achado.Severidade)).Padding(3).Text(LabelSeveridade(achado.Severidade)).FontSize(7).Bold().FontColor(Colors.White);
                                            row.RelativeItem().PaddingLeft(6).Text(pt ? achado.TituloPt : achado.TituloEn).FontSize(10).Bold();
                                        });
                                        var risco = pt ? achado.RiscoPt : achado.RiscoEn;
                                        if (!string.IsNullOrWhiteSpace(risco))
                                        {
                                            achadoCol.Item().PaddingTop(3).Text((pt ? "Risco se não corrigir: " : "Risk if not fixed: ") + risco).FontSize(9).FontColor(Colors.Red.Darken2);
                                        }

                                        var recomendacao = pt ? achado.RecomendacaoPt : achado.RecomendacaoEn;
                                        if (!string.IsNullOrWhiteSpace(recomendacao))
                                        {
                                            achadoCol.Item().PaddingTop(3).Text((pt ? "Recomendação: " : "Recommendation: ") + recomendacao).FontSize(9).FontColor(Colors.Grey.Darken2);
                                        }

                                        var instrucoes = pt ? achado.InstrucoesPt : achado.InstrucoesEn;
                                        if (!paraCliente && !string.IsNullOrWhiteSpace(instrucoes))
                                        {
                                            achadoCol.Item().PaddingTop(6).Background(Colors.Grey.Lighten4).Padding(8).Column(instrCol =>
                                            {
                                                instrCol.Item().Text(pt ? "Como corrigir" : "How to fix").FontSize(8.5f).Bold().FontColor(Colors.Grey.Darken3);
                                                instrCol.Item().PaddingTop(3).Text(instrucoes).FontSize(8.5f).FontColor(Colors.Grey.Darken2).LineHeight(1.35f);
                                            });
                                        }
                                    });
                                }
                            }
                        });
                    }

                    col.Item().PaddingTop(24).Text(pt ? "Por que isso importa" : "Why this matters").FontSize(13).Bold();
                    col.Item().PaddingTop(4).Text(pt
                        ? "Corrigir os achados acima — e manter esse tipo de verificação contínua — sustenta cinco frentes que impactam diretamente o negócio:"
                        : "Fixing the findings above — and keeping this kind of check continuous — supports five fronts with direct business impact:")
                        .FontSize(9.5f).FontColor(Colors.Grey.Darken2);

                    (string TituloPt, string TituloEn, string DescPt, string DescEn)[] pilares =
                    [
                        ("Redução de risco", "Risk reduction",
                            "Cada achado corrigido fecha uma porta de entrada real usada em ataques automatizados e direcionados — menos superfície de exposição, menos probabilidade de incidente.",
                            "Every fixed finding closes a real entry point used in automated and targeted attacks — less exposure surface, lower likelihood of an incident."),
                        ("Rapidez para responder a incidentes", "Faster incident response",
                            "Monitoramento contínuo e achados já triados encurtam o tempo entre a exposição existir e alguém agir sobre ela — o que limita o dano quando algo passa.",
                            "Continuous monitoring and pre-triaged findings shorten the time between an exposure existing and someone acting on it — which limits the damage when something gets through."),
                        ("Conformidade", "Compliance",
                            "Evidência técnica documentada de monitoramento e correção apoia requisitos de LGPD, ISO 27001, NIST CSF e diretivas como a NIS2, além de questionários de segurança de clientes e seguradoras.",
                            "Documented technical evidence of monitoring and remediation supports LGPD, ISO 27001, NIST CSF and directives like NIS2, as well as customer and cyber-insurance security questionnaires."),
                        ("Visibilidade do ambiente", "Environment visibility",
                            "Inventário atualizado de ativos e do estado real de cada um — inclusive o que a equipe interna não sabia que estava exposto.",
                            "Up-to-date inventory of assets and each one's real state — including exposure the internal team didn't know existed."),
                        ("Relatórios para auditorias e diretoria", "Reports for audits and the board",
                            "Este mesmo relatório, gerado a qualquer momento, serve como evidência formal para auditoria interna/externa e para leitura executiva do risco pela diretoria.",
                            "This same report, generated on demand, serves as formal evidence for internal/external audits and as an executive-level risk read for the board."),
                    ];

                    col.Item().PaddingTop(10).Column(pilaresCol =>
                    {
                        foreach (var pilar in pilares)
                        {
                            pilaresCol.Item().PaddingTop(8).Row(row =>
                            {
                                row.ConstantItem(90).Text(pt ? pilar.TituloPt : pilar.TituloEn).FontSize(9).Bold().FontColor(Colors.Blue.Darken1);
                                row.RelativeItem().Text(pt ? pilar.DescPt : pilar.DescEn).FontSize(9).FontColor(Colors.Grey.Darken2);
                            });
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span(pt ? "Página " : "Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return documento.GeneratePdf();
    }
}
