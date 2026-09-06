using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

/// <summary>
/// Como o monitoramento funciona, em caixas e setas — Estações → Agente/Ferramenta → Servidor de
/// segurança → Internet → L'okta IA.
///
/// ⚠️ ISTO EXPLICA O MECANISMO DO SERVIÇO, e não é <c>MapaDaArquitetura</c>. Aquele desenho mostra
/// o que foi AVALIADO no ambiente do cliente, camada por camada, colorido por risco — é dado do
/// diagnóstico. Este aqui é sempre o MESMO desenho, com duas variações fixas (implantar × já ter
/// ferramenta): explica o produto, não o cliente. Confundir os dois faria um desenho de risco
/// aparecer com cara de "como funciona", ou vice-versa.
///
/// ⚠️ "NUVEM" ENTRA COMO NOTA, NUNCA COMO CAIXA COLORIDA. Não se sabe, aqui, se o cliente usa
/// nuvem — afirmar isso com uma caixa no desenho seria inventar um fato sobre o ambiente dele.
/// </summary>
public static class FluxogramaDaSolucao
{
    private const string Fundo = "#F6F8FB";
    private const string Borda = "#D7E0EC";

    public static void Desenhar(ColumnDescriptor col, int maquinas, bool jaTemFerramenta,
        string? ferramentaExistente, bool hospedagemDoCliente,
        string muted = "#5A7191", string azul = "#0B45DD")
    {
        col.Item().PaddingTop(16).Text("COMO O MONITORAMENTO VAI FUNCIONAR").FontSize(9).Bold().FontColor(muted);

        col.Item().PaddingTop(8).Row(row =>
        {
            void Caixa(string titulo, string subtitulo, bool destaque = false)
            {
                row.ConstantItem(96).Height(72)
                    .Background(destaque ? "#EEF4FF" : Fundo)
                    .Border(1).BorderColor(destaque ? azul : Borda)
                    .Padding(7).Column(c =>
                    {
                        c.Item().Text(titulo).FontSize(8.5f).Bold()
                            .FontColor(destaque ? azul : "#1C2836").LineHeight(1.2f);
                        c.Item().PaddingTop(3).Text(subtitulo).FontSize(7).FontColor(muted).LineHeight(1.25f);
                    });
            }

            void Seta(string? rotulo = null)
            {
                row.ConstantItem(26).AlignMiddle().Column(c =>
                {
                    c.Item().AlignCenter().Text("→").FontSize(15).FontColor(muted);
                    if (rotulo is not null)
                    {
                        c.Item().AlignCenter().Text(rotulo).FontSize(6).FontColor(muted);
                    }
                });
            }

            Caixa("Estações e servidores", maquinas > 0 ? $"{maquinas} máquina(s)" : "quantidade a confirmar");

            if (jaTemFerramenta)
            {
                Seta("já monitoradas por");
                Caixa(string.IsNullOrWhiteSpace(ferramentaExistente) ? "Ferramenta já existente" : ferramentaExistente!,
                    "Ambiente do cliente");
                Seta("API");
                Caixa("Conector L'okta", "Só leitura, sem instalar nada");
            }
            else
            {
                Seta();
                Caixa("Agente instalado", "Um em cada máquina");
                Seta();
                Caixa("Servidor de segurança", hospedagemDoCliente ? "Wazuh · no ambiente do cliente" : "Wazuh · hospedado por nós",
                    destaque: true);
            }

            Seta("internet, TLS");
            Caixa("L'okta IA", "Painel, alertas e relatório mensal", destaque: true);
        });

        col.Item().PaddingTop(6).Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(muted).LineHeight(1.4f));
            t.Span(jaTemFerramenta
                ? "A leitura é feita pela API da própria ferramenta — nenhum agente novo é instalado nas máquinas."
                : "A comunicação com a internet é de saída (a máquina fala com o servidor, nunca o contrário) e cifrada.");
            t.Span(" Ambiente em nuvem (AWS, Azure, Google Cloud), quando existir, entra pelo mesmo caminho — "
                 + "o agente ou o conector lê de lá também, sem um desenho à parte.");
        });
    }
}
