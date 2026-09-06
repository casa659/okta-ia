using OktaIA.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

/// <summary>
/// A via do cliente do orçamento de monitoramento gerenciado, em PDF.
///
/// ⚠️ NÃO CONFUNDIR COM `PropostaComercialPdfService`. Aquele é a proposta de consultoria da
/// plataforma inteira — 15 módulos, ROI, cronograma de 90 dias. Este é o orçamento de UM serviço:
/// monitorar N máquinas por mês. Documentos diferentes, para conversas diferentes.
///
/// ⚠️ O QUE NUNCA ENTRA AQUI: custo direto e margem. Eles vivem na tela, na caixa marcada "só
/// nosso" — e um PDF é justamente o que se encaminha sem pensar. Basta um encaminhamento para o
/// cliente descobrir a sua margem, e essa conversa não tem volta.
/// </summary>
public class OrcamentoPdfService
{
    private readonly byte[] _iconMonoBranco;

    static OrcamentoPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public OrcamentoPdfService(IWebHostEnvironment env)
    {
        _iconMonoBranco = File.ReadAllBytes(Path.Combine(env.WebRootPath, "img", "brand", "simbolo-mono-branco.png"));
    }

    private const string Muted = "#5A7191";
    private const string Text = "#1C2836";
    private const string Azul = "#0B45DD";
    private const string Fundo = "#0B1220";

    public byte[] Gerar(OrcamentoMonitoramento o)
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
                        row.AutoItem().AlignMiddle().Text(o.Numero).FontSize(9).Bold().FontColor(Azul);
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Item().Text("PROPOSTA DE MONITORAMENTO GERENCIADO").FontSize(15).Bold();
                    col.Item().PaddingTop(3).Text("Medida técnica de segurança do art. 46 da LGPD, com evidência mensal")
                        .FontSize(9.5f).FontColor(Muted);

                    // ── Cliente ────────────────────────────────────────────────────────────
                    col.Item().PaddingTop(16).Background("#F3F6FB").Padding(12).Column(c =>
                    {
                        c.Item().Text(o.NomeEmpresa).FontSize(13).Bold();
                        var linha = new List<string>();
                        if (!string.IsNullOrWhiteSpace(o.Cnpj)) { linha.Add($"CNPJ {o.Cnpj}"); }
                        if (!string.IsNullOrWhiteSpace(o.Contato)) { linha.Add($"A/C {o.Contato}"); }
                        if (!string.IsNullOrWhiteSpace(o.Email)) { linha.Add(o.Email!); }
                        if (!string.IsNullOrWhiteSpace(o.Telefone)) { linha.Add(o.Telefone!); }
                        if (linha.Count > 0)
                        {
                            c.Item().PaddingTop(3).Text(string.Join("  ·  ", linha)).FontSize(9).FontColor(Muted);
                        }

                        // ⚠️ SÓ QUANDO É REVENDA. A ressalva vai DENTRO do arquivo, e não só na
                        // tela de quem gerou: este PDF é o que circula até quem vai pagar, e sem
                        // a frase o valor abaixo seria lido como o preço ao cliente final — que o
                        // parceiro é quem define, e a L'okta não tem por que saber qual é.
                        if (!string.IsNullOrWhiteSpace(o.ParceiroNome))
                        {
                            c.Item().PaddingTop(6).Text(t =>
                            {
                                t.Span("Proposta encaminhada por ").FontSize(8.5f).FontColor(Muted);
                                t.Span(o.ParceiroNome).FontSize(8.5f).Bold().FontColor(Muted);
                                t.Span(" — o valor abaixo é o repasse ao parceiro; o preço ao cliente final é definido por ele.")
                                    .FontSize(8.5f).FontColor(Muted);
                            });
                        }
                    });

