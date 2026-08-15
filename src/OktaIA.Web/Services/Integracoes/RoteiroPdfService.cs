using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services.Integracoes;

/// <summary>
/// Gera os dois roteiros de implantação em PDF a partir do <see cref="CatalogoDeRoteiros"/>.
///
/// São dois públicos e dois documentos, de propósito. O do CLIENTE fala do produto dele e do que ele
/// provisiona — vai por e-mail antes da reunião técnica. O INTERNO fala da nossa plataforma e do que
/// o técnico executa — nunca deve ser enviado ao cliente, porque descreve a nossa operação.
///
/// Documento de fabricante não implementado sai com tarja: sem isso, alguém enviaria a um cliente o
/// passo a passo de uma integração que ainda não existe.
/// </summary>
public class RoteiroPdfService
{
    private readonly byte[] _icone;

    static RoteiroPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public RoteiroPdfService(IWebHostEnvironment env)
    {
        _icone = File.ReadAllBytes(Path.Combine(env.WebRootPath, "img", "brand", "simbolo-mono-branco.png"));
    }

    private const string Muted = "#5A7191";
    private const string Text = "#1C2836";
    private const string Azul = "#4D9BFF";
    private const string Laranja = "#A2400B";

    public byte[] GerarParaCliente(RoteiroDeImplantacao r, string? empresaNome) => Gerar(r, empresaNome, cliente: true);

    public byte[] GerarParaTecnico(RoteiroDeImplantacao r, string? empresaNome) => Gerar(r, empresaNome, cliente: false);

    private byte[] Gerar(RoteiroDeImplantacao r, string? empresaNome, bool cliente)
    {
        var agora = DateTimeOffset.Now;

        var doc = Document.Create(container =>
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
                            .Padding(5).Image(_icone);
                        row.RelativeItem().PaddingLeft(9).AlignMiddle().Text(t =>
                        {
                            t.Span("L'okta ").FontSize(13).Bold().FontColor("#0B1220");
                            t.Span("IA").FontSize(13).Bold().FontColor(Azul);
                        });
                        row.AutoItem().AlignMiddle().Text($"Gerado em {agora:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Muted);
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Item().Text(cliente
                        ? $"CONECTAR {r.Fabricante.ToUpperInvariant()} À L'OKTA IA"
                        : $"IMPLANTAÇÃO — {r.Fabricante.ToUpperInvariant()}").FontSize(15).Bold();

                    col.Item().PaddingTop(4).Text(cliente
                        ? "O que a sua equipe precisa provisionar e o que enviar para o time técnico"
                        : "Roteiro interno de execução · não enviar ao cliente")
                        .FontSize(9.5f).FontColor(Muted);

                    if (!cliente)
                    {
                        col.Item().PaddingTop(10).Background("#FBEDE3").Border(1).BorderColor(Laranja).Padding(9)
                            .Text("DOCUMENTO INTERNO. Descreve a operação da plataforma e não deve ser enviado ao cliente.")
                            .FontSize(8.5f).Bold().FontColor(Laranja);
                    }

                    if (!r.Implementado)
                    {
                        col.Item().PaddingTop(8).Background("#FBEDE3").Border(1).BorderColor(Laranja).Padding(9)
                            .Text("CONECTOR AINDA NÃO IMPLEMENTADO NA PLATAFORMA. Este roteiro serve ao levantamento e à preparação comercial — ainda não existe tela onde instalá-lo.")
                            .FontSize(8.5f).Bold().FontColor(Laranja);
                    }

                    // Ficha: o que muda de fabricante para fabricante e define todo o resto.
                    col.Item().PaddingTop(14).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(11).Column(c =>
                    {
                        void Campo(string k, string v)
                        {
                            c.Item().PaddingBottom(6).Row(row =>
                            {
                                row.ConstantItem(150).Text(k).FontSize(8.5f).FontColor(Muted);
                                row.RelativeItem().Text(v).FontSize(9.5f);
                            });
                        }

                        if (!string.IsNullOrWhiteSpace(empresaNome)) { Campo("EMPRESA", empresaNome!); }
                        Campo("FABRICANTE", $"{r.Fabricante} · {r.Categoria}");
                        Campo("MODO DE ACESSO", r.Modo == ModoDeAcesso.InstaladoNoCliente
                            ? "Produto instalado no ambiente do cliente — exige endereço e liberação de rede"
                            : "Produto na nuvem do fabricante — sem liberação de firewall; exige consentimento");
                        Campo("ONDE ESTÁ O ALERTA", r.OndeEstaOAlerta);
                    });

                    void Secao(string titulo, IReadOnlyList<string> itens, bool numerado)
                    {
                        if (itens.Count == 0) { return; }

                        col.Item().PaddingTop(16).Text(titulo).FontSize(10.5f).Bold();
                        col.Item().PaddingTop(6).Column(c =>
                        {
                            for (var i = 0; i < itens.Count; i++)
                            {
                                var marcador = numerado ? $"{i + 1}." : "•";
                                c.Item().PaddingBottom(6).Row(row =>
                                {
                                    row.ConstantItem(18).Text(marcador).FontSize(9.5f).Bold().FontColor(Azul);
                                    row.RelativeItem().Text(itens[i]).FontSize(9.5f).LineHeight(1.5f);
                                });
                            }
                        });
                    }

                    if (cliente)
                    {
                        col.Item().PaddingTop(14).Text(
                            "A L'okta IA vai LER os alertas que a sua ferramenta já gera. Nada é instalado no seu ambiente, " +
                            "nada é alterado, e o acesso é somente de leitura — criado por você, e revogável por você a qualquer momento.")
                            .FontSize(9.5f).LineHeight(1.6f);

                        Secao("O QUE A SUA EQUIPE PROVISIONA", r.PassosCliente, numerado: true);
                        Secao("O QUE ENVIAR AO TIME TÉCNICO DA L'OKTA IA", r.InformacoesParaEnviar, numerado: false);

                        col.Item().PaddingTop(16).Background("#EDF3F3").Padding(10).Text(
                            "Envie estes dados pelo canal combinado com o time técnico. A senha é gravada cifrada e nunca é " +
                            "reexibida na plataforma; a conta de serviço pode ser desativada por você a qualquer momento, o que " +
                            "encerra o acesso imediatamente.")
                            .FontSize(9).LineHeight(1.55f);
                    }
                    else
                    {
                        Secao("O QUE PEDIR AO CLIENTE", r.PassosCliente, numerado: true);
                        Secao("O QUE O CLIENTE DEVE DEVOLVER", r.InformacoesParaEnviar, numerado: false);
                        Secao("EXECUÇÃO NA PLATAFORMA", r.PassosTecnico, numerado: true);
                    }

                    Secao(cliente ? "OBSERVAÇÕES" : "ARMADILHAS CONHECIDAS", r.Observacoes, numerado: false);
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text(cliente
                            ? "L'okta IA · dúvidas sobre qualquer item podem ser tratadas antes de qualquer provisionamento"
                            : "L'okta IA · documento interno de implantação")
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
        });

        return doc.GeneratePdf();
    }
}
