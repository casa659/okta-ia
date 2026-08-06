using System.Text.Json;

namespace OktaIA.Web.Services;

/// <summary>
/// Conteúdo estático do site institucional (marketing), extraído literalmente do mockup
/// "Okta-IA Site Institucional". Decorativo/copy — não é dado de negócio real, por isso não
/// vira EF Core model (diferente dos módulos do SOC interno, que usam Postgres de verdade).
/// </summary>
public static class MarketingContent
{
    public record Layer(string N, string T, string D);
    public record Diff(string N, string T, string D);
    public record Client(string N);
    public record Commitment(string T, string C, string D);
    public record Partner(string N, string T, string D);
    public record ServiceItem(string T, string D);
    public record ServiceGroup(string N, string T, string C, bool Dark, IReadOnlyList<ServiceItem> Items);
    public record Step(string N, string T, string D);
    public record AiPoint(string T);
    public record LgpdCard(string N, string T, string D);
    public record Framework(string T, string D, int P, string C);
    public record SecCard(string K, string C, string T, string D);
    public record ArchLayer(string K, string C, string T, string D);
    public record Post(string Tag, string C, string T, string Date, string? D = null, string? B = null);
    public record Field(string L, string P);
    public record Faq(string Q, string A);
    public record FooterCol(string T, IReadOnlyList<(string T, string Page)> Items);
    public record PlanMatrixRow(string T, string Essential, string Professional, string Enterprise);
    public record PlanMatrixGroup(string T, IReadOnlyList<PlanMatrixRow> Rows);

    public record Plan(
        string Id, string N, string C, string Tag, bool Best, string Sub,
        string Range, string Porte, string User, double Mult, int[] Rates,
        IReadOnlyList<string> Items);

    public static readonly IReadOnlyList<Plan> Plans = new[]
    {
        new Plan("essential", "Essential", "#4D9BFF", "Até 50 estações", false,
            "Para empresas que precisam sair da cegueira e ter monitoramento profissional pela primeira vez.",
            "R$ 2.000 a R$ 5.000", "R$ 3.500", "R$ 18", 1, new[] { 20, 80, 300},
            new[] { "Monitoramento contínuo de disponibilidade", "Dashboard em tempo real", "Vulnerability Scan contínuo", "Inventário de ativos", "Score de segurança", "Relatórios mensais", "Alertas por e-mail", "Suporte em horário comercial" }),
        new Plan("professional", "Professional", "#00E0A4", "Até 200 estações", true,
            "Para quem já tem TI estruturada e precisa de gestão de risco, conformidade e teste ofensivo recorrente.",
            "R$ 8.000 a R$ 20.000", "R$ 9.000", "R$ 42", 1.8, new[] { 36, 145, 540 },
            new[] { "Tudo do Essential", "SIEM com correlação de eventos", "Gestão de vulnerabilidades com SLA", "Pentest trimestral", "Conformidade LGPD", "Conformidade ISO 27001 e NIST", "Gestão de riscos", "Patch management", "Inventário de hardware e software", "Backup monitor", "Tickets e fluxo de aprovação", "Relatórios executivos", "Alertas por WhatsApp, Teams e Slack", "Suporte 12×5 com SLA de 4h" }),
        new Plan("enterprise", "Enterprise", "#8A7BFF", "Acima de 500 estações", false,
            "Para operações críticas: SOC as a Service com prioridade máxima, resposta a incidentes em campo e IA investigando o seu ambiente.",
            "R$ 30.000 a R$ 100.000+", "Sob consulta", "Sob consulta", 0, new[] { 0, 0, 0 },
            new[] { "Tudo do Professional", "SOC as a Service com resposta prioritária", "Inteligência Artificial em todos os módulos", "Resposta a incidentes em campo", "Threat Intelligence", "EDR e MDR gerenciados", "Dark Web Monitoring", "Integração Microsoft, Google e AWS", "Consultoria dedicada e reunião mensal de risco", "White label e multiempresa", "On-premises ou híbrido", "API aberta e webhooks", "SLA de primeira resposta em 15 min", "Gerente de conta técnico" }),
    };

