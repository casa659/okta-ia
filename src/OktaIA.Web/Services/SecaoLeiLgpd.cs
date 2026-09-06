using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

/// <summary>
/// A página "O QUE A LEI EXIGE" — artigo por artigo, com o que o serviço de monitoramento entrega
/// ao lado, mais o parágrafo do art. 14 quando cabe e a ressalva de que isto não é conformidade.
///
/// ⚠️ UMA CÓPIA SÓ. Até 06/09/2026 este texto vivia dentro de `OrcamentoPdfService`; quando
/// `PropostaLgpdPdfService` passou a precisar do mesmo conteúdo, copiar o bloco criaria dois
/// lugares para corrigir o dia em que um artigo estiver errado — e foi exatamente citando o
/// artigo errado (37 em vez de 49) que este texto já pegou uma correção real. Extraído para cá,
/// os dois documentos citam a lei do mesmo jeito, sempre.
///
/// ⚠️ CITA A LEI PELO ARTIGO, nunca "a LGPD manda". Artigo com número é conferível: o cliente abre
/// a lei e vê. E NUNCA PROMETE CONFORMIDADE — monitoramento é uma medida entre várias, e dizer que
/// ele deixa a empresa "em conformidade" seria vender o que não se entrega. A ressalva no fim é
/// obrigatória, não estilo.
/// </summary>
public static class SecaoLeiLgpd
{
    public static void Desenhar(ColumnDescriptor col, bool trataDadosDeCriancas,
        string muted = "#5A7191", string azul = "#0B45DD")
    {
        col.Item().PageBreak();

        col.Item().Text("O QUE A LEI EXIGE").FontSize(14).Bold();
        col.Item().PaddingTop(3).Text("Lei nº 13.709/2018 — Lei Geral de Proteção de Dados Pessoais")
            .FontSize(9.5f).FontColor(muted);

        col.Item().PaddingTop(14).Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(10).LineHeight(1.5f));
            t.Span("A LGPD não pede boa intenção: ela impõe ");
            t.Span("obrigações verificáveis").Bold();
            t.Span(" a quem trata dados pessoais — e a responsabilidade é do ");
            t.Span("controlador").Bold();
            t.Span(", ou seja, da sua instituição, mesmo quando o sistema é de terceiro.");
        });

        col.Item().PaddingTop(16).Table(t =>
        {
            t.ColumnsDefinition(c => { c.ConstantColumn(74); c.RelativeColumn(5); });

            void Artigo(string artigo, string exige, string entrega)
            {
                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(8)
                    .PaddingRight(8).Text(artigo).FontSize(9).Bold().FontColor(azul);

                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(8)
                    .Column(c =>
                    {
                        c.Item().Text(exige).FontSize(9.5f).LineHeight(1.4f);
                        c.Item().PaddingTop(3).Text(tt =>
                        {
                            tt.Span("→ ").FontSize(9).Bold().FontColor("#12855C");
                            tt.Span(entrega).FontSize(9).FontColor("#12855C").LineHeight(1.35f);
                        });
                    });
            }

            Artigo("Art. 46",
                "Obriga o controlador e o operador a adotar medidas de segurança, técnicas e "
                + "administrativas, aptas a proteger os dados pessoais de acessos não autorizados "
                + "e de situações acidentais ou ilícitas de destruição, perda, alteração, "
                + "comunicação ou difusão.",
                "O monitoramento contínuo das máquinas é uma dessas medidas técnicas — e, ao "
                + "contrário de uma política escrita, ela funciona sozinha todos os dias.");

            Artigo("Art. 48",
                "Obriga o controlador a comunicar à ANPD e ao titular a ocorrência de incidente "
                + "de segurança que possa acarretar risco ou dano relevante. A Resolução CD/ANPD "
                + "nº 15/2024 fixou o prazo em 3 dias úteis, contados do conhecimento do incidente.",
                "Não se comunica um incidente que não foi detectado. Sem monitoramento, o prazo "
                + "de 3 dias começa a correr no dia em que alguém descobre por acaso.");

            Artigo("Art. 6º, X",
                "Princípio da responsabilização e prestação de contas: o agente deve demonstrar a "
                + "adoção de medidas eficazes e capazes de comprovar a observância das normas de "
                + "proteção de dados.",
                "O relatório mensal é essa demonstração — com data, máquinas e achados. Medida "
                + "sem registro é medida que não se prova.");

            Artigo("Art. 49",
                "Os sistemas utilizados para o tratamento de dados pessoais devem ser estruturados "
                + "de forma a atender aos requisitos de segurança, aos padrões de boas práticas e "
                + "de governança e aos princípios gerais previstos na Lei.",
                "O monitoramento mede se os sistemas estão configurados conforme boas práticas "
                + "reconhecidas (benchmark CIS) — e aponta, item a item, o que está fora.");

            Artigo("Art. 52",
                "Sanções aplicáveis pela ANPD: advertência, multa simples de até 2% do "
                + "faturamento no Brasil, limitada a R$ 50 milhões por infração, publicização da "
                + "infração, bloqueio e eliminação dos dados.",
                "A sanção considera a adoção de política de boas práticas e a pronta adoção de "
                + "medidas corretivas (art. 52, §1º). O que você fez antes do incidente conta na "
                + "dosimetria.");
        });

        if (trataDadosDeCriancas)
        {
            col.Item().PaddingTop(16).Background("#F4F7FE").Border(1).BorderColor(azul)
                .Padding(12).Column(c =>
            {
                c.Item().Text("DADOS DE CRIANÇAS E ADOLESCENTES · ART. 14")
                    .FontSize(8.5f).Bold().FontColor(azul);
                c.Item().PaddingTop(6).Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(9.5f).LineHeight(1.45f));
                    t.Span("O tratamento de dados de crianças e adolescentes deve ser feito ");
                    t.Span("em seu melhor interesse").Bold();
                    t.Span(", e o de dados de crianças exige ");
                    t.Span("consentimento específico e em destaque").Bold();
                    t.Span(" de um dos pais ou responsável legal. É um regime mais rígido que o "
                         + "comum — e, numa instituição de ensino, praticamente todo o cadastro "
                         + "está dentro dele: nome, data de nascimento, filiação, endereço, "
                         + "saúde, imagem e desempenho escolar.");
                });
                c.Item().PaddingTop(6).Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(9.5f).LineHeight(1.45f));
                    t.Span("Na prática: um incidente que exponha o cadastro de alunos atinge "
                         + "titulares vulneráveis, em volume, e com dados que não se trocam como "
                         + "se troca uma senha. É o cenário em que a ausência de medida técnica "
                         + "pesa mais.");
                });
            });
        }

        col.Item().PaddingTop(16).Background("#FFF8EE").Padding(11).Text(t =>
        {
            t.Span("Importante: ").FontSize(8.5f).Bold();
            t.Span("este documento não é parecer jurídico, e o monitoramento não torna a "
                 + "instituição \"em conformidade com a LGPD\" por si só. Conformidade envolve "
                 + "base legal, consentimento, contratos com operadores, política de retenção, "
                 + "atendimento ao titular e Encarregado nomeado. O que este serviço entrega é "
                 + "uma das medidas técnicas do art. 46 — e a evidência de que ela existe.")
                .FontSize(8.5f).FontColor(muted);
        });
    }
}