                    // ── O escopo, em uma frase ─────────────────────────────────────────────
                    //
                    // ⚠️ DUAS FRASES, PORQUE SÃO DOIS SERVIÇOS (06/09/2026). Cliente que já opera
                    // uma ferramenta não recebe "inclui o servidor" — não há servidor novo, e
                    // prometer um seria vender uma instalação que não vai acontecer.
                    col.Item().PaddingTop(16).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.45f));
                        if (o.JaTemFerramenta)
                        {
                            t.Span("Administração, configuração e suporte contínuo de ");
                            t.Span($"{o.MaquinasTotal} máquina{(o.MaquinasTotal == 1 ? "" : "s")}").Bold();
                            t.Span(" já monitoradas por ");
                            t.Span(string.IsNullOrWhiteSpace(o.FerramentaExistente) ? "ferramenta própria" : o.FerramentaExistente!)
                                .Bold();
                            t.Span(", com cobertura em ");
                            t.Span(CalculadoraDeOrcamento.Rotulo(o.Cobertura)).Bold();
                            t.Span(". Sem instalação de servidor: a leitura é pela ferramenta que já existe.");
                        }
                        else
                        {
                            t.Span("Monitoramento contínuo de ");
                            t.Span($"{o.MaquinasTotal} máquina{(o.MaquinasTotal == 1 ? "" : "s")}").Bold();
                            t.Span(", com cobertura em ");
                            t.Span(CalculadoraDeOrcamento.Rotulo(o.Cobertura)).Bold();
                            t.Span(" e ");
                            t.Span($"{o.RetencaoDias} dias").Bold();
                            t.Span(" de histórico. ");
                            t.Span(o.HospedagemDoCliente
                                ? "O servidor de segurança fica na infraestrutura do cliente."
                                : "Inclui o servidor de segurança dedicado, gerenciado por nós.");
                        }
                    });

                    // ⚠️ COMO FUNCIONA, ANTES DO PREÇO. Pedido do dono: "o fluxograma mostrando
                    // como vai funcionar a estrutura". Mesmo desenho da proposta de LGPD — ver
                    // FluxogramaDaSolucao; explica o MECANISMO do serviço, não é o mapa de risco.
                    FluxogramaDaSolucao.Desenhar(col, o.MaquinasTotal, o.JaTemFerramenta,
                        o.FerramentaExistente, o.HospedagemDoCliente, Muted, Azul);

                    // ── Os dois números ────────────────────────────────────────────────────
                    col.Item().PaddingTop(18).Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                        {
                            c.Item().Text(o.JaTemFerramenta ? "ONBOARDING · UMA VEZ" : "IMPLANTAÇÃO · UMA VEZ")
                                .FontSize(7.5f).Bold().FontColor(Muted);
                            c.Item().PaddingTop(4).Text($"R$ {o.ValorImplantacao:N2}").FontSize(19).Bold();
                            c.Item().PaddingTop(3).Text(o.JaTemFerramenta
                                    ? "Conexão com a ferramenta já existente e conferência inicial."
                                    : "Levantamento, servidor, agentes e o relatório inicial de conformidade.")
                                .FontSize(8).FontColor(Muted);
                        });

                        row.ConstantItem(12);

                        row.RelativeItem().Border(1).BorderColor(Azul).Background("#F4F7FE").Padding(12).Column(c =>
                        {
                            c.Item().Text(o.JaTemFerramenta ? "ADMINISTRAÇÃO MENSAL" : "MENSALIDADE")
                                .FontSize(7.5f).Bold().FontColor(Azul);
                            c.Item().PaddingTop(4).Text($"R$ {o.ValorMensal:N2}").FontSize(19).Bold();
                            c.Item().PaddingTop(3).Text("Monitoramento, triagem de alertas e relatório mensal.")
                                .FontSize(8).FontColor(Muted);
                        });
                    });

                    // ⚠️ ITEM A ITEM — pedido do dono: "detalhar o orçamento... valor por
                    // estação". Vem do JSON gravado no momento do cálculo, nunca recalculado
                    // aqui: ver o comentário de `ItensDeCustoJson` no modelo.
                    var itensDeCusto = string.IsNullOrWhiteSpace(o.ItensDeCustoJson) ? null
                        : System.Text.Json.JsonSerializer.Deserialize<List<CalculadoraDeOrcamento.ItemDeCusto>>(o.ItensDeCustoJson);
                    if (itensDeCusto is { Count: > 0 })
                    {
                        DetalhamentoDeCustoPdf.Desenhar(col, o.JaTemFerramenta ? "ONBOARDING · DETALHAMENTO" : "IMPLANTAÇÃO · DETALHAMENTO",
                            itensDeCusto.Where(i => i.Grupo == "Implantação").ToList(),
                            itensDeCusto.Where(i => i.Grupo == "Implantação").Sum(i => i.Valor),
                            o.ValorImplantacao, Muted, Azul);
                        DetalhamentoDeCustoPdf.Desenhar(col, o.JaTemFerramenta ? "ADMINISTRAÇÃO MENSAL · DETALHAMENTO" : "MENSALIDADE · DETALHAMENTO",
                            itensDeCusto.Where(i => i.Grupo == "Mensalidade").ToList(),
                            itensDeCusto.Where(i => i.Grupo == "Mensalidade").Sum(i => i.Valor),
                            o.ValorMensal, Muted, Azul);
                    }

                    // ⚠️ O FECHO EXPLICA A CONSEQUÊNCIA DE FECHAR — pedido do dono: "explicando
                    // que se implantar, estaremos dentro do que a lei exige". Preciso no que
                    // promete: "atende ao art. 46" é conferível, "em conformidade com a LGPD" sem
                    // o artigo é a frase que a ressalva de SecaoLeiLgpd proíbe.
                    col.Item().PaddingTop(10).Background("#EEF4FF").Padding(11).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(9).LineHeight(1.5f));
                        t.Span(o.JaTemFerramenta ? "Ao contratar esta administração, " : "Ao implantar este serviço, ");
                        t.Span($"{o.NomeEmpresa} passa a ter em operação, com evidência mensal, a medida "
                             + "técnica de segurança que o ").FontColor("#1C2836");
                        t.Span("art. 46 da LGPD").Bold();
                        t.Span(" exige.").FontColor("#1C2836");
                    });

                    if (o.ValidaAte is { } val)
                    {
                        col.Item().PaddingTop(8).Text(t =>
                        {
                            t.Span("Proposta válida até ").FontSize(9).FontColor(Muted);
                            t.Span(val.ToString("dd/MM/yyyy")).FontSize(9).Bold();
                            t.Span(". Valores em reais, impostos inclusos conforme regime da contratada.")
                                .FontSize(9).FontColor(Muted);
                        });
                    }

                    // ── O que está incluído ────────────────────────────────────────────────
                    col.Item().PaddingTop(20).Text("O QUE ESTÁ INCLUÍDO").FontSize(9).Bold().FontColor(Muted);
                    col.Item().PaddingTop(6).Column(c =>
                    {
                        foreach (var item in Incluidos(o))
                        {
                            c.Item().PaddingBottom(4).Row(r =>
                            {
                                r.ConstantItem(12).Text("•").FontColor(Azul).Bold();
                                r.RelativeItem().Text(item).FontSize(9.5f).LineHeight(1.35f);
                            });
                        }
                    });

                    // ── O parque, para não haver dúvida do que foi orçado ──────────────────
                    col.Item().PaddingTop(18).Text("O PARQUE ORÇADO").FontSize(9).Bold().FontColor(Muted);
                    col.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });

                        void Linha(string rotulo, string valor, bool forte = false)
                        {
                            var celula = t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5);
                            var texto = celula.Text(rotulo).FontSize(9.5f);
                            if (forte) { texto.SemiBold(); }

                            var celula2 = t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5);
                            var texto2 = celula2.AlignRight().Text(valor).FontSize(9.5f);
                            if (forte) { texto2.SemiBold(); }
                        }

                        Linha("Estações Windows", o.EstacoesWindows.ToString());
                        Linha("Estações Linux / macOS", o.EstacoesOutras.ToString());
                        Linha("Servidores", o.Servidores.ToString());
                        if (o.ServidoresExpostos > 0)
                        {
                            Linha("Destes, expostos à internet", o.ServidoresExpostos.ToString());
                        }
                        Linha("Total monitorado", o.MaquinasTotal.ToString(), forte: true);
                    });

                    // ⚠️ O CONTEÚDO DA LEI MORA EM `SecaoLeiLgpd` (06/09/2026), reusado por
                    // `PropostaLgpdPdfService`. Editar aqui edita os dois documentos — é
                    // exatamente o que se quer: já pegou uma correção real (art. 37 → 49) e uma
                    // segunda cópia teria envelhecido sozinha.
                    SecaoLeiLgpd.Desenhar(col, o.TrataDadosDeCriancas, Muted, Azul);

                    if (!string.IsNullOrWhiteSpace(o.Observacoes))
                    {
                        col.Item().PaddingTop(18).Text("OBSERVAÇÕES").FontSize(9).Bold().FontColor(Muted);
                        col.Item().PaddingTop(5).Text(o.Observacoes).FontSize(9.5f).LineHeight(1.4f);
                    }

                    // ⚠️ Dito no documento, e não só na conversa: o serviço OBSERVA e AVISA. Um
                    // cliente que entende "monitoramento" como "vocês impedem o ataque" cobra
                    // exatamente isso no primeiro incidente — e a hora de alinhar é agora, por
                    // escrito, não depois.
                    col.Item().PaddingTop(20).Background("#FFF8EE").Padding(10).Text(t =>
                    {
                        t.Span("O que este serviço faz: ").FontSize(8.5f).Bold();
                        t.Span("detecta, registra e avisa. Ele não substitui antivírus, firewall ou "
                             + "backup, e não bloqueia ataques automaticamente — dá visibilidade do que "
                             + "acontece nas máquinas e o caminho para agir.").FontSize(8.5f).FontColor(Muted);
                    });
                });

                page.Footer().PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"L'okta IA · {o.Numero} · gerado em {agora:dd/MM/yyyy HH:mm}")
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

    private static List<string> Incluidos(OrcamentoMonitoramento o)
    {
        // ⚠️ A LISTA MUDA QUANDO JÁ HÁ FERRAMENTA (06/09/2026). "Agente instalado" e "servidor
        // dedicado" descrevem uma implantação nossa; com ferramenta existente não há agente novo
        // nem servidor novo — a leitura é pela ferramenta do cliente, e listar os dois itens
        // prometeria uma instalação que não vai acontecer.
        var itens = o.JaTemFerramenta
            ? new List<string>
              {
                  $"Leitura contínua de {(string.IsNullOrWhiteSpace(o.FerramentaExistente) ? "a ferramenta já em uso" : o.FerramentaExistente)}, sem instalar nada novo",
                  "Alertas normalizados numa tela só, com triagem",
                  "Relatório mensal de conformidade e incidentes — a evidência que o art. 6º, X da LGPD "
                      + "exige demonstrar",
              }
            : new List<string>
              {
                  "Agente instalado em cada máquina, com inventário de programas e monitoramento de integridade de arquivos",
                  "Auditoria de configuração contra o benchmark CIS do sistema de cada máquina",
                  "Detecção de vulnerabilidades conhecidas nos programas instalados",
                  "Alertas de todas as máquinas normalizados numa tela só, com triagem",
                  "Relatório mensal de conformidade e incidentes — a evidência que o art. 6º, X da LGPD "
                      + "exige demonstrar",
              };

        if (!o.JaTemFerramenta && !o.HospedagemDoCliente)
        {
            itens.Add("Servidor de segurança dedicado, com certificado, firewall fechado e atualizações");
        }

        if (o.Cobertura != CoberturaOrcamento.Comercial)
        {
            itens.Add($"Cobertura em {CalculadoraDeOrcamento.Rotulo(o.Cobertura)}");
        }

        if (!string.IsNullOrWhiteSpace(o.Conformidade))
        {
            itens.Add($"Atenção à exigência declarada: {o.Conformidade}");
        }

        return itens;
    }
}