    public static readonly IReadOnlyList<PlanMatrixGroup> Matrix = new[]
    {
        new PlanMatrixGroup("Monitoramento e visibilidade", new[]
        {
            new PlanMatrixRow("Dashboard em tempo real", "T", "T", "T"),
            new PlanMatrixRow("Monitoramento contínuo de disponibilidade", "T", "T", "T"),
            new PlanMatrixRow("Score de segurança", "T", "T", "T"),
            new PlanMatrixRow("Inventário de ativos", "T", "T", "T"),
            new PlanMatrixRow("Inventário de hardware e software", "—", "T", "T"),
            new PlanMatrixRow("Backup monitor", "—", "T", "T"),
            new PlanMatrixRow("Dark Web Monitoring", "—", "—", "T"),
        }),
        new PlanMatrixGroup("Vulnerabilidades e testes", new[]
        {
            new PlanMatrixRow("Vulnerability scan contínuo", "T", "T", "T"),
            new PlanMatrixRow("Gestão de vulnerabilidades com SLA", "—", "T", "T"),
            new PlanMatrixRow("Patch management", "—", "T", "T"),
            new PlanMatrixRow("Pentest", "—", "Trimestral", "Mensal"),
            new PlanMatrixRow("Red team e simulação de adversário", "—", "—", "T"),
        }),
        new PlanMatrixGroup("Detecção e resposta", new[]
        {
            new PlanMatrixRow("Alertas por e-mail", "T", "T", "T"),
            new PlanMatrixRow("Alertas por WhatsApp, Teams e Slack", "—", "T", "T"),
            new PlanMatrixRow("SIEM com correlação de eventos", "—", "T", "T"),
            new PlanMatrixRow("SOC as a Service com resposta prioritária", "—", "—", "T"),
            new PlanMatrixRow("Inteligência Artificial analisando eventos", "—", "Parcial", "T"),
            new PlanMatrixRow("Resposta a incidentes", "—", "Remota", "Em campo"),
            new PlanMatrixRow("EDR e MDR gerenciados", "—", "—", "T"),
            new PlanMatrixRow("Threat Intelligence", "—", "—", "T"),
        }),
        new PlanMatrixGroup("Governança e conformidade", new[]
        {
            new PlanMatrixRow("Relatórios mensais", "T", "T", "T"),
            new PlanMatrixRow("Relatórios executivos para o conselho", "—", "T", "T"),
            new PlanMatrixRow("Conformidade LGPD", "—", "T", "T"),
            new PlanMatrixRow("Conformidade ISO 27001 e NIST", "—", "T", "T"),
            new PlanMatrixRow("Gestão de riscos", "—", "T", "T"),
            new PlanMatrixRow("Trilha de auditoria imutável", "T", "T", "T"),
            new PlanMatrixRow("Gestão de usuários e MFA", "T", "T", "T"),
        }),
        new PlanMatrixGroup("Plataforma e integração", new[]
        {
            new PlanMatrixRow("Tickets e fluxo de aprovação", "—", "T", "T"),
            new PlanMatrixRow("Integração Microsoft, Google e AWS", "—", "Parcial", "T"),
            new PlanMatrixRow("API aberta e webhooks", "—", "T", "T"),
            new PlanMatrixRow("White label e multiempresa", "—", "—", "T"),
            new PlanMatrixRow("On-premises ou híbrido", "—", "—", "T"),
            new PlanMatrixRow("Consultoria dedicada", "—", "—", "T"),
        }),
    };

    public record AssessStep(string N, string T, string D);
    public record AssessDeliver(string T, string D);

    public static readonly IReadOnlyList<AssessStep> AssessSteps = new[]
    {
        new AssessStep("01", "Autorização", "Assinamos o termo de escopo e confidencialidade. Nada é testado sem sua autorização formal por escrito."),
        new AssessStep("02", "Varredura", "Mapeamos sua superfície exposta: domínios, subdomínios, portas, certificados, e-mail e credenciais vazadas."),
        new AssessStep("03", "Análise", "Nossos especialistas validam os achados manualmente e descartam falso positivo antes de qualquer conclusão."),
        new AssessStep("04", "Entrega", "Em até 48 horas você recebe o relatório executivo com riscos, impacto financeiro estimado e plano de mitigação."),
    };

