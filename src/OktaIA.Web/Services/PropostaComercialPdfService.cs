using OktaIA.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

// Gera a "Proposta Consultoria" — documento comercial de venda (não o relatório técnico de
// RelatorioPdfService), no molde do PDF de referência que o usuário trouxe pronto (L'okta IA ·
// Proposta Comercial · iAgrow, ago/2026): capa escura, sumário executivo, diagnóstico REAL da
// empresa, catálogo dos 15 módulos em 4 camadas (Discover/Protect/Analyze/Govern), comparativo,
// ROI, cronograma de 90 dias, investimento, termos comerciais e anexos. Só a seção 02 (diagnóstico)
// usa dado real do tenant — o resto é narrativa comercial fixa, igual em qualquer proposta emitida.
// Sempre em PT-BR: proposta comercial pra cliente brasileiro, sem versão EN (diferente do resto do
// app, que é bilíngue).
public class PropostaComercialPdfService
{
    private readonly byte[] _iconMono;

    static PropostaComercialPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PropostaComercialPdfService(IWebHostEnvironment env)
    {
        _iconMono = File.ReadAllBytes(Path.Combine(env.WebRootPath, "img", "brand", "simbolo-mono.png"));
    }

    private const string BrandBg = "#0B1220";
    private const string BrandBg2 = "#0F1B2E";
    private const string BrandBlue = "#4D9BFF";
    private const string BrandGreen = "#00E0A4";
    private const string BrandOrange = "#FF8A3D";
    private const string BrandRed = "#FF3B5C";
    private const string BrandPurple = "#8A7BFF";
    private const string BrandYellow = "#F5D547";
    private const string TextMuted = "#7A8FAB";
    private const string TextMuted2 = "#5A7191";

