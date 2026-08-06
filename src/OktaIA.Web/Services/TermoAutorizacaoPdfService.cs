using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

// Termo de Autorização para Verificação de Segurança — documento que formaliza por escrito o que
// o checkbox "Confirmo que sou responsável por esse domínio..." em /Ativos já pede em clique, mas
// sem registro nenhum além do próprio ato. Gerado ANTES do ativo existir no banco (a partir do que
// está digitado no formulário), então recebe nome da empresa e domínio como texto solto, não como
// entidades já persistidas.
public class TermoAutorizacaoPdfService
{
    static TermoAutorizacaoPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string Muted = "#5A7191";
    private const string Text = "#1C2836";

    public byte[] Gerar(string empresaNome, string dominio)
    {
        var geradoEm = DateTimeOffset.Now;
        var dominioExibicao = string.IsNullOrWhiteSpace(dominio) ? "________________________________" : dominio.Trim();

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
                        row.AutoItem().Width(26).Height(26).Background("#0B1220").AlignMiddle().AlignCenter()
                            .Text("O").FontSize(13).Bold().FontColor(Colors.White);
                        row.RelativeItem().PaddingLeft(9).AlignMiddle().Text(t =>
                        {
                            t.Span("Okta").FontSize(13).Bold().FontColor("#0B1220");
                            t.Span("IA").FontSize(13).Bold().FontColor("#4D9BFF");
                        });
                        row.AutoItem().AlignMiddle().Text($"Gerado em {geradoEm:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Muted);
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Item().Text("TERMO DE AUTORIZAÇÃO PARA VERIFICAÇÃO DE SEGURANÇA").FontSize(15).Bold();
                    col.Item().PaddingTop(4).Text("Autorização do responsável pelo domínio para execução de checagens técnicas externas de segurança")
                        .FontSize(9.5f).FontColor(Muted);

                    col.Item().PaddingTop(20).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                    {
                        void Campo(string label, string valor)
                        {
                            c.Item().PaddingBottom(8).Row(r =>
                            {
                                r.ConstantItem(130).Text(label).FontSize(8.5f).FontColor(Muted);
                                r.RelativeItem().Text(valor).FontSize(10).Bold();
                            });
                        }

                        Campo("EMPRESA AUTORIZANTE", empresaNome);
                        Campo("DOMÍNIO/ATIVO AUTORIZADO", dominioExibicao);
                        Campo("PRESTADOR AUTORIZADO", "Okta IA — comercial@okta-ia.com");
                    });

                    col.Item().PaddingTop(20).Text(
                        $"Eu, na qualidade de representante ou responsável técnico pela empresa acima identificada, declaro que sou responsável " +
                        $"pelo domínio {dominioExibicao} — ou possuo autorização expressa de quem é — e, nessa condição, autorizo a Okta IA a " +
                        "executar verificações técnicas de segurança sobre esse domínio, com a finalidade exclusiva de identificar vulnerabilidades " +
                        "e apoiar a melhoria da postura de segurança da empresa.")
                        .FontSize(9.5f).LineHeight(1.65f);

                    col.Item().PaddingTop(14).Text("ESCOPO AUTORIZADO").FontSize(9).Bold();
                    (string T, string D)[] escopo =
                    [
                        ("Certificado e protocolo TLS", "Validade, protocolo negociado e configuração do certificado público."),
                        ("Cabeçalhos HTTP de segurança", "Presença de HSTS, CSP, X-Content-Type-Options, X-Frame-Options e correlatos."),
                        ("Autenticação de e-mail (SPF/DMARC)", "Registros DNS públicos que evitam falsificação de remetente no domínio."),
                        ("Portas comumente exploradas", "Varredura de portas públicas conhecidas, com timeout curto e sem varredura de faixa completa."),
                    ];
                    col.Item().PaddingTop(6).Column(c =>
                    {
                        foreach (var e in escopo)
                        {
                            c.Item().PaddingTop(6).Row(r =>
                            {
                                r.ConstantItem(14).Text("•").FontSize(10).Bold().FontColor("#4D9BFF");
                                r.RelativeItem().Column(cc =>
                                {
                                    cc.Item().Text(e.T).FontSize(9).Bold();
                                    cc.Item().PaddingTop(1).Text(e.D).FontSize(8.5f).FontColor(Muted);
                                });
                            });
                        }
                    });

                    col.Item().PaddingTop(14).Text(
                        "As verificações são passivas e não intrusivas — não incluem tentativa de exploração de vulnerabilidade, ataque de negação " +
                        "de serviço ou acesso não autorizado a dados. Esta autorização é válida enquanto o ativo permanecer cadastrado como " +
                        "autorizado na plataforma Okta IA, podendo ser revogada a qualquer momento mediante solicitação por escrito ao e-mail " +
                        "comercial@okta-ia.com.")
                        .FontSize(9).FontColor(Muted).LineHeight(1.6f);

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        void Assinatura(string label)
                        {
                            row.RelativeItem().PaddingRight(20).Column(c =>
                            {
                                c.Item().PaddingBottom(28);
                                c.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                                c.Item().PaddingTop(4).Text(label).FontSize(8).FontColor(Muted);
                            });
                        }

                        Assinatura("Nome completo e cargo");
                        Assinatura("Assinatura e data");
                    });
                });

                page.Footer().PaddingTop(10).Column(col =>
                {
                    col.Item().LineHorizontal(0.7f).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(6).Text(
                        $"Documento gerado eletronicamente pela plataforma Okta IA em {geradoEm:dd/MM/yyyy HH:mm}. " +
                        "Válido como registro de autorização informada, mantido como evidência de conformidade (LGPD, art. 46).")
                        .FontSize(7.5f).FontColor(Muted);
                });
            });
        });

        return documento.GeneratePdf();
    }
}