    public static readonly IReadOnlyList<AssessDeliver> AssessDeliverItems = new[]
    {
        new AssessDeliver("Vulnerabilidades encontradas", "Lista priorizada por probabilidade real de exploração, não só por CVSS."),
        new AssessDeliver("Riscos ao negócio", "O que cada achado permite que um atacante faça na prática no seu ambiente."),
        new AssessDeliver("Impacto financeiro estimado", "Custo projetado de indisponibilidade, vazamento e multa regulatória."),
        new AssessDeliver("Situação de conformidade LGPD", "Onde o cenário atual não sustenta o que a lei exige em medidas de segurança."),
        new AssessDeliver("Plano de mitigação", "Ações ordenadas por esforço e retorno, com prazo sugerido para cada uma."),
        new AssessDeliver("Credenciais vazadas", "E-mails corporativos encontrados em vazamentos públicos e mercados fechados."),
    };

    public static readonly IReadOnlyList<Faq> PlanFaqs = new[]
    {
        new Faq("O Security Assessment gratuito tem alguma pegadinha?", "Não. Você assina o termo de escopo, executamos a varredura externa e entregamos o relatório em até 48 horas. Se decidir não seguir, o relatório é seu e não há cobrança nem obrigação. Fazemos isso porque, na prática, o diagnóstico vende melhor que qualquer apresentação."),
        new Faq("Qual modelo de cobrança é melhor para mim?", "Por porte é mais simples e previsível para empresas com parque estável. Por ativo é mais justo quando o ambiente é muito heterogêneo — poucas estações e muitos servidores, por exemplo. Por usuário funciona melhor quando o risco está nas pessoas, como em escritórios e operações comerciais. Na proposta simulamos os três e mostramos qual sai mais barato para o seu caso."),
        new Faq("Posso começar no Essential e subir depois?", "Sim, e é o caminho mais comum. A migração é feita sem reimplantação: os mesmos agentes e conectores passam a alimentar os módulos adicionais. O histórico de eventos e o inventário são preservados integralmente."),
        new Faq("Existe fidelidade ou multa de cancelamento?", "O contrato padrão é de 12 meses com 15% de desconto no pagamento anual, ou mensal sem fidelidade a preço cheio. Em contrato anual, o cancelamento antecipado cobra apenas as mensalidades já usufruídas com o desconto revertido — não há multa punitiva."),
        new Faq("O que o Enterprise tem que realmente justifica o valor?", "Três coisas: prioridade máxima na fila de resposta com SLA de 15 minutos, resposta a incidentes com deslocamento em campo quando necessário, e consultoria dedicada com reunião mensal de risco. O software é o mesmo; o que muda é a operação gerenciada por trás dele."),
        new Faq("Atendem órgãos públicos e licitação?", "Sim. Temos plano Gov com faturamento adequado a empenho, opção on-premises para dados que não podem sair do datacenter do órgão, e apoio na elaboração do termo de referência."),
    };

    public static readonly IReadOnlyList<Layer> Layers = new[]
    {
        new Layer("DISCOVER", "Descoberta automática", "Ativos, serviços, domínios e exposição na internet mapeados continuamente — inclusive o que a equipe interna não sabia que existia."),
        new Layer("PROTECT", "Integração com o que você já tem", "Firewall, EDR, antivírus, SIEM, IAM, backup e nuvem conectados numa única operação, sem trocar de fornecedor."),
        new Layer("ANALYZE", "IA correlacionando e priorizando", "Eventos correlacionados, risco calculado, vulnerabilidades priorizadas e conformidade avaliada em tempo real."),
        new Layer("GOVERN", "Governança para quem decide", "Dashboards executivos, relatórios de auditoria, gestão de terceiros, KPIs e acompanhamento de planos de ação."),
    };

    public static readonly IReadOnlyList<Diff> Diffs = new[]
    {
        new Diff("01", "IA integrada", "Classificação, correlação e recomendação em todos os módulos, não como recurso à parte."),
        new Diff("02", "Resposta com SLA", "Monitoramento contínuo e escalonamento gerenciado, com SLA de primeira resposta a partir de 15 minutos."),
        new Diff("03", "Arquitetura cloud", "Nuvem, on-premises ou híbrido, com o mesmo console e as mesmas garantias."),
        new Diff("04", "Escalabilidade real", "De 50 a 100 mil ativos sem troca de plataforma ou reimplantação."),
        new Diff("05", "API aberta", "REST, GraphQL, WebSocket e webhooks documentados — nada fica preso na interface."),
        new Diff("06", "Alta disponibilidade", "Redundância multirregião e failover automático com SLA contratual de 99,98%."),
        new Diff("07", "Painel executivo", "Leitura de risco em linguagem de negócio, pronta para diretoria e conselho."),
        new Diff("08", "Auditoria completa", "Trilha imutável e assinada de toda ação humana ou automatizada na plataforma."),
        new Diff("09", "White label", "Parceiros entregam a plataforma com a própria marca, domínio e identidade visual."),
        new Diff("10", "Multiempresa", "Isolamento lógico total por organização, com visão consolidada para o MSSP."),
    };