    /// <summary>
    /// Monta a proposta. Os dois últimos parâmetros são opcionais e trazem o diagnóstico
    /// conduzido em reunião, quando existir.
    ///
    /// Scanner e diagnóstico não competem, se completam — e a proposta precisa deixar isso
    /// explícito: a varredura MEDE a superfície externa, o diagnóstico LEVANTA o que ela não
    /// alcança (backup, identidade, pessoas, governança) a partir do que o cliente declarou.
    /// Misturar os dois num número só seria vender levantamento como medição.
    /// </summary>
    public byte[] Gerar(Company empresa, List<Vulnerability> achadosReais, int ativosReaisCount,
        int ativosTotalCount, DateTimeOffset? ultimaVarredura,
        Models.Diagnostico? diagnostico = null,
        Services.Diagnostico.ResultadoDoDiagnostico? resultadoDiagnostico = null,
        List<DiagnosticoRisco>? riscosDiagnostico = null)
    {
        var geradoEm = DateTimeOffset.Now;
        var referencia = $"OKT-{geradoEm:yyyy}-{ReferenciaSlug(empresa.Nome)}-01";
        var diagnosticoDisponivel = ativosReaisCount > 0;

        var portasAbertas = achadosReais.Count(a => a.CategoriaScan == SecurityScanService.CategoriaPortas);
        var score = diagnosticoDisponivel ? CompanySecurityScoreCalculator.Calcular(achadosReais, ativosReaisCount, portasAbertas) : null;
        var corScore = score is null ? TextMuted : score.Score >= 80 ? BrandGreen : score.Score >= 50 ? BrandYellow : BrandRed;

        (string Chave, string Label)[] categorias =
        [
            (SecurityScanService.CategoriaTls, "Certificado e protocolo TLS"),
            (SecurityScanService.CategoriaHeaders, "Cabeçalhos HTTP de segurança"),
            (SecurityScanService.CategoriaDns, "Autenticação de e-mail (SPF/DMARC)"),
            (SecurityScanService.CategoriaPortas, "Portas comumente exploradas"),
        ];
        var achadosPorCategoria = categorias.Select(cat => (cat.Label, Achados: achadosReais.Where(a => a.CategoriaScan == cat.Chave).ToList())).ToList();
        var totalSemAchado = achadosReais.Count == 0;

        var documento = Document.Create(container =>
        {
            // ---------- CAPA ----------
            container.Page(capa =>
            {
                capa.Size(PageSizes.A4);
                capa.Margin(0);
                capa.DefaultTextStyle(x => x.FontFamily(Fonts.Calibri).FontColor(Colors.White));
                capa.PageColor(BrandBg);

                capa.Content().Padding(46).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.AutoItem().Width(30).Height(30).Background(BrandBlue).AlignMiddle().AlignCenter()
                            .Padding(6).Image(_iconMono);
                        row.RelativeItem().PaddingLeft(10).Column(brand =>
                        {
                            brand.Item().Text(t =>
                            {
                                t.Span("L'okta ").FontSize(15).Bold().FontColor(Colors.White);
                                t.Span("IA").FontSize(15).Bold().FontColor(BrandBlue);
                            });
                            brand.Item().Text("SEGURANÇA CONTÍNUA").FontSize(7).FontColor(TextMuted).LetterSpacing(0.15f);
                        });
                    });

                    col.Item().PaddingTop(70).Text("PROPOSTA COMERCIAL").FontSize(10).Bold().FontColor(BrandBlue).LetterSpacing(0.1f);
                    col.Item().PaddingTop(10).Text($"Uma visão única sobre toda a segurança da {empresa.Nome}").FontSize(30).Bold().LineHeight(1.15f);
                    col.Item().PaddingTop(16).Width(420).Text(
                        $"Transformamos as dezenas de ferramentas de segurança que a {empresa.Nome} já possui em uma visão única, inteligente e orientada ao negócio.")
                        .FontSize(11.5f).FontColor("#C4D3E6").LineHeight(1.55f);

                    col.Item().PaddingTop(40).LineHorizontal(1).LineColor("#22334D");
                    col.Item().PaddingTop(20).Row(row =>
                    {
                        void Meta(string label, string valor)
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(label).FontSize(7.5f).FontColor(TextMuted2).LetterSpacing(0.1f);
                                c.Item().PaddingTop(3).Text(valor).FontSize(12).FontColor(Colors.White);
                            });
                        }

                        Meta("CLIENTE", empresa.Nome);
                        Meta("EMISSÃO", geradoEm.ToString("dd · MMM · yyyy"));
                        Meta("VALIDADE", "30 dias");
                        Meta("REFERÊNCIA", referencia);
                    });
                });

                capa.Footer().Background(BrandBg).PaddingHorizontal(46).PaddingVertical(14).Row(row =>
                {
                    row.RelativeItem().Text("DOCUMENTO CONFIDENCIAL — USO RESTRITO").FontSize(7).FontColor(TextMuted2);
                    row.AutoItem().Text("L'OKTA IA").FontSize(7).FontColor(TextMuted2);
                });
            });

            // ---------- CORPO ----------
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(38);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily(Fonts.Calibri).FontColor("#1C2836"));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Text("L'OKTA IA").FontSize(7.5f).Bold().FontColor(TextMuted2).LetterSpacing(0.1f);
                    row.AutoItem().Text($"PROPOSTA COMERCIAL · {empresa.Nome.ToUpperInvariant()} · {geradoEm:MMM yyyy}".ToUpperInvariant())
                        .FontSize(7.5f).FontColor(TextMuted2).LetterSpacing(0.06f);
                });

                page.Footer().PaddingTop(6).Column(col =>
                {
                    col.Item().LineHorizontal(0.7f).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text("DOCUMENTO CONFIDENCIAL — USO RESTRITO").FontSize(6.5f).FontColor(TextMuted2);
                        row.AutoItem().Text(x =>
                        {
                            x.Span("L'OKTA IA · ").FontSize(6.5f).FontColor(TextMuted2);
                            x.CurrentPageNumber().FontSize(6.5f).FontColor(TextMuted2);
                            x.Span(" / ").FontSize(6.5f).FontColor(TextMuted2);
                            x.TotalPages().FontSize(6.5f).FontColor(TextMuted2);
                        });
                    });
                });

                page.Content().PaddingTop(18).Column(body =>
                {
                    body.Spacing(4);

                    // 01 — Sumário executivo
                    SectionTitle(body, "01", "Sumário executivo");
                    body.Item().PaddingTop(6).Text(
                        $"A {empresa.Nome} já trabalha com firewall, antivírus ou EDR, algum sistema de identidade (Active Directory, " +
                        "Google Workspace ou similar), provedor de nuvem, monitoramento e rotina de backup? Se a resposta for sim — mesmo que só " +
                        "parte disso — cada uma dessas ferramentas resolve bem o seu pedaço. A pergunta é: juntas, elas respondem sozinhas a " +
                        "\"estamos seguros?\"")
                        .FontSize(10).LineHeight(1.6f);
                    body.Item().PaddingTop(6).Text(
                        "O problema não é falta de ferramenta. É que ninguém enxerga tudo junto. Quando a diretoria pergunta " +
                        "“estamos seguros?”, a resposta hoje exige abrir vários consoles, cruzar planilhas e confiar na memória de quem estava de plantão.")
                        .FontSize(10).LineHeight(1.6f);
                    body.Item().PaddingTop(6).Text(t =>
                    {
                        t.Span("A L'okta IA não substitui nenhuma dessas ferramentas. Ela ").FontSize(10).LineHeight(1.6f);
                        t.Span("orquestra").FontSize(10).Bold().LineHeight(1.6f);
                        t.Span(" todas. É a camada de inteligência que fica acima do parque instalado, consolida o que cada sistema já sabe, " +
                               "prioriza riscos com IA e traduz tudo em uma linguagem que o conselho entende: risco, custo, prazo e responsável.")
                            .FontSize(10).LineHeight(1.6f);
                    });

                    body.Item().PaddingTop(14).Row(row =>
                    {
                        void Stat(string valor, string label)
                        {
                            row.RelativeItem().Background("#F6F8FB").Padding(12).Column(c =>
                            {
                                c.Item().Text(valor).FontSize(22).Bold().FontColor(BrandBg);
                                c.Item().PaddingTop(4).Text(label).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.4f);
                            });
                        }

                        Stat("1", "Um único painel no lugar de vários consoles distintos");
                        Stat("90", "Dias para as quatro camadas em operação plena");
                        Stat("0", "Ferramentas descartadas — o investimento atual é preservado");
                    });

                    // 02 — Diagnóstico real
                    body.Item().PaddingTop(22);
                    SectionTitle(body, "02", $"Onde a {empresa.Nome} está hoje");

                    if (!diagnosticoDisponivel)
                    {
                        body.Item().PaddingTop(6).Text(
                            "Ainda não há um ativo real autorizado e escaneado para esta empresa na plataforma. O diagnóstico de " +
                            "superfície de ataque — camada Discover, sem nenhuma integração necessária — é o primeiro passo do piloto de 30 dias " +
                            "descrito na seção 13 e passa a preencher esta seção automaticamente assim que o primeiro ativo for verificado.")
                            .FontSize(9.5f).FontColor(TextMuted).LineHeight(1.6f).Italic();
                    }
                    else
                    {
                        body.Item().PaddingTop(6).Text(
                            $"O diagnóstico de superfície de ataque já executado na {empresa.Nome} serve como linha de base desta proposta. " +
                            "Ele mede o que um atacante enxerga da internet, sem nenhum acesso privilegiado — exatamente o ponto de partida de qualquer invasão real.")
                            .FontSize(9.5f).LineHeight(1.6f);

                        body.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(14).Row(row =>
                        {
                            row.ConstantItem(150).Background(BrandBg).Padding(12).Column(c =>
                            {
                                c.Item().Text("SCORE ATUAL").FontSize(7).FontColor(TextMuted2).LetterSpacing(0.08f);
                                c.Item().PaddingTop(4).Row(r =>
                                {
                                    r.AutoItem().Text(score!.Score.ToString()).FontSize(30).Bold().FontColor(corScore);
                                    r.AutoItem().PaddingTop(14).PaddingLeft(2).Text("/100").FontSize(10).FontColor(TextMuted2);
                                });
                                c.Item().PaddingTop(6).Background("#1B4B3A").Padding(4).Text($"CLASSE {score!.Classificacao}").FontSize(8).Bold().FontColor(BrandGreen);
                            });

                            row.RelativeItem().PaddingLeft(14).Column(c =>
                            {
                                c.Item().Text("VERIFICAÇÕES REALIZADAS").FontSize(7.5f).Bold().FontColor(TextMuted2).LetterSpacing(0.08f);
                                foreach (var (label, achados) in achadosPorCategoria)
                                {
                                    c.Item().PaddingTop(8).Row(r =>
                                    {
                                        var ok = achados.Count == 0;
                                        r.ConstantItem(16).Text(ok ? "✓" : "!").FontSize(11).Bold().FontColor(ok ? BrandGreen : BrandOrange);
                                        r.RelativeItem().PaddingLeft(4).Column(cc =>
                                        {
                                            cc.Item().Text(label).FontSize(9).Bold();
                                            cc.Item().PaddingTop(1).Text(ok
                                                ? "Verificado — nenhuma vulnerabilidade encontrada."
                                                : $"{achados.Count} achado(s) identificado(s) — risco mapeado, correção conduzida pela nossa equipe técnica.")
                                                .FontSize(8.5f).FontColor(ok ? BrandGreen : BrandOrange);
                                        });
                                    });
                                }
                            });
                        });

                        body.Item().PaddingTop(10).Background(totalSemAchado ? "#EAFBF4" : "#FFF7EC").Padding(10)
                            .Text(totalSemAchado
                                ? $"Nenhuma vulnerabilidade foi encontrada em nenhuma das {categorias.Length} frentes verificadas na superfície externa da {empresa.Nome}. " +
                                  "Nos aspectos técnicos avaliados (certificado, cabeçalhos de segurança, autenticação de e-mail e portas expostas), o ambiente está em conformidade " +
                                  "com as medidas de segurança exigidas pelo Art. 46 da LGPD."
                                : $"{achadosPorCategoria.Count(x => x.Achados.Count == 0)} de {categorias.Length} frentes verificadas não apresentaram nenhuma vulnerabilidade. " +
                                  "As demais têm achado identificado, com risco e recomendação já mapeados pela nossa equipe — a correção técnica completa é " +
                                  "conduzida por especialistas, dentro do escopo do serviço contratado.")
                            .FontSize(9).FontColor("#1C2836").LineHeight(1.55f);

                        body.Item().PaddingTop(10).Text(
                            $"Ativos verificados: {ativosTotalCount} · Última varredura: " +
                            (ultimaVarredura is null ? "—" : ultimaVarredura.Value.ToString("dd/MM/yyyy HH:mm")))
                            .FontSize(8.5f).FontColor(TextMuted);

                        body.Item().PaddingTop(10).Text(
                            "Cada achado vem com duas coisas que uma varredura comum não entrega: qual é o risco em termos de negócio e qual é a " +
                            "recomendação. A correção — o passo a passo técnico — fica com a nossa equipe, que executa e responde pelo resultado. " +
                            "Não é uma lista de problemas — é um plano de ação conduzido por especialistas.")
                            .FontSize(9).FontColor(TextMuted).LineHeight(1.6f);
                    }

                    // 02b — O levantamento conduzido em reunião, quando existe.
                    if (diagnostico is not null && resultadoDiagnostico is { } rd)
                    {
                        DiagnosticoConduzido(body, empresa, diagnostico, rd, riscosDiagnostico ?? []);
                    }

                    body.Item().PaddingTop(8).Text(
                        "O que esse diagnóstico mostra é apenas a superfície externa. É o primeiro dos quinze módulos da plataforma, e o único que roda " +
                        "sem nenhuma integração. Os outros catorze só aparecem quando a L'okta IA se conecta ao que a empresa já tem instalado.")
                        .FontSize(9).FontColor(TextMuted).LineHeight(1.6f);

                    // 03 — O diretor não quer saber de porta 445
                    body.Item().PaddingTop(20);
                    SectionTitle(body, "03", "O diretor não quer saber de porta 445");
                    body.Item().PaddingTop(6).Text(
                        "Quem decide orçamento não quer relatório de IPS, IDS, CVE ou hash SHA256. Quer a resposta para uma única pergunta: estamos seguros?")
                        .FontSize(9.5f).LineHeight(1.6f);
                    body.Item().PaddingTop(6).Text(
                        "Hoje essa pergunta trava. A informação existe, mas está espalhada em vários sistemas que não conversam entre si, cada um com sua " +
                        "própria escala de severidade, seu próprio relatório e seu próprio responsável. Consolidar leva dias e o resultado envelhece na semana seguinte.")
                        .FontSize(9.5f).FontColor(TextMuted).LineHeight(1.6f);

                    body.Item().PaddingTop(14).Background(BrandBg).Padding(16).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Com a L'okta IA, a resposta cabe em uma tela").FontSize(12).Bold().FontColor(Colors.White);
                            r.AutoItem().Text("EXEMPLO DE PAINEL (ILUSTRATIVO)").FontSize(6.5f).FontColor(TextMuted2);
                        });
                        c.Item().PaddingTop(12).Row(r =>
                        {
                            void Ind(string valor, string label, string cor)
                            {
                                r.RelativeItem().PaddingRight(8).Column(cc =>
                                {
                                    cc.Item().Text(valor).FontSize(16).Bold().FontColor(cor);
                                    cc.Item().PaddingTop(2).Text(label).FontSize(7.5f).FontColor(TextMuted2);
                                });
                            }

                            Ind("82/100", "Score de segurança", BrandGreen);
                            Ind("14", "Vulnerabilidades críticas", BrandRed);
                            Ind("2", "Servidores expostos", BrandOrange);
                            Ind("7", "Usuários administradores", BrandYellow);
                        });
                        c.Item().PaddingTop(10).Row(r =>
                        {
                            void Ind(string valor, string label, string cor)
                            {
                                r.RelativeItem().PaddingRight(8).Column(cc =>
                                {
                                    cc.Item().Text(valor).FontSize(16).Bold().FontColor(cor);
                                    cc.Item().PaddingTop(2).Text(label).FontSize(7.5f).FontColor(TextMuted2);
                                });
                            }

                            Ind("43", "Máquinas sem atualização", BrandOrange);
                            Ind("OK", "Backup funcionando", BrandGreen);
                            Ind("81%", "Cobertura de MFA", BrandYellow);
                            Ind("3", "Certificados expirando", BrandOrange);
                        });
                        c.Item().PaddingTop(10).Text($"Painel ilustrativo. Os indicadores reais da {empresa.Nome} são preenchidos automaticamente na Fase 1 da implantação.")
                            .FontSize(7.5f).FontColor(TextMuted2).Italic();
                    });

                    // 04 — Não é mais uma ferramenta
                    body.Item().PaddingTop(20);
                    SectionTitle(body, "04", "Não é mais uma ferramenta. É a camada acima delas.");
                    body.Item().PaddingTop(6).Text(
                        "Enquanto o firewall protege o perímetro, o EDR protege os endpoints e o SIEM correlaciona eventos, a L'okta IA conecta tudo isso, " +
                        "prioriza riscos com inteligência artificial, gera relatórios executivos, mede conformidade e orienta a tomada de decisão.")
                        .FontSize(9.5f).LineHeight(1.6f);
                    body.Item().PaddingTop(10).BorderLeft(3).BorderColor(BrandBlue).Background("#F0F5FF").Padding(12)
                        .Text("“Nós transformamos dezenas de ferramentas de segurança em uma visão única, inteligente e orientada ao negócio.”")
                        .FontSize(11).Italic().FontColor(BrandBg);
                    body.Item().PaddingTop(10).Text(
                        $"Isso significa que a {empresa.Nome} não troca de fornecedor, não refaz contrato, não descarta licença e não retreina equipe. " +
                        "Cada investimento já feito em segurança passa a render mais, porque finalmente aparece em um lugar onde a diretoria consegue ver.")
                        .FontSize(9.5f).FontColor(TextMuted).LineHeight(1.6f);

                    // 05 — Quatro camadas
                    body.Item().PaddingTop(20);
                    SectionTitle(body, "05", "A plataforma em quatro camadas");
                    body.Item().PaddingTop(6).Text(
                        "Quinze módulos organizados em quatro camadas que se constroem uma sobre a outra. Cada camada entrega valor sozinha e potencializa a seguinte.")
                        .FontSize(9.5f).FontColor(TextMuted).LineHeight(1.6f);

                    LayerBlock(body, "01", "Discover", "Descobrir tudo o que existe — inclusive o que ninguém sabia que existia", BrandBlue,
                    [
                        ("Attack Surface Management", "Varredura contínua de domínios, subdomínios, IPs públicos, nuvem, VPN, SSH, RDP, DNS, SPF, DMARC, DKIM e certificados SSL. Ativo novo, porta aberta ou domínio registrado gera alerta no mesmo dia."),
                        ("Inventário Inteligente", "Descoberta automática de computadores, notebooks, celulares, servidores, roteadores, switches, APs, firewalls, impressoras, VMs, containers, usuários, licenças e serviços."),
                        ("Certificados e Licenças", "SSL, ICP-Brasil, VPN, nuvem, domínios e licenças de software com data de expiração rastreada. A plataforma avisa antes de vencer, não depois."),
                    ]);

                    LayerBlock(body, "02", "Protect", "Conectar o que já está instalado e fechar as lacunas", BrandGreen,
                    [
                        ("Patch Management", "Visão consolidada do que está desatualizado em Windows, Linux, Office, navegadores, bancos de dados e servidores de aplicação — com o risco de cada pendência."),
                        ("Identidade e Acessos", "Integração com Active Directory, Entra ID, Okta, Google Workspace e LDAP. Expõe usuários inativos, excesso de administradores, senhas antigas, MFA faltante e contas órfãs."),
                        ("SIEM e SOC", "Conexão com as principais plataformas de SIEM e observabilidade do mercado. Em vez de milhares de eventos, a IA responde: “houve 3 eventos relevantes hoje”."),
                    ]);

                    LayerBlock(body, "03", "Analyze", "A IA correlaciona, prioriza e explica", BrandPurple,
                    [
                        ("Vulnerability Management", "Consolida os principais scanners do mercado em uma fila única, e responde: quais são as mais críticas, qual o risco financeiro e qual corrigir primeiro."),
                        ("IA Consultora", "Traduz cada achado técnico em impacto de negócio, prioridade e tempo estimado de correção. Uma explicação em português no lugar de um código CVE."),
                        ("IA de Investigação", "O analista pergunta “por que esse servidor caiu?” e a IA cruza firewall, EDR, sistema operacional, nuvem, DNS, logs e backup para responder."),
                        ("Cyber Digital Twin", $"Mapa vivo da {empresa.Nome} — filiais, firewalls, switches, servidores, banco de dados, ERP, usuários e nuvem — mostrando em tempo real onde está o risco e o que ele afeta."),
                    ]);

                    LayerBlock(body, "04", "Govern", "O que a diretoria e a auditoria realmente pedem", BrandOrange,
                    [
                        ("Executive Dashboard", "Risco cibernético, evolução, incidentes, ataques bloqueados, tempo médio de resposta e de correção, custo evitado e benchmark. Sem uma linha de log."),
                        ("Compliance Automático", "LGPD, ISO 27001, NIST, CIS Controls e outras normas medidas continuamente, com a lista exata do que falta."),
                        ("Gestão de Terceiros", "Score de risco por fornecedor, com classificação alto, médio e baixo. Exigência crescente em contratos e auditorias de cadeia de suprimentos."),
                        ("Relatórios Automáticos", "PDF executivo periódico, risco da semana, relatório de LGPD mensal, board report trimestral. Gerados sozinhos."),
                        ("Gestão Financeira", "Quanto custa manter a segurança atual. Quanto custa um incidente. Quanto foi economizado — o argumento que fecha orçamento."),
                    ]);

                    // 06 — Três exemplos
                    body.Item().PaddingTop(20);
                    SectionTitle(body, "06", $"Três exemplos do que a {empresa.Nome} passa a ver");

                    body.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(14).Column(c =>
                    {
                        c.Item().Text("CONFORMIDADE MEDIDA CONTINUAMENTE (ILUSTRATIVO)").FontSize(7).FontColor(TextMuted2).LetterSpacing(0.08f);
                        void Barra(string label, int pct, string cor)
                        {
                            c.Item().PaddingTop(10).Row(r =>
                            {
                                r.ConstantItem(60).Text(label).FontSize(9).Bold();
                                r.RelativeItem().Height(9).Background("#EEF1F5").Row(bar =>
                                {
                                    bar.RelativeItem(pct).Background(cor);
                                    bar.RelativeItem(100 - pct);
                                });
                                r.ConstantItem(34).AlignRight().Text($"{pct}%").FontSize(9).Bold();
                            });
                        }

                        Barra("ISO 27001", 76, BrandOrange);
                        Barra("LGPD", 91, BrandGreen);
                        Barra("NIST CSF", 53, BrandRed);
                        c.Item().PaddingTop(10).Text("E, ao lado de cada percentual, a lista exata dos controles que faltam para chegar a 100%.")
                            .FontSize(8.5f).FontColor(TextMuted);
                    });

                    body.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                        {
                            c.Item().Text("IA CONSULTORA — RESPOSTA TÍPICA").FontSize(7).FontColor(TextMuted2).LetterSpacing(0.08f);
                            c.Item().PaddingTop(6).Text("CVE-2026-XXXXX").FontSize(9).FontColor(TextMuted).FontFamily(Fonts.CourierNew);
                            c.Item().PaddingTop(6).Text(
                                "“Esta vulnerabilidade permite execução remota de código no servidor financeiro. Caso explorada, poderá causar " +
                                "indisponibilidade do ERP. Prioridade: Alta. Tempo estimado de correção: 30 minutos.”")
                                .FontSize(9).Italic().LineHeight(1.5f);
                        });
                        row.ConstantItem(14);
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                        {
                            c.Item().Text("RISCO DE TERCEIROS (ILUSTRATIVO)").FontSize(7).FontColor(TextMuted2).LetterSpacing(0.08f);
                            void Forn(string nome, int score, string label, string cor)
                            {
                                c.Item().PaddingTop(8).Row(r =>
                                {
                                    r.RelativeItem().Text(nome).FontSize(9);
                                    r.ConstantItem(24).AlignRight().Text(score.ToString()).FontSize(9).Bold();
                                    r.ConstantItem(46).AlignRight().Background(cor + "22").Padding(2).Text(label).FontSize(7).Bold().FontColor(cor);
                                });
                            }

                            Forn("Fornecedor A", 95, "BAIXO", BrandGreen);
                            Forn("Fornecedor B", 41, "ALTO", BrandRed);
                            Forn("Fornecedor C", 76, "MÉDIO", BrandOrange);
                        });
                    });

                    // 07 — Como isso muda o dia a dia
                    body.Item().PaddingTop(20);
                    SectionTitle(body, "07", "Como isso muda o dia a dia");
                    body.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2.2f);
                            cd.RelativeColumn(3);
                            cd.RelativeColumn(3);
                        });

                        void Head(string t) => table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(4).Text(t).FontSize(7.5f).Bold().FontColor(TextMuted2).LetterSpacing(0.06f);
                        Head("SITUAÇÃO");
                        Head("HOJE, SEM A PLATAFORMA");
                        Head("COM A L'OKTA IA");

                        (string Sit, string Antes, string Depois)[] linhas =
                        [
                            ("Um novo servidor sobe na nuvem", "Ninguém registra. Aparece meses depois em uma auditoria, ou nunca.", "Alerta no mesmo dia: “novo servidor encontrado”, com portas abertas e risco associado."),
                            ("O conselho pede a posição de segurança", "Dias de coleta manual em vários sistemas, apresentação montada às pressas.", "O board report do trimestre já foi gerado automaticamente e está pronto para envio."),
                            ("Certificado SSL vence", "O site cai, o cliente avisa antes da TI, e a equipe corre para renovar.", "Aviso automático semanas antes, com o responsável e o custo de renovação já mapeados."),
                            ("Ex-funcionário mantém acesso", "Descoberto por acaso, geralmente após um incidente ou revisão de licenças.", "Contas órfãs e inativas aparecem no painel de identidade desde o primeiro dia."),
                            ("Um servidor cai de madrugada", "Horas de investigação manual entre firewall, EDR, logs, nuvem e backup.", "A IA de Investigação cruza as fontes e entrega a causa provável antes do café."),
                        ];

                        foreach (var l in linhas)
                        {
                            table.Cell().PaddingVertical(7).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(l.Sit).FontSize(9).Bold();
                            table.Cell().PaddingVertical(7).PaddingRight(8).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(l.Antes).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.45f);
                            table.Cell().PaddingVertical(7).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(l.Depois).FontSize(8.5f).FontColor(BrandBg).LineHeight(1.45f);
                        }
                    });

                    // 08 — Sem conflito
                    body.Item().PaddingTop(18);
                    SectionTitle(body, "08", "Por que não existe conflito com o que a empresa já usa");
                    body.Item().PaddingTop(6).Text(
                        "Fabricantes de perímetro e endpoint resolvem controle. SIEMs resolvem correlação de eventos. Nenhum dos dois grupos resolve gestão. " +
                        "É exatamente esse espaço que a L'okta IA ocupa.")
                        .FontSize(9.5f).LineHeight(1.6f);

                    body.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(3.2f);
                            cd.RelativeColumn(1.6f);
                            cd.RelativeColumn(1.6f);
                            cd.RelativeColumn(1.6f);
                        });

                        void Head(string t, bool destaque = false) => table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(4)
                            .Text(t).FontSize(7.5f).Bold().FontColor(destaque ? BrandBlue : TextMuted2).LetterSpacing(0.05f);
                        Head("NECESSIDADE");
                        Head("FABRICANTES DE PONTO");
                        Head("SIEM");
                        Head("L'OKTA IA", true);

                        (string Necessidade, string Fab, string Siem, string Okta)[] linhas =
                        [
                            ("Bloquear ameaça no perímetro e no endpoint", "Sim", "Não", "Integra"),
                            ("Correlacionar eventos e logs", "Parcial", "Sim", "Integra"),
                            ("Visão única de todo o parque, de todos os fabricantes", "Não", "Não", "Sim"),
                            ("Score de risco entendível pela diretoria", "Não", "Não", "Sim"),
                            ("Percentual de aderência a LGPD, ISO e NIST", "Não", "Parcial", "Sim"),
                            ("Custo da segurança e custo evitado", "Não", "Não", "Sim"),
                            ("Risco dos fornecedores e terceiros", "Não", "Não", "Sim"),
                        ];

                        foreach (var l in linhas)
                        {
                            table.Cell().PaddingVertical(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(l.Necessidade).FontSize(8.5f);
                            table.Cell().PaddingVertical(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignCenter().Text(l.Fab).FontSize(8.5f).FontColor(TextMuted);
                            table.Cell().PaddingVertical(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignCenter().Text(l.Siem).FontSize(8.5f).FontColor(TextMuted);
                            table.Cell().PaddingVertical(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignCenter().Text(l.Okta).FontSize(8.5f).Bold().FontColor(BrandBlue);
                        }
                    });

                    // 09 — ROI
                    body.Item().PaddingTop(18);
                    SectionTitle(body, "09", "Retorno sobre o investimento");
                    body.Item().PaddingTop(6).Text(
                        "O retorno da L'okta IA vem de quatro frentes mensuráveis. Os valores são preenchidos com os números reais da empresa a partir da Fase 1 " +
                        "e o cálculo passa a ser acompanhado no próprio painel, no módulo de Gestão Financeira.")
                        .FontSize(9.5f).FontColor(TextMuted).LineHeight(1.6f);

                    (string T, string D)[] roi =
                    [
                        ("Horas técnicas devolvidas", "Coleta manual de dados, montagem de relatórios e conferência entre consoles somam horas todo mês. A automação devolve esse tempo à equipe sem aumentar o quadro."),
                        ("Incidentes evitados", "Um único servidor exposto ou uma fraude por e-mail sem DMARC pode custar mais do que um ano inteiro de plataforma. A conta de retorno vira positiva no primeiro incidente evitado."),
                        ("Licenças e contratos otimizados", "Licença ociosa, renovação automática esquecida e sobreposição entre ferramentas aparecem no inventário. O módulo financeiro costuma se pagar antes do resto."),
                        ("Custo de auditoria reduzido", "Evidência de conformidade gerada continuamente, e não em regime de mutirão às vésperas da auditoria. Vale para LGPD, ISO 27001 e due diligence de clientes e investidores."),
                    ];
                    body.Item().PaddingTop(8).Column(c =>
                    {
                        foreach (var r in roi)
                        {
                            c.Item().PaddingTop(8).BorderLeft(2).BorderColor(BrandBlue).PaddingLeft(10).Column(cc =>
                            {
                                cc.Item().Text(r.T).FontSize(9.5f).Bold();
                                cc.Item().PaddingTop(2).Text(r.D).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.5f);
                            });
                        }
                    });

                    // 10 — Implantação
                    body.Item().PaddingTop(18);
                    SectionTitle(body, "10", "Implantação em 90 dias");
                    body.Item().PaddingTop(6).Text("Cada fase entrega valor por conta própria. A empresa vê resultado na segunda semana, não no fim do projeto.")
                        .FontSize(9.5f).FontColor(TextMuted).LineHeight(1.6f);

                    body.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(1.1f);
                            cd.RelativeColumn(1.1f);
                            cd.RelativeColumn(3.2f);
                            cd.RelativeColumn(2.4f);
                        });

                        void Head(string t) => table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(4).Text(t).FontSize(7.5f).Bold().FontColor(TextMuted2).LetterSpacing(0.05f);
                        Head("PERÍODO");
                        Head("CAMADA");
                        Head("ATIVIDADES");
                        Head("ENTREGA");

                        (string P, string C, string A, string E)[] fases =
                        [
                            ("Sem. 1–2", "Discover", "Ativação do ASM contínuo, descoberta de ativos internos e externos, mapeamento de certificados e licenças.", "Inventário completo e score de superfície de ataque atualizado diariamente."),
                            ("Sem. 3–6", "Protect", "Integração com firewall, EDR, identidade, nuvem, backup e SIEM já em uso.", "Painéis de patch, identidade e eventos alimentados por dados reais."),
                            ("Sem. 7–10", "Analyze", "Calibragem da IA com o contexto da empresa, definição de criticidade por sistema, montagem do Cyber Digital Twin.", "Fila única de vulnerabilidades priorizada por impacto no negócio."),
                            ("Sem. 11–13", "Govern", "Configuração dos dashboards executivos, réguas de compliance, cadastro de terceiros e agenda de relatórios.", "Primeiro board report emitido e rotina automática em operação."),
                        ];

                        foreach (var f in fases)
                        {
                            table.Cell().PaddingVertical(7).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(f.P).FontSize(8.5f).Bold();
                            table.Cell().PaddingVertical(7).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(f.C).FontSize(8.5f).Bold().FontColor(BrandBlue);
                            table.Cell().PaddingVertical(7).PaddingRight(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(f.A).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.45f);
                            table.Cell().PaddingVertical(7).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(f.E).FontSize(8.5f).LineHeight(1.45f);
                        }
                    });

                    // 11 — Investimento
                    body.Item().PaddingTop(18);
                    SectionTitle(body, "11", "Investimento");
                    body.Item().PaddingTop(6).Background("#FFF7EC").Padding(10).Text(
                        "Faixas ilustrativas. Os valores abaixo servem para dimensionar a ordem de grandeza do investimento. A proposta financeira formal é " +
                        "emitida após o levantamento de escopo — quantidade de ativos, integrações e módulos contratados.")
                        .FontSize(8.5f).FontColor("#7A5A20").LineHeight(1.5f);

                    body.Item().PaddingTop(10).Row(row =>
                    {
                        void Plano(string tag, string nome, string preco, string sub, string desc, string escopo, bool destaque)
                        {
                            row.RelativeItem().Padding(2).Border(destaque ? 2 : 1).BorderColor(destaque ? BrandBlue : Colors.Grey.Lighten2)
                                .Background(destaque ? BrandBg : Colors.White).Padding(12).Column(c =>
                            {
                                c.Item().Text(tag).FontSize(7).Bold().FontColor(destaque ? BrandBlue : TextMuted2).LetterSpacing(0.06f);
                                c.Item().PaddingTop(4).Text(nome).FontSize(13).Bold().FontColor(destaque ? Colors.White : BrandBg);
                                c.Item().PaddingTop(6).Text(preco).FontSize(17).Bold().FontColor(destaque ? BrandGreen : BrandBg);
                                c.Item().Text(sub).FontSize(7.5f).FontColor(destaque ? TextMuted2 : TextMuted);
                                c.Item().PaddingTop(8).Text(desc).FontSize(8).FontColor(destaque ? "#C4D3E6" : TextMuted).LineHeight(1.45f);
                                c.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(destaque ? "#22334D" : Colors.Grey.Lighten2);
                                c.Item().PaddingTop(6).Text(escopo).FontSize(7.5f).Bold().FontColor(destaque ? Colors.White : BrandBg);
                            });
                        }

                        Plano("PLANO", "Essencial", "R$ 3–6 mil", "por mês · exemplo",
                            "Camada Discover: ASM contínuo, inventário inteligente, gestão de certificados e licenças, relatório executivo mensal.",
                            "Até 250 ativos monitorados", false);
                        Plano("RECOMENDADO", "Avançado", "R$ 9–16 mil", "por mês · exemplo",
                            "Discover + Protect + Analyze: tudo do Essencial, mais integrações com firewall, EDR, identidade, nuvem, backup e SIEM, IA Consultora e fila única de vulnerabilidades.",
                            "Até 1.000 ativos · 10 integrações", true);
                        Plano("PLANO", "Enterprise", "Sob consulta", "a partir de R$ 22 mil/mês",
                            "As quatro camadas: tudo do Avançado, mais Cyber Digital Twin, gestão de terceiros, gestão financeira, compliance multinorma e board report trimestral.",
                            "Ativos e integrações ilimitados", false);
                    });

                    body.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(1.6f);
                            cd.RelativeColumn(4);
                            cd.RelativeColumn(1.4f);
                        });

                        void Head(string t) => table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(4).Text(t).FontSize(7.5f).Bold().FontColor(TextMuted2).LetterSpacing(0.05f);
                        Head("ITEM ÚNICO");
                        Head("DESCRIÇÃO");
                        Head("FAIXA ILUSTRATIVA");

                        (string I, string D, string F)[] itens =
                        [
                            ("Setup e onboarding", "Levantamento, conexão das integrações, calibragem da IA e treinamento das equipes.", "R$ 12–28 mil"),
                            ("Integração sob medida", "Conector para sistema proprietário ou legado fora do catálogo padrão.", "R$ 6–15 mil"),
                            ("Cyber Digital Twin", "Modelagem do mapa vivo com filiais, dependências e criticidade por sistema.", "R$ 18–35 mil"),
                        ];

                        foreach (var it in itens)
                        {
                            table.Cell().PaddingVertical(7).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(it.I).FontSize(8.5f).Bold();
                            table.Cell().PaddingVertical(7).PaddingRight(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(it.D).FontSize(8.5f).FontColor(TextMuted);
                            table.Cell().PaddingVertical(7).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(it.F).FontSize(8.5f).Bold();
                        }
                    });

                    // 12 — Termos comerciais
                    body.Item().PaddingTop(18);
                    SectionTitle(body, "12", "Termos comerciais");
                    body.Item().PaddingTop(8).Row(row =>
                    {
                        void Termo(string label, string valor)
                        {
                            row.RelativeItem().PaddingRight(10).PaddingBottom(10).Column(c =>
                            {
                                c.Item().Text(label).FontSize(7).Bold().FontColor(TextMuted2).LetterSpacing(0.05f);
                                c.Item().PaddingTop(3).Text(valor).FontSize(8.5f).LineHeight(1.4f);
                            });
                        }

                        Termo("PRAZO CONTRATUAL", "12 meses, renovação automática por igual período");
                        Termo("FATURAMENTO", "Mensal, com setup faturado na assinatura");
                        Termo("PILOTO", "30 dias na camada Discover, sem compromisso");
                        Termo("SUPORTE", "Horário comercial (8×5), em todos os planos");
                    });
                    body.Item().Row(row =>
                    {
                        void Termo(string label, string valor)
                        {
                            row.RelativeItem().PaddingRight(10).Column(c =>
                            {
                                c.Item().Text(label).FontSize(7).Bold().FontColor(TextMuted2).LetterSpacing(0.05f);
                                c.Item().PaddingTop(3).Text(valor).FontSize(8.5f).LineHeight(1.4f);
                            });
                        }

                        Termo("DADOS", "Hospedagem em nuvem no Brasil, em conformidade com a LGPD");
                        Termo("REAJUSTE", "Anual, pelo IPCA acumulado");
                        Termo("CONFIDENCIALIDADE", "NDA mútuo assinado antes de qualquer coleta");
                        Termo("VALIDADE DA PROPOSTA", "30 dias a partir da data de emissão");
                    });

                    // 13 — Próximos passos
                    body.Item().PaddingTop(18);
                    SectionTitle(body, "13", "Próximos passos");
                    body.Item().PaddingTop(8).Row(row =>
                    {
                        void Passo(string n, string t, string d)
                        {
                            row.RelativeItem().PaddingRight(10).Column(c =>
                            {
                                c.Item().Text(n).FontSize(8).Bold().FontColor(BrandBlue);
                                c.Item().PaddingTop(4).Text(t).FontSize(9.5f).Bold();
                                c.Item().PaddingTop(3).Text(d).FontSize(8).FontColor(TextMuted).LineHeight(1.5f);
                            });
                        }

                        Passo("01", "Reunião de alinhamento", "60 minutos com TI e diretoria para revisar o diagnóstico atual.");
                        Passo("02", "NDA e escopo", "Assinatura do NDA mútuo e levantamento de ativos e integrações.");
                        Passo("03", "Piloto de 30 dias", "Camada Discover em operação real, com relatório executivo ao final.");
                        Passo("04", "Contratação", "Proposta financeira formal e início do cronograma de 90 dias.");
                    });

                    body.Item().PaddingTop(14).Background(BrandBg).Padding(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Comece pelo piloto").FontSize(13).Bold().FontColor(Colors.White);
                            c.Item().PaddingTop(4).Text(
                                $"Trinta dias, sem compromisso, para a {empresa.Nome} ver o próprio parque em uma única tela — e decidir com dados na mão.")
                                .FontSize(8.5f).FontColor("#C4D3E6").LineHeight(1.5f);
                        });
                        row.ConstantItem(160).Column(c =>
                        {
                            c.Item().Text("CONTATO COMERCIAL").FontSize(6.5f).FontColor(TextMuted2).LetterSpacing(0.08f);
                            c.Item().PaddingTop(3).Text("L'okta IA").FontSize(9.5f).Bold().FontColor(Colors.White);
                            c.Item().Text("comercial@loktaia.com").FontSize(8.5f).FontColor(BrandBlue);
                        });
                    });

                    // Anexo A
                    body.Item().PaddingTop(20);
                    SectionTitle(body, "A", "Anexo A — Catálogo dos 15 módulos");
                    body.Item().PaddingTop(6).Background("#F0F5FF").Padding(10).Text(
                        "Roadmap do produto, construído por fase. O módulo 01 (Attack Surface Management) já está em operação e é a base do " +
                        "diagnóstico desta proposta — os demais entram em produção conforme o cronograma de implantação (seção 10) e a integração " +
                        "com as ferramentas que a empresa já usa.")
                        .FontSize(8.5f).FontColor("#1C2836").LineHeight(1.5f);
                    body.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(20);
                            cd.RelativeColumn(2.2f);
                            cd.RelativeColumn(1.2f);
                            cd.RelativeColumn(3.6f);
                        });

                        void Head(string t) => table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(4).Text(t).FontSize(7).Bold().FontColor(TextMuted2).LetterSpacing(0.05f);
                        Head("#");
                        Head("MÓDULO");
                        Head("CAMADA");
                        Head("VANTAGEM");

                        (string N, string M, string C, string V)[] modulos =
                        [
                            ("01", "Attack Surface Management", "Discover", "Saber o que o atacante vê, antes dele."),
                            ("02", "Inventário Inteligente", "Discover", "Nenhum ativo fora do radar — nem o que a TI não sabia que existia."),
                            ("03", "Certificados e Licenças", "Discover", "Fim das quedas por certificado vencido e das renovações esquecidas."),
                            ("04", "Patch Management", "Protect", "Uma lista só do que está desatualizado, ordenada por risco real."),
                            ("05", "Identidade e Acessos", "Protect", "Excesso de administradores e contas órfãs viram item de painel, não surpresa."),
                            ("06", "SIEM e SOC", "Protect", "Milhares de eventos viram os poucos que merecem atenção."),
                            ("07", "Vulnerability Management", "Analyze", "Uma fila única entre todos os scanners, priorizada por impacto financeiro."),
                            ("08", "IA Consultora", "Analyze", "Explicação em português, com prioridade e tempo de correção estimado."),
                            ("09", "IA de Investigação", "Analyze", "Causa raiz em minutos, cruzando todas as fontes de uma vez."),
                            ("10", "Cyber Digital Twin", "Analyze", "Um mapa que qualquer não técnico entende em cinco segundos."),
                            ("11", "Executive Dashboard", "Govern", "A diretoria acompanha segurança sem depender de tradução técnica."),
                            ("12", "Compliance Automático", "Govern", "Aderência a LGPD, ISO e NIST medida sempre, não só na véspera da auditoria."),
                            ("13", "Gestão de Terceiros", "Govern", "Score por fornecedor — cada vez mais exigido em contratos e auditorias."),
                            ("14", "Relatórios Automáticos", "Govern", "Semanal, mensal e trimestral prontos sem ninguém montar planilha."),
                            ("15", "Gestão Financeira", "Govern", "Custo da segurança, custo do incidente e custo evitado, lado a lado."),
                        ];

                        foreach (var m in modulos)
                        {
                            table.Cell().PaddingVertical(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(m.N).FontSize(7.5f).FontColor(TextMuted);
                            table.Cell().PaddingVertical(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(m.M).FontSize(8.5f).Bold();
                            table.Cell().PaddingVertical(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(m.C).FontSize(8).FontColor(BrandBlue);
                            table.Cell().PaddingVertical(6).PaddingRight(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Text(m.V).FontSize(8).FontColor(TextMuted).LineHeight(1.4f);
                        }
                    });

                    // Anexo B
                    body.Item().PaddingTop(20);
                    SectionTitle(body, "B", "Anexo B — Integrações suportadas");
                    body.Item().PaddingTop(6).Text(
                        "O parque atual é coberto pelo catálogo padrão. Sistemas fora desta lista são atendidos por conector sob medida.")
                        .FontSize(9).FontColor(TextMuted).LineHeight(1.5f);

                    body.Item().PaddingTop(10).Row(row =>
                    {
                        void Grupo(string titulo, string itens)
                        {
                            row.RelativeItem().PaddingRight(10).PaddingBottom(12).Column(c =>
                            {
                                c.Item().Text(titulo).FontSize(7).Bold().FontColor(BrandBlue).LetterSpacing(0.06f);
                                c.Item().PaddingTop(4).Text(itens).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.5f);
                            });
                        }

                        Grupo("PERÍMETRO E REDE", "Fortinet FortiGate · Palo Alto · Cisco · Sophos · VPN corporativa · Cloudflare");
                        Grupo("ENDPOINT E EDR", "Microsoft Defender · CrowdStrike · Sophos · antivírus corporativos");
                    });
                    body.Item().Row(row =>
                    {
                        void Grupo(string titulo, string itens)
                        {
                            row.RelativeItem().PaddingRight(10).PaddingBottom(12).Column(c =>
                            {
                                c.Item().Text(titulo).FontSize(7).Bold().FontColor(BrandBlue).LetterSpacing(0.06f);
                                c.Item().PaddingTop(4).Text(itens).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.5f);
                            });
                        }

                        Grupo("IDENTIDADE", "Active Directory · Entra ID · Okta · Google Workspace · LDAP");
                        Grupo("NUVEM", "Microsoft Azure · AWS · Google Cloud · Microsoft 365");
                    });
                    body.Item().Row(row =>
                    {
                        void Grupo(string titulo, string itens)
                        {
                            row.RelativeItem().PaddingRight(10).PaddingBottom(12).Column(c =>
                            {
                                c.Item().Text(titulo).FontSize(7).Bold().FontColor(BrandBlue).LetterSpacing(0.06f);
                                c.Item().PaddingTop(4).Text(itens).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.5f);
                            });
                        }

                        Grupo("VULNERABILIDADES", "Nessus · Qualys · OpenVAS · Rapid7 · Microsoft Defender · Wazuh");
                        Grupo("SIEM E OBSERVABILIDADE", "Microsoft Sentinel · Splunk · Elastic · QRadar · Wazuh · Graylog · Zabbix");
                    });
                    body.Item().Row(row =>
                    {
                        void Grupo(string titulo, string itens)
                        {
                            row.RelativeItem().PaddingRight(10).Column(c =>
                            {
                                c.Item().Text(titulo).FontSize(7).Bold().FontColor(BrandBlue).LetterSpacing(0.06f);
                                c.Item().PaddingTop(4).Text(itens).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.5f);
                            });
                        }

                        Grupo("BACKUP", "Veeam · backup nativo de nuvem · rotinas locais");
                        Grupo("SUPERFÍCIE EXTERNA", "DNS · MX · SPF · DMARC · DKIM · SSL · ICP-Brasil · registros de domínio");
                    });
                });
            });
        });

        return documento.GeneratePdf();
    }

    /// <summary>
    /// A parte da proposta que vem do levantamento em reunião, e não da varredura.
    ///
    /// ⚠️ **Toda esta seção precisa dizer que é declarada.** O restante da proposta apoia-se em
    /// medição; esta apoia-se na palavra do cliente. Apagar essa distinção é o que faz um auditor
    /// derrubar o documento inteiro na primeira pergunta — e, com ele, a credibilidade das seções
    /// que estavam certas.
    /// </summary>
    private static void DiagnosticoConduzido(
        ColumnDescriptor body,
        Company empresa,
        Models.Diagnostico diagnostico,
        Services.Diagnostico.ResultadoDoDiagnostico r,
        List<DiagnosticoRisco> riscos)
    {
        body.Item().PaddingTop(20).Text("Além do que a varredura alcança")
            .FontSize(12.5f).Bold().FontColor("#0B1220");

        var quando = diagnostico.ConcluidoEm ?? diagnostico.CriadoEm;
        var respondente = string.IsNullOrWhiteSpace(diagnostico.Respondente)
            ? "a equipe da empresa"
            : $"{diagnostico.Respondente}{(string.IsNullOrWhiteSpace(diagnostico.RespondenteCargo) ? "" : $", {diagnostico.RespondenteCargo}")}";

        body.Item().PaddingTop(6).Text(
            $"A varredura mede o que se vê de fora. Já backup, identidade, resposta a incidente e governança não aparecem numa " +
            $"análise externa — e são justamente onde um incidente costuma decidir se a empresa volta a operar. Por isso conduzimos " +
            $"um levantamento estruturado com {respondente} em {quando.ToLocalTime():dd/MM/yyyy}, cobrindo " +
            $"{Services.Diagnostico.CatalogoDeDominios.Todos.Count} domínios de segurança.")
            .FontSize(9.5f).LineHeight(1.6f);

        body.Item().PaddingTop(10).Row(row =>
        {
            void Indicador(string valor, string sufixo, string label, string descricao, string cor)
            {
                row.RelativeItem().PaddingRight(8).Background("#F6F8FB").Padding(11).Column(c =>
                {
                    c.Item().Row(rr =>
                    {
                        rr.AutoItem().Text(valor).FontSize(20).Bold().FontColor(cor);
                        rr.AutoItem().PaddingTop(9).PaddingLeft(1).Text(sufixo).FontSize(9).FontColor(TextMuted2);
                    });
                    c.Item().PaddingTop(3).Text(label).FontSize(7.5f).Bold().FontColor(TextMuted2).LetterSpacing(0.06f);
                    c.Item().PaddingTop(2).Text(descricao).FontSize(8).FontColor(TextMuted).LineHeight(1.35f);
                });
            }

            var corCob = r.Cobertura >= 70 ? BrandGreen : r.Cobertura >= 40 ? BrandYellow : BrandRed;
            Indicador(r.Cobertura.ToString(), "%", "COBERTURA", "dos controles esperados existem", corCob);
            Indicador(r.Maturidade?.ToString("0.0") ?? "—", "/5", "MATURIDADE", "quão bem gerenciado é o que existe", BrandBg);
            Indicador(r.UsoDoInvestimento?.ToString() ?? "—", "%", "USO DO INVESTIMENTO", "do que já foi pago está em uso", BrandBg);
        });

        // A separação entre cobertura e maturidade é o argumento comercial inteiro: quando a
        // primeira é alta e a segunda é baixa, o cliente não precisa comprar — precisa operar.
        if (r.Maturidade is { } mat && r.Cobertura >= 60 && mat < 3m)
        {
            body.Item().PaddingTop(10).Background("#EEF4FF").Padding(11).Text(
                $"A {empresa.Nome} já tem a maior parte dos controles que se espera de um ambiente do seu porte — a cobertura de " +
                $"{r.Cobertura}% mostra isso. O que a maturidade de {mat:0.0} indica é outra coisa: essas ferramentas não estão " +
                "sendo operadas. Licença vencida, alerta que ninguém lê, backup que nunca foi restaurado para teste. " +
                "Esta proposta trata desse problema — não de comprar mais tecnologia.")
                .FontSize(9).FontColor("#1C2836").LineHeight(1.55f);
        }

        // O mapa do ambiente. Um percentual não diz se o buraco é o backup ou a borda; o desenho
        // diz — e é ele que sustenta a conversa com quem decide.
        var mapa = Services.Diagnostico.MapaDaArquitetura.Montar(diagnostico);
        if (mapa.Count > 0)
        {
            body.Item().PaddingTop(14).Text("O ambiente, camada a camada")
                .FontSize(9).Bold().FontColor("#0B1220");

            // O MESMO SVG que a tela mostra. Gerar o desenho duas vezes, por caminhos diferentes,
            // faria os dois divergirem na primeira alteração — e o cliente veria uma coisa na
            // reunião e outra no documento.
            body.Item().PaddingTop(8)
                .Svg(Services.Diagnostico.DiagramaDeRede.Gerar(mapa, Services.Diagnostico.TemaDoDiagrama.Claro));

            body.Item().PaddingTop(6).Row(row =>
            {
                foreach (var estado in new[]
                {
                    Services.Diagnostico.EstadoDaCamada.Protegido,
                    Services.Diagnostico.EstadoDaCamada.Parcial,
                    Services.Diagnostico.EstadoDaCamada.Descoberto,
                    Services.Diagnostico.EstadoDaCamada.NaoAvaliado,
                })
                {
                    var cor = Services.Diagnostico.MapaDaArquitetura.Cor(estado);
                    row.AutoItem().PaddingRight(14).Row(r =>
                    {
                        r.AutoItem().PaddingTop(2).Width(7).Height(7).Background(cor);
                        r.AutoItem().PaddingLeft(4)
                            .Text(Services.Diagnostico.MapaDaArquitetura.Rotulo(estado).ToLowerInvariant())
                            .FontSize(7.5f).FontColor(TextMuted);
                    });
                }
            });

            var naoAvaliadas = mapa.Count(c => c.Estado == Services.Diagnostico.EstadoDaCamada.NaoAvaliado);
            if (naoAvaliadas > 0)
            {
                // Dito na cara: camada sem resposta não é camada sem problema.
                body.Item().PaddingTop(6).Text(
                    $"{naoAvaliadas} camada(s) não foram avaliadas neste levantamento. Isso não significa que estejam bem — "
                    + "significa que ainda não olhamos, e fazê-lo é parte do trabalho proposto.")
                    .FontSize(8).FontColor(TextMuted).Italic();
            }
        }

        if (riscos.Count > 0)
        {
            body.Item().PaddingTop(12).Text("Lacunas de maior gravidade identificadas")
                .FontSize(9).Bold().FontColor("#0B1220");

            foreach (var risco in riscos.Take(6))
            {
                var cor = risco.Gravidade switch
                {
                    GravidadeRisco.Critico => BrandRed,
                    GravidadeRisco.Alto => BrandOrange,
                    GravidadeRisco.Medio => BrandYellow,
                    _ => TextMuted,
                };

                body.Item().PaddingTop(7).Row(row =>
                {
                    row.ConstantItem(52).PaddingTop(1)
                        .Text(risco.Gravidade.ToString().ToUpperInvariant())
                        .FontSize(6.5f).Bold().FontColor(cor).LetterSpacing(0.05f);

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(risco.Titulo).FontSize(9).Bold().FontColor("#1C2836");
                        if (risco.SeNaoTratar is { Length: > 0 } consequencia)
                        {
                            c.Item().PaddingTop(1).Text(consequencia).FontSize(8.5f).FontColor(TextMuted).LineHeight(1.45f);
                        }
                    });
                });
            }
        }

        // A ressalva de origem fecha a seção e não é rodapé decorativo: é o que mantém o documento
        // de pé quando alguém do outro lado perguntar como sabemos disso.
        var declaradas = r.PorOrigem.GetValueOrDefault(OrigemDaInformacao.Declarado);
        var total = r.PorOrigem.Values.Sum();
        var tudoDeclarado = total > 0 && declaradas == total;

        body.Item().PaddingTop(12).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Text(
            tudoDeclarado
                ? $"Como sabemos disso: as {total} respostas deste levantamento foram informadas pela equipe da {empresa.Nome} " +
                  "durante a reunião e não passaram por verificação técnica independente. Diferente da seção anterior, que " +
                  "mede a superfície externa, esta reflete o que a empresa relata sobre o próprio ambiente. Validar " +
                  "tecnicamente cada ponto é parte do trabalho proposto, não um pré-requisito dele."
                : $"Como sabemos disso: das {total} respostas deste levantamento, {declaradas} foram informadas pela equipe e as " +
                  "demais têm evidência anexada ou verificação técnica. Cada item do relatório detalhado indica a própria origem.")
            .FontSize(8.5f).FontColor(TextMuted).LineHeight(1.5f);
    }

    private static void SectionTitle(ColumnDescriptor body, string numero, string titulo)
    {
        body.Item().Row(row =>
        {
            row.AutoItem().Width(22).Text(numero).FontSize(9).Bold().FontColor(BrandBlue);
            row.RelativeItem().PaddingLeft(6).Text(titulo).FontSize(15).Bold().FontColor("#0B1220");
        });
    }

    private static void LayerBlock(ColumnDescriptor body, string numero, string nome, string descricaoCamada, string cor, (string Titulo, string Desc)[] modulos)
    {
        body.Item().PaddingTop(14).Background("#0B1220").Padding(10).Row(row =>
        {
            row.AutoItem().Width(20).Text(numero).FontSize(9).Bold().FontColor(cor);
            row.AutoItem().PaddingLeft(4).Text(nome).FontSize(12).Bold().FontColor(Colors.White);
            row.RelativeItem().PaddingLeft(10).AlignMiddle().Text(descricaoCamada).FontSize(8).FontColor("#9DB0C8");
        });

        body.Item().PaddingTop(8).Row(row =>
        {
            for (var i = 0; i < modulos.Length; i++)
            {
                var m = modulos[i];
                if (i > 0)
                {
                    row.ConstantItem(10);
                }

                row.RelativeItem().Column(c =>
                {
                    c.Item().BorderLeft(2).BorderColor(cor).PaddingLeft(8).Text(m.Titulo).FontSize(9).Bold();
                    c.Item().PaddingLeft(10).PaddingTop(3).Text(m.Desc).FontSize(7.8f).FontColor("#5A7191").LineHeight(1.45f);
                });
            }
        });
    }

    private static string ReferenciaSlug(string nome)
    {
        var letras = new string(nome.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        if (letras.Length == 0)
        {
            return "CLI";
        }

        return letras.Length >= 3 ? letras[..3] : letras.PadRight(3, 'X');
    }
}
