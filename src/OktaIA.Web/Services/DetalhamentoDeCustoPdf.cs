using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

/// <summary>
/// O preço, item a item — "de onde vem esse número". Uma cópia só, usada pelo orçamento e pela
/// proposta de LGPD.
///
/// ⚠️ NUNCA RECEBE CUSTO NEM MARGEM. Só <see cref="CalculadoraDeOrcamento.ItemDeCusto"/>, que por
/// definição só carrega preço de venda (ver o comentário do record). Não há filtro aqui porque não
/// precisa haver: o que entra é seguro por construção.
/// </summary>
public static class DetalhamentoDeCustoPdf
{
    /// <param name="titulo">"IMPLANTAÇÃO · DETALHAMENTO" ou "MENSALIDADE · DETALHAMENTO".</param>
    /// <param name="itens">Só os itens DESTE grupo — o chamador já filtrou por Grupo.</param>
    /// <param name="subtotalCalculado">Soma dos itens — é o que a conta deu.</param>
    /// <param name="valorFechado">
    /// O que vale de fato. Igual ao subtotal na maioria dos casos; diferente quando alguém fechou
    /// à mão — aí uma linha extra explica a diferença, em vez de deixar a soma não bater.
    /// </param>
    public static void Desenhar(ColumnDescriptor col, string titulo,
        List<CalculadoraDeOrcamento.ItemDeCusto> itens, decimal subtotalCalculado, decimal valorFechado,
        string muted = "#5A7191", string azul = "#0B45DD")
    {
        if (itens.Count == 0) { return; }

        col.Item().PaddingTop(10).Text(titulo).FontSize(8).Bold().FontColor(muted).LetterSpacing(0.04f);

        col.Item().PaddingTop(4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Column(c =>
        {
            foreach (var item in itens)
            {
                c.Item().PaddingVertical(4).PaddingHorizontal(6).Row(row =>
                {
                    row.RelativeItem().Text(item.Descricao).FontSize(9).LineHeight(1.3f);
                    row.AutoItem().PaddingLeft(8)
                        .Text(item.Valor == 0
                            ? "incluído"
                            : $"R$ {item.Valor:N2}")
                        .FontSize(9).Bold().FontColor(item.Valor == 0 ? muted : "#1C2836");
                });
            }

            c.Item().PaddingTop(4).PaddingHorizontal(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            c.Item().PaddingTop(4).PaddingHorizontal(6).Row(row =>
            {
                row.RelativeItem().Text("Subtotal").FontSize(9).Bold();
                row.AutoItem().Text($"R$ {subtotalCalculado:N2}").FontSize(9).Bold();
            });

            // ⚠️ SÓ APARECE QUANDO DIVERGE. Um valor fechado à mão que bate com a conta não
            // precisa de explicação; um que diverge precisa, ou o cliente soma os itens, não bate
            // com o total do topo, e desconfia do documento inteiro — com razão.
            if (Math.Round(valorFechado, 2) != Math.Round(subtotalCalculado, 2))
            {
                c.Item().PaddingTop(2).PaddingHorizontal(6).Row(row =>
                {
                    row.RelativeItem().Text("Condição especial desta proposta").FontSize(9).Bold().FontColor(azul);
                    row.AutoItem().Text($"R$ {valorFechado:N2}").FontSize(9).Bold().FontColor(azul);
                });
            }
        });
    }
}