    public static readonly IReadOnlyList<Client> Clients = new[]
    {
        new Client("Saúde"), new Client("Financeiro"), new Client("Indústria"),
        new Client("Governo"), new Client("Jurídico"), new Client("Provedores"),
    };

    public static readonly IReadOnlyList<string> Expertise = new[]
    {
        "Cyber Security", "Desenvolvimento de software", "Arquitetura corporativa", "Cloud computing",
        "Infraestrutura", "Ambientes hospitalares", "Mercado financeiro", "Grandes empresas",
        "Projetos corporativos", "Compliance", "LGPD", "Governança de TI", "Automação",
        "Inteligência Artificial", "Segurança da informação", "Resposta a incidentes",
    };

    public static readonly IReadOnlyList<Commitment> Commitments = new[]
    {
        new Commitment("Segurança da informação", "#4D9BFF", "Criptografia em trânsito e em repouso, segregação de ambientes e revisão de acessos a cada 90 dias."),
        new Commitment("Ética e transparência", "#00E0A4", "Canal de denúncias independente, código de conduta público e comunicação honesta sobre limitações técnicas."),
        new Commitment("LGPD", "#8A7BFF", "Tratamento mínimo necessário, base legal documentada e resposta a titulares dentro do prazo legal."),
        new Commitment("Compliance", "#FFC93C", "Controles alinhados a ISO 27001, ISO 27701 e NIST CSF, com auditoria interna semestral."),
        new Commitment("Continuidade dos negócios", "#FF8A3D", "Plano de continuidade testado, RTO e RPO acordados e comunicação de crise pré-definida."),
    };

    public static readonly IReadOnlyList<Partner> Partners = new[]
    {
        new Partner("LGPD", "Consultorias de privacidade", "Camada técnica de monitoramento e evidência para programas de adequação."),
        new Partner("LEGAL", "Escritórios de advocacia", "Suporte pericial e produção de evidência técnica em incidentes e litígios."),
        new Partner("CLOUD", "Integradores de nuvem", "Avaliação de postura e hardening em projetos de migração e modernização."),
        new Partner("MSP", "Provedores gerenciados", "Plataforma white label com comissionamento recorrente e suporte dedicado."),
    };

