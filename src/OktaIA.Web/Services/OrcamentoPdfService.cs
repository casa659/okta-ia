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
                    col.Item().PaddingTop(16).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10.5f).LineHeight(1.45f));
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
                    });

                    // ── Os dois números ────────────────────────────────────────────────────
                    col.Item().PaddingTop(18).Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                        {
                            c.Item().Text("IMPLANTAÇÃO · UMA VEZ").FontSize(7.5f).Bold().FontColor(Muted);
                            c.Item().PaddingTop(4).Text($"R$ {o.ValorImplantacao:N2}").FontSize(19).Bold();
                            c.Item().PaddingTop(3).Text("Levantamento, servidor, agentes e o relatório inicial de conformidade.")
                                .FontSize(8).FontColor(Muted);
                        });

                        row.ConstantItem(12);

                        row.RelativeItem().Border(1).BorderColor(Azul).Background("#F4F7FE").Padding(12).Column(c =>
                        {
                            c.Item().Text("MENSALIDADE").FontSize(7.5f).Bold().FontColor(Azul);
                            c.Item().PaddingTop(4).Text($"R$ {o.ValorMensal:N2}").FontSize(19).Bold();
                            c.Item().PaddingTop(3).Text("Monitoramento, triagem de alertas e relatório mensal.")
                                .FontSize(8).FontColor(Muted);
                        });
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

                    // ══ A LEI ═══════════════════════════════════════════════════════════
                    //
                    // ⚠️ ESTA SEÇÃO É O ARGUMENTO DE VENDA, e não um enfeite jurídico. Para
                    // escola, clínica e escritório, monitoramento não se vende como "segurança"
                    // — vende-se como a medida técnica que a lei EXIGE e como a prova de que ela
                    // foi adotada. Quem decide a compra é o diretor, não o técnico.
                    //
                    // ⚠️ CITA A LEI PELO ARTIGO, e nunca "a LGPD manda". Artigo com número é
                    // conferível: o cliente abre a lei e vê. Afirmação genérica sobre a lei, num
                    // documento comercial, é o que faz um advogado do outro lado desmontar a
                    // proposta inteira na primeira reunião.
                    //
                    // ⚠️ E NÃO PROMETE CONFORMIDADE. Monitoramento é UMA medida entre várias;
                    // dizer que ele deixa a empresa "em conformidade com a LGPD" seria vender o
                    // que não se entrega — e criaria, para nós, a responsabilidade de um parecer
                    // que ninguém emitiu. A ressalva no fim da seção é obrigatória.
                    col.Item().PageBreak();

                    col.Item().Text("O QUE A LEI EXIGE").FontSize(14).Bold();
                    col.Item().PaddingTop(3).Text("Lei nº 13.709/2018 — Lei Geral de Proteção de Dados Pessoais")
                        .FontSize(9.5f).FontColor(Muted);

                    col.Item().PaddingTop(14).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10).LineHeight(1.5f));
                        t.Span("A LGPD não pede boa intenção: ela impõe ");
                        t.Span("obrigações verificáveis").Bold();
                        t.Span(" a quem trata dados pessoais — e a responsabilidade é do ");
                        t.Span("controlador").Bold();
                        t.Span(", ou seja, da sua instituição, mesmo quando o sistema é de terceiro.");
                    });

                    // ── Artigo a artigo, com o que o serviço entrega ao lado ────────────
                    col.Item().PaddingTop(16).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(74); c.RelativeColumn(5); });

                        void Artigo(string artigo, string exige, string entrega)
                        {
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(8)
                                .PaddingRight(8).Text(artigo).FontSize(9).Bold().FontColor(Azul);

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
                            "Obriga o controlador e o operador a adotar medidas de segurança, "
                            + "técnicas e administrativas, aptas a proteger os dados pessoais de "
                            + "acessos não autorizados e de situações acidentais ou ilícitas de "
                            + "destruição, perda, alteração, comunicação ou difusão.",
                            "O monitoramento contínuo das máquinas é uma dessas medidas técnicas — "
                            + "e, ao contrário de uma política escrita, ela funciona sozinha todos os dias.");

                        Artigo("Art. 48",
                            "Obriga o controlador a comunicar à ANPD e ao titular a ocorrência de "
                            + "incidente de segurança que possa acarretar risco ou dano relevante. "
                            + "A Resolução CD/ANPD nº 15/2024 fixou o prazo em 3 dias úteis, "
                            + "contados do conhecimento do incidente.",
                            "Não se comunica um incidente que não foi detectado. Sem monitoramento, "
                            + "o prazo de 3 dias começa a correr no dia em que alguém descobre por acaso.");

                        Artigo("Art. 6º, X",
                            "Princípio da responsabilização e prestação de contas: o agente deve "
                            + "demonstrar a adoção de medidas eficazes e capazes de comprovar a "
                            + "observância das normas de proteção de dados.",
                            "O relatório mensal é essa demonstração — com data, máquinas e achados. "
                            + "Medida sem registro é medida que não se prova.");

                        // ⚠️ AQUI ESTAVA O ART. 37, E ERA ERRADO. Art. 37 é o registro das
                        // OPERAÇÕES DE TRATAMENTO — quais dados, para qual finalidade, com qual
                        // base legal, por quanto tempo. Isso é o inventário de dados da
                        // instituição, e um log de alertas de segurança não é isso. Citar o
                        // artigo errado num documento comercial é o que faz o advogado do outro
                        // lado desmontar a proposta inteira — e com razão. O artigo que de fato
                        // fala de SISTEMAS é o 49.
                        Artigo("Art. 49",
                            "Os sistemas utilizados para o tratamento de dados pessoais devem ser "
                            + "estruturados de forma a atender aos requisitos de segurança, aos "
                            + "padrões de boas práticas e de governança e aos princípios gerais "
                            + "previstos na Lei.",
                            "O monitoramento mede se os sistemas estão configurados conforme boas "
                            + "práticas reconhecidas (benchmark CIS) — e aponta, item a item, o que "
                            + "está fora.");

                        Artigo("Art. 52",
                            "Sanções aplicáveis pela ANPD: advertência, multa simples de até 2% do "
                            + "faturamento no Brasil, limitada a R$ 50 milhões por infração, "
                            + "publicização da infração, bloqueio e eliminação dos dados.",
                            "A sanção considera a adoção de política de boas práticas e a pronta "
                            + "adoção de medidas corretivas (art. 52, §1º). O que você fez antes do "
                            + "incidente conta na dosimetria.");
                    });

                    if (o.TrataDadosDeCriancas)
                    {
                        // ⚠️ Só quando marcado. Num escritório de advocacia este parágrafo seria
                        // ruído; numa escola é o parágrafo mais importante do documento.
                        col.Item().PaddingTop(16).Background("#F4F7FE").Border(1).BorderColor(Azul)
                            .Padding(12).Column(c =>
                        {
                            c.Item().Text("DADOS DE CRIANÇAS E ADOLESCENTES · ART. 14")
                                .FontSize(8.5f).Bold().FontColor(Azul);
                            c.Item().PaddingTop(6).Text(t =>
                            {
                                t.DefaultTextStyle(x => x.FontSize(9.5f).LineHeight(1.45f));
                                t.Span("O tratamento de dados de crianças e adolescentes deve ser feito ");
                                t.Span("em seu melhor interesse").Bold();
                                t.Span(", e o de dados de crianças exige ");
                                t.Span("consentimento específico e em destaque").Bold();
                                t.Span(" de um dos pais ou responsável legal. É um regime mais rígido "
                                     + "que o comum — e, numa instituição de ensino, praticamente "
                                     + "todo o cadastro está dentro dele: nome, data de nascimento, "
                                     + "filiação, endereço, saúde, imagem e desempenho escolar.");
                            });
                            c.Item().PaddingTop(6).Text(t =>
                            {
                                t.DefaultTextStyle(x => x.FontSize(9.5f).LineHeight(1.45f));
                                t.Span("Na prática: um incidente que exponha o cadastro de alunos "
                                     + "atinge titulares vulneráveis, em volume, e com dados que "
                                     + "não se trocam como se troca uma senha. É o cenário em que "
                                     + "a ausência de medida técnica pesa mais.");
                            });
                        });
                    }

                    // ⚠️ A RESSALVA NÃO É LETRA MIÚDA — é o que separa uma proposta honesta de
                    // uma promessa que não se cumpre. Ela também protege quem assina.
                    col.Item().PaddingTop(16).Background("#FFF8EE").Padding(11).Text(t =>
                    {
                        t.Span("Importante: ").FontSize(8.5f).Bold();
                        t.Span("este documento não é parecer jurídico, e o monitoramento não torna "
                             + "a instituição \"em conformidade com a LGPD\" por si só. Conformidade "
                             + "envolve base legal, consentimento, contratos com operadores, "
                             + "política de retenção, atendimento ao titular e Encarregado nomeado. "
                             + "O que este serviço entrega é uma das medidas técnicas do art. 46 — "
                             + "e a evidência de que ela existe.")
                            .FontSize(8.5f).FontColor(Muted);
                    });

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
        var itens = new List<string>
        {
            "Agente instalado em cada máquina, com inventário de programas e monitoramento de integridade de arquivos",
            "Auditoria de configuração contra o benchmark CIS do sistema de cada máquina",
            "Detecção de vulnerabilidades conhecidas nos programas instalados",
            "Alertas de todas as máquinas normalizados numa tela só, com triagem",
            "Relatório mensal de conformidade e incidentes — a evidência que o art. 6º, X da LGPD "
                + "exige demonstrar",
        };

        if (!o.HospedagemDoCliente)
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