    public static readonly IReadOnlyList<ServiceGroup> ServiceGroups = new[]
    {
        new ServiceGroup("01", "Detecção e resposta", "#4D9BFF", false, new[]
        {
            new ServiceItem("SOC as a Service", "Monitoramento contínuo do seu ambiente com tecnologias integradas de parceiros e escalonamento definido em contrato."),
            new ServiceItem("SIEM gerenciado", "Coleta, normalização e correlação de logs de Windows, Linux, firewalls, nuvem, containers e bancos de dados."),
            new ServiceItem("XDR", "Detecção estendida cruzando endpoint, rede, identidade e aplicação para reconstruir a cadeia completa do ataque."),
            new ServiceItem("Resposta a incidentes", "Contenção, erradicação e recuperação conduzidas por especialistas, com relatório pós-incidente formal."),
            new ServiceItem("Monitoramento de disponibilidade", "Verificação contínua de serviços, certificados, DNS e desempenho, com alerta antes que o usuário perceba."),
            new ServiceItem("Proteção contra ransomware", "Detecção comportamental de criptografia em massa, isolamento automático e validação de backups imutáveis."),
        }),
        new ServiceGroup("02", "Prevenção e testes", "#00E0A4", true, new[]
        {
            new ServiceItem("Gestão de vulnerabilidades", "Ciclo completo de descoberta, priorização por exploração real, atribuição de responsável e verificação da correção."),
            new ServiceItem("Pentest", "Testes de intrusão em aplicações web, APIs, redes internas e engenharia social, com reteste incluído."),
            new ServiceItem("Scanner de websites e APIs", "Varredura contínua de OWASP Top 10, cabeçalhos, cookies, CORS, JWT, GraphQL e configuração de TLS."),
            new ServiceItem("Análise de riscos", "Avaliação estruturada de ativos, ameaças e impacto ao negócio, com matriz de risco revisada periodicamente."),
            new ServiceItem("Backup e continuidade", "Monitoramento de rotinas, testes de restauração e apoio na construção do plano de continuidade."),
        }),
        new ServiceGroup("03", "Proteção de aplicações e nuvem", "#8A7BFF", false, new[]
        {
            new ServiceItem("Cloud Security", "Avaliação de postura em AWS, Azure e Google Cloud, com detecção de configurações perigosas e acessos excessivos."),
            new ServiceItem("API Security", "Descoberta de endpoints, inventário de shadow APIs, controle de abuso e validação de esquema em tempo real."),
            new ServiceItem("Segurança para aplicações", "Integração com o pipeline de CI/CD, análise de dependências e bloqueio de builds com vulnerabilidade crítica."),
            new ServiceItem("Proteção de websites", "WAF gerenciado, mitigação de bots e defesa contra DDoS de camada 7 para portais e e-commerce."),
            new ServiceItem("Firewall gerenciado", "Administração de regras, revisão periódica de políticas e correlação dos logs com o SIEM."),
        }),
        new ServiceGroup("04", "Governança e consultoria", "#FFC93C", true, new[]
        {
            new ServiceItem("Adequação à LGPD", "Camada técnica do programa de privacidade: inventário, monitoramento de riscos e produção de evidências."),
            new ServiceItem("Apoio a consultorias de LGPD", "Plataforma white label para escritórios e consultorias entregarem monitoramento contínuo aos próprios clientes."),
            new ServiceItem("Consultoria em segurança", "Diagnóstico de maturidade, desenho de arquitetura segura e roadmap priorizado por risco e orçamento."),
            new ServiceItem("Treinamentos", "Capacitação técnica para times de TI e conscientização para colaboradores, com simulação de phishing."),
        }),
    };

    public static readonly IReadOnlyList<Step> OnboardingSteps = new[]
    {
        new Step("01", "Descoberta", "Mapeamos ativos expostos, domínios, IPs e serviços — inclusive o que a equipe interna não sabia que existia."),
        new Step("02", "Ativação", "Conectores de log, agentes e integrações de nuvem em produção, sem janela de indisponibilidade."),
        new Step("03", "Calibração", "Os modelos aprendem a linha de base do seu ambiente por 14 dias, reduzindo falso positivo antes do go-live."),
        new Step("04", "Operação", "A operação gerenciada assume o monitoramento, com reunião mensal de risco e relatório executivo recorrente."),
    };

    public static readonly IReadOnlyList<string> Modules = new[]
    {
        "Dashboard", "Empresas", "Usuários", "Perfis", "Clientes", "Websites", "APIs", "Servidores",
        "Firewalls", "Monitoramento", "Vulnerabilidades", "Scanner", "Pentest", "Backup", "Logs",
        "Auditoria", "Incidentes", "SIEM", "SOC", "IA", "Relatórios", "Financeiro", "Configurações",
        "API", "Integrações",
    };

    public static readonly IReadOnlyList<AiPoint> AiPoints = new[]
    {
        new AiPoint("Correlaciona eventos de fontes distintas para reconstruir a cadeia completa do ataque."),
        new AiPoint("Prioriza vulnerabilidades pela probabilidade real de exploração, não apenas pelo CVSS."),
        new AiPoint("Detecta desvio comportamental de usuários e serviços sem depender de assinatura."),
        new AiPoint("Redige o relatório executivo com a narrativa de risco pronta para a diretoria."),
        new AiPoint("Recomenda a mitigação com base em CVEs, boas práticas e no seu próprio inventário."),
    };

    public static readonly IReadOnlyList<LgpdCard> LgpdCards = new[]
    {
        new LgpdCard("ART. 46", "Medidas de segurança", "Evidência contínua de criptografia, controle de acesso, registro de operações e proteção contra acessos não autorizados."),
        new LgpdCard("ART. 37", "Registro de operações", "Trilha imutável de quem acessou qual dado, quando e a partir de onde — exportável para o encarregado."),
        new LgpdCard("ART. 48", "Comunicação de incidente", "Detecção, classificação de impacto e cronologia formal para subsidiar a comunicação à ANPD e aos titulares."),
        new LgpdCard("INVENTÁRIO", "Mapeamento de ativos", "Descoberta automática de sistemas, bancos e integrações que tratam dado pessoal, sempre atualizada."),
        new LgpdCard("RIPD", "Apoio ao relatório de impacto", "Dados técnicos de exposição, vulnerabilidade e controle para instruir o relatório conduzido pelo jurídico."),
        new LgpdCard("FORNECEDOR", "Risco de terceiros", "Monitoramento da superfície exposta de operadores e fornecedores integrados ao seu ambiente."),
    };

    public static readonly IReadOnlyList<Framework> Frameworks = new[]
    {
        new Framework("ISO 27001", "Gestão de segurança da informação", 94, "#00E0A4"),
        new Framework("ISO 27701", "Gestão de privacidade", 88, "#00E0A4"),
        new Framework("NIST CSF", "Identificar, proteger, detectar, responder", 91, "#00E0A4"),
        new Framework("NIST 800-53", "Controles para sistemas federais", 76, "#4D9BFF"),
        new Framework("OWASP", "Top 10 web e API", 97, "#00E0A4"),
        new Framework("MITRE ATT&CK", "Táticas e técnicas adversárias", 89, "#00E0A4"),
        new Framework("CIS Controls", "18 controles críticos", 84, "#4D9BFF"),
        new Framework("COBIT", "Governança de TI", 71, "#4D9BFF"),
        new Framework("ITIL", "Gestão de serviços", 68, "#FFC93C"),
        new Framework("LGPD", "Lei Geral de Proteção de Dados", 92, "#00E0A4"),
    };

    public static readonly IReadOnlyList<SecCard> SecCards = new[]
    {
        new SecCard("CRIPTOGRAFIA", "#4D9BFF", "AES-256 e TLS 1.3", "Dados sensíveis cifrados em repouso com AES-256 e todo tráfego em TLS 1.3, com chaves rotacionadas e custodiadas em cofre dedicado."),
        new SecCard("IDENTIDADE", "#00E0A4", "MFA e passkeys obrigatórios", "Autenticação multifator via TOTP ou passkey obrigatória para todos os perfis, com OAuth2 e OpenID Connect para federação corporativa."),
        new SecCard("AUDITORIA", "#8A7BFF", "Logs assinados e imutáveis", "Cada ação gera registro assinado criptograficamente, sem possibilidade de alteração ou exclusão — inclusive por administradores."),
        new SecCard("ISOLAMENTO", "#4D9BFF", "Segregação por organização", "Dados de cada empresa isolados logicamente em toda a pilha, com validação de escopo em cada consulta e teste automatizado de vazamento entre tenants."),
        new SecCard("DISPONIBILIDADE", "#00E0A4", "Redundância multirregião", "Infraestrutura replicada com failover automático, RTO de 15 minutos e RPO de 5 minutos para os dados críticos."),
        new SecCard("BACKUP", "#FFC93C", "Cópias imutáveis", "Backups diários com retenção escalonada, armazenamento WORM e teste de restauração executado mensalmente."),
        new SecCard("ACESSO", "#8A7BFF", "Privilégio mínimo", "Perfis granulares, acesso just-in-time para operações sensíveis e revisão trimestral de permissões documentada."),
        new SecCard("TESTES", "#FF8A3D", "Pentest na própria plataforma", "Testes de intrusão externos semestrais, programa de divulgação responsável e análise de dependências a cada build."),
        new SecCard("MONITORAMENTO", "#4D9BFF", "Nós somos o cliente zero", "Toda a nossa infraestrutura é monitorada pela própria plataforma, sob os mesmos SLAs entregues aos clientes."),
    };

    public static readonly IReadOnlyList<ArchLayer> ArchLayers = new[]
    {
        new ArchLayer("INGESTÃO", "#4D9BFF", "Coleta distribuída", "Agentes, syslog, APIs de nuvem e webhooks alimentando uma fila resiliente com garantia de entrega."),
        new ArchLayer("PROCESSAMENTO", "#00E0A4", "Correlação em fluxo", "Normalização, enriquecimento com threat intel e aplicação de regras e modelos em tempo real."),
        new ArchLayer("ARMAZENAMENTO", "#8A7BFF", "Quente, morno e frio", "90 dias de retenção quente para busca instantânea e arquivamento imutável de longo prazo."),
        new ArchLayer("ENTREGA", "#FFC93C", "Console, API e alertas", "Interface em tempo real, APIs abertas e notificação por e-mail, WhatsApp, Teams, Slack ou webhook."),
    };

    public static readonly IReadOnlyList<Post> PostsFeatured = new[]
    {
        new Post("RANSOMWARE", "#FF3B5C", "O que muda quando o ransomware para de criptografar e passa só a vazar", "28 JUL 2026", "A extorsão sem criptografia cresceu 40% no último ano e quebra a premissa de que backup resolve. Analisamos o que isso exige da estratégia de detecção.", "rgba(255,59,92,.3)"),
        new Post("LGPD", "#00E0A4", "Evidência técnica: o elo mais frágil dos programas de privacidade", "19 JUL 2026", "Políticas bem escritas não sobrevivem à auditoria sem log, inventário e prova de controle. Como sustentar conformidade com dado, não com documento.", "rgba(0,224,164,.3)"),
        new Post("IA", "#8A7BFF", "Por que 93% dos alertas do seu SOC não deveriam chegar a um humano", "11 JUL 2026", "Fadiga de alerta é problema de arquitetura, não de esforço. O que separa supressão inteligente de simplesmente ignorar evento.", "rgba(138,123,255,.3)"),
    };

    public static readonly IReadOnlyList<Post> PostsList = new[]
    {
        new Post("CVE", "#FF8A3D", "CVSS 10 não significa prioridade 1: o caso do EPSS", "04 JUL 2026"),
        new Post("CLOUD", "#4D9BFF", "Cinco configurações de S3 e Blob que ainda vazam dados em 2026", "27 JUN 2026"),
        new Post("MSP", "#00E0A4", "Como um provedor gerenciado monta um SOC sem contratar 12 analistas", "18 JUN 2026"),
        new Post("PHISHING", "#8A7BFF", "Typosquatting: o domínio falso foi registrado antes do seu treinamento", "09 JUN 2026"),
        new Post("API", "#FFC93C", "Shadow APIs: o inventário que quase ninguém tem e todo atacante encontra", "30 MAI 2026"),
        new Post("SOC", "#FF3B5C", "MTTR de 41 minutos: o que realmente move esse número", "21 MAI 2026"),
    };

    public static readonly IReadOnlyList<Field> ContactFields = new[]
    {
        new Field("Nome completo", "Como devemos chamar você"),
        new Field("E-mail corporativo", "nome@suaempresa.com.br"),
        new Field("Empresa", "Razão social ou nome fantasia"),
        new Field("Telefone", "(11) 90000-0000"),
        new Field("Quantidade de ativos", "Servidores, sites e APIs aproximados"),
        new Field("Como podemos ajudar", "Descreva brevemente seu cenário e sua urgência"),
    };

    public static readonly IReadOnlyList<Faq> Faqs = new[]
    {
        new Faq("Quanto tempo leva para a plataforma começar a gerar valor?", "A descoberta de ativos expostos entrega resultado no primeiro dia. A operação completa, com modelos calibrados para o seu ambiente e falso positivo reduzido, leva em média 14 dias."),
        new Faq("Vocês substituem meu time de TI ou de segurança?", "Não. A plataforma e o SOC as a Service eliminam o trabalho repetitivo de triagem e monitoramento, devolvendo o tempo do seu time para arquitetura, projetos e decisões. Em organizações sem time dedicado, agregamos as tecnologias e a operação gerenciada necessárias para cobrir essa lacuna."),
        new Faq("É possível rodar on-premises?", "Sim. A plataforma é distribuída em containers e opera em nuvem pública, em datacenter próprio ou em modelo híbrido — com o mesmo console e as mesmas funcionalidades. Órgãos públicos e instituições financeiras costumam optar pelo híbrido."),
        new Faq("Como funciona o isolamento entre empresas na versão multiempresa?", "Cada organização tem isolamento lógico completo em toda a pilha: armazenamento, índices de busca, filas e cache. Toda consulta valida escopo no servidor, e executamos testes automatizados de vazamento entre tenants a cada release."),
        new Faq("Vocês prestam consultoria jurídica em LGPD?", "Não. Fornecemos a camada técnica — inventário, monitoramento, evidência e relatórios — que apoia o trabalho do encarregado, do jurídico e das consultorias especializadas. Trabalhamos ao lado desses profissionais, nunca no lugar deles."),
        new Faq("Como é a cobrança?", "Assinatura mensal ou anual por quantidade de ativos monitorados, com planos do Business ao Enterprise+. Serviços pontuais como pentest e resposta a incidentes podem ser contratados à parte ou incluídos no pacote."),
    };

    public static readonly IReadOnlyList<FooterCol> FooterCols = new[]
    {
        new FooterCol("Plataforma", new[] { ("Visão geral", "/Plataforma"), ("Módulos", "/Plataforma"), ("Inteligência Artificial", "/Plataforma"), ("Nossa segurança", "/Seguranca") }),
        new FooterCol("Serviços", new[] { ("SOC as a Service", "/Servicos"), ("Pentest", "/Servicos"), ("Planos e preços", "/Planos"), ("Assessment gratuito", "/Planos"), ("Consultoria", "/Servicos") }),
        new FooterCol("Conformidade", new[] { ("LGPD", "/Lgpd"), ("Compliance", "/Lgpd"), ("ISO 27001", "/Lgpd"), ("NIST CSF", "/Lgpd") }),
        new FooterCol("Empresa", new[] { ("Sobre nós", "/Empresa"), ("Parceiros", "/Empresa"), ("Blog", "/Blog"), ("Contato", "/Contato") }),
    };

    public record FeedItem(string Type, string Cc, string Sev)
    {
        public string Color => Sev switch { "crit" => "#FF3B5C", "high" => "#FF8A3D", "med" => "#FFC93C", _ => "#4D9BFF" };
    }

    public static readonly IReadOnlyList<FeedItem> HeroFeed = new[]
    {
        new FeedItem("SQL Injection", "CN", "crit"),
        new FeedItem("Brute Force SSH", "RU", "high"),
        new FeedItem("Credential Stuffing", "US", "high"),
        new FeedItem("DDoS L7 Flood", "VN", "crit"),
    };

    public record PlatformFeedItem(string Type, string Sev, string Time, string Ip, string Target)
    {
        public string Color => Sev switch { "crit" => "#FF3B5C", "high" => "#FF8A3D", "med" => "#FFC93C", _ => "#4D9BFF" };
    }

    public static readonly IReadOnlyList<PlatformFeedItem> PlatformFeed = new[]
    {
        new PlatformFeedItem("SQL Injection", "crit", "14:22:07", "118.42.201.9", "api.grupovector.com"),
        new PlatformFeedItem("Brute Force SSH", "high", "14:21:54", "95.181.44.203", "vpn-sp01"),
        new PlatformFeedItem("Credential Stuffing", "high", "14:21:39", "52.14.88.176", "checkout.pagou.io"),
        new PlatformFeedItem("Port Scan", "low", "14:21:20", "45.9.148.62", "srv-db-prod-02"),
        new PlatformFeedItem("XSS Reflected", "med", "14:20:58", "118.107.6.44", "portal.hsanta.br"),
        new PlatformFeedItem("DDoS L7 Flood", "crit", "14:20:31", "95.216.3.190", "mail.prefdigital.gov.br"),
        new PlatformFeedItem("Path Traversal", "high", "14:20:12", "45.155.204.11", "api.grupovector.com"),
        new PlatformFeedItem("API Rate Abuse", "med", "14:19:47", "52.66.201.5", "checkout.pagou.io"),
    };

    // ORIG/DCS usados pelo mapa (marketing-map.js recebe via data-mk-map-data JSON).
    public static readonly (string Cc, double Lat, double Lng)[] AttackOrigins =
    {
        ("CN", 35, 105), ("RU", 61, 95), ("US", 38, -97), ("IN", 21, 78), ("BR", -12, -51),
        ("VN", 15, 108), ("IR", 32, 53), ("NG", 9, 8), ("UA", 49, 31), ("NL", 52, 5),
        ("ID", -2, 118), ("TR", 39, 35),
    };

    public static readonly (string Code, double Lat, double Lng)[] DataCenters =
    {
        ("SAO1", -23.5, -46.6), ("FRA1", 50.1, 8.7), ("IAD1", 38.9, -77.4),
        ("SCL1", -33.4, -70.6), ("LIS1", 38.7, -9.1),
    };

    public static string MapDataJson() => JsonSerializer.Serialize(new
    {
        origins = AttackOrigins.Select(o => new { cc = o.Cc, lat = o.Lat, lng = o.Lng }),
        dcs = DataCenters.Select(d => new { code = d.Code, lat = d.Lat, lng = d.Lng }),
    });
}
