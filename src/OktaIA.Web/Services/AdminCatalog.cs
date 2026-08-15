namespace OktaIA.Web.Services;

/// <summary>
/// Conteúdo do console de Administração da plataforma. Segue a mesma filosofia do
/// <see cref="MarketingContent"/>: catálogo estático fiel ao mockup para as seções que são
/// vitrine/ilustrativas de engenharia de plataforma (marketplace, conectores, cofre de
/// credenciais, motor de eventos, SOAR, API Gateway, observabilidade, SDK) — não é dado de
/// negócio real, por isso não vira EF model. Empresas/Usuários/Auditoria usam dado real
/// (Company/ApplicationUser/AdminAuditLog) e ficam fora daqui.
/// </summary>
public static class AdminCatalog
{
    public record NavItem(string Id, string Page, string Num, string Label, string? Badge = null, string? BadgeColor = null);
    public record NavGroup(string Title, IReadOnlyList<NavItem> Items);

    public record OverviewKpi(string L, string V, string D, string C);
    public record RecentActivity(string T, string Who, string When, string C);
    public record HealthMetric(string L, string V, int P, string C);

    public static readonly IReadOnlyList<OverviewKpi> OverviewKpis = new[]
    {
        new OverviewKpi("MRR", "R$ 284,7k", "+11,4%", "#00E0A4"),
        new OverviewKpi("Organizações", "8", "+1", "#4D9BFF"),
        new OverviewKpi("Usuários ativos", "42", "+6", "#4D9BFF"),
        new OverviewKpi("Ativos licenciados", "6.788", "+214", "#8A7BFF"),
        new OverviewKpi("Faturas em aberto", "2", "R$ 27,1k", "#FF8A3D"),
    };

    public static readonly IReadOnlyList<RecentActivity> RecentActivities = new[]
    {
        new RecentActivity("Nova organização provisionada · Escritório Lemos", "R. Silva", "8 min", "#00E0A4"),
        new RecentActivity("Perfil de Diego Moraes elevado para Gestor", "R. Silva", "22 min", "#4D9BFF"),
        new RecentActivity("Integração Microsoft Sentinel reconectada", "sistema", "1 h", "#00E0A4"),
        new RecentActivity("Conta de Paula Lemos suspensa por inatividade", "política", "3 h", "#FF8A3D"),
        new RecentActivity("Chave de API rotacionada · Banco Meridiano", "B. Teixeira", "5 h", "#4D9BFF"),
        new RecentActivity("Fatura NF-8838 marcada como vencida", "sistema", "9 h", "#FF3B5C"),
        new RecentActivity("Política de retenção alterada para 5 anos", "R. Silva", "ontem", "#8A7BFF"),
    };

    public static readonly IReadOnlyList<HealthMetric> PlatformHealth = new[]
    {
        new HealthMetric("Ingestão de eventos", "18,4k EPS", 68, "#00E0A4"),
        new HealthMetric("Fila de processamento", "204 ms", 22, "#00E0A4"),
        new HealthMetric("Índice de busca", "2,4 bi docs", 74, "#4D9BFF"),
        new HealthMetric("Armazenamento quente", "81%", 81, "#FF8A3D"),
        new HealthMetric("Disponibilidade da API", "99,99%", 99, "#00E0A4"),
        new HealthMetric("Latência do console", "112 ms", 18, "#00E0A4"),
    };

    // ---------- Marketplace ----------
    public record MarketItem(string N, string Cat, string Vendor, string Ver, string Status, string Trust, double Rating, string Installs)
    {
        public string Ini => string.Concat(N.Split(' ').Where(w => w.Length > 0).Take(2).Select(w => w[0])).ToUpperInvariant();
    }

    public static readonly IReadOnlyList<MarketItem> Market = new[]
    {
        new MarketItem("Microsoft Defender", "EDR / XDR", "Microsoft", "1.4.2", "instalado", "oficial", 4.9, "2.1k"),
        new MarketItem("Microsoft Sentinel", "SIEM", "Microsoft", "2.1.0", "instalado", "oficial", 4.8, "1.8k"),
        new MarketItem("Wazuh", "HIDS / SIEM", "Wazuh Inc.", "1.2.8", "instalado", "verificado", 4.7, "3.4k"),
        new MarketItem("Fortinet FortiGate", "Firewall", "Fortinet", "1.6.1", "instalado", "oficial", 4.6, "2.7k"),
        new MarketItem("CrowdStrike Falcon", "EDR", "CrowdStrike", "1.3.4", "instalado", "oficial", 4.9, "1.9k"),
        new MarketItem("Palo Alto Cortex XDR", "XDR", "Palo Alto", "1.2.0", "atualizar", "oficial", 4.7, "1.4k"),
        new MarketItem("SentinelOne", "EDR", "SentinelOne", "1.0.9", "disponível", "oficial", 4.6, "980"),
        new MarketItem("Sophos Central", "Endpoint", "Sophos", "1.1.2", "disponível", "verificado", 4.4, "760"),
        new MarketItem("Trend Micro Vision One", "XDR", "Trend Micro", "0.9.8", "disponível", "verificado", 4.3, "520"),
        new MarketItem("Elastic Security", "SIEM", "Elastic", "1.3.1", "disponível", "oficial", 4.5, "1.2k"),
        new MarketItem("Splunk Enterprise", "SIEM", "Splunk", "2.0.4", "disponível", "oficial", 4.7, "2.3k"),
        new MarketItem("Qualys VMDR", "Vulnerability Mgmt", "Qualys", "1.1.5", "disponível", "oficial", 4.5, "890"),
        new MarketItem("Rapid7 InsightVM", "Vulnerability Mgmt", "Rapid7", "1.0.6", "disponível", "verificado", 4.4, "670"),
        new MarketItem("Okta Identity", "IAM", "Okta", "1.2.3", "disponível", "oficial", 4.8, "1.5k"),
        new MarketItem("Proofpoint", "E-mail Security", "Proofpoint", "1.0.2", "disponível", "verificado", 4.3, "410"),
        new MarketItem("Zscaler", "SASE / Proxy", "Zscaler", "1.1.0", "disponível", "oficial", 4.5, "620"),
        new MarketItem("pfSense", "Firewall", "Comunidade", "0.8.4", "beta", "comunidade", 4.1, "340"),
        new MarketItem("Suricata", "IDS / IPS", "Comunidade", "0.9.1", "beta", "comunidade", 4.2, "480"),
    };

    // ---------- Conectores instalados ----------
    public record InterfaceMethod(string M, string D, string R);

    public static readonly IReadOnlyList<InterfaceMethod> ConnectorInterface = new[]
    {
        new InterfaceMethod("connect()", "Estabelece a sessão autenticada e persiste o handle no cofre de credenciais.", "Promise<Session>"),
        new InterfaceMethod("disconnect()", "Encerra a sessão, revoga tokens temporários e libera recursos do pool.", "Promise<void>"),
        new InterfaceMethod("testConnection()", "Valida credenciais e alcance de rede sem gravar dado algum. Usado na instalação.", "Promise<TestResult>"),
        new InterfaceMethod("sync(scope, cursor)", "Sincronização incremental por cursor, idempotente e retomável após falha.", "AsyncIterable<Envelope>"),
        new InterfaceMethod("healthCheck()", "Latência, cota restante, defasagem do cursor e último erro observado.", "Promise<Health>"),
        new InterfaceMethod("getCapabilities()", "Declara entidades, direção, granularidade e limites que o conector suporta.", "Capabilities"),
    };

    public record ConnectorItem(string N, string Slug, string Ver, string Cat, string Vendor, string Auth, string[] Caps, string Health, int Uptime, string Sync, string Vol, string Status);

    public static readonly IReadOnlyList<ConnectorItem> Connectors = new[]
    {
        new ConnectorItem("Microsoft Defender", "defender", "1.4.2", "EDR / XDR", "Microsoft", "oauth2", new[] { "alerts", "assets", "incidents", "users" }, "healthy", 98, "2 min", "8.4k", "ativo"),
        new ConnectorItem("Microsoft Sentinel", "sentinel", "2.1.0", "SIEM", "Microsoft", "oauth2", new[] { "alerts", "incidents", "events" }, "healthy", 99, "1 min", "24.1k", "ativo"),
        new ConnectorItem("Wazuh", "wazuh", "1.2.8", "HIDS / SIEM", "Wazuh Inc.", "apikey", new[] { "alerts", "assets", "vulns", "events" }, "degraded", 82, "4 min", "11.7k", "ativo"),
        new ConnectorItem("Fortinet FortiGate", "fortigate", "1.6.1", "Firewall / NGFW", "Fortinet", "cert", new[] { "alerts", "assets", "events" }, "healthy", 97, "2 min", "19.3k", "ativo"),
        new ConnectorItem("CrowdStrike Falcon", "crowdstrike", "1.3.4", "EDR", "CrowdStrike", "oauth2", new[] { "alerts", "assets", "incidents", "vulns" }, "healthy", 99, "1 min", "6.2k", "ativo"),
        new ConnectorItem("AWS Security Hub", "aws-hub", "1.1.0", "Cloud Security", "L'okta IA", "iam", new[] { "alerts", "assets", "vulns" }, "healthy", 96, "5 min", "3.8k", "ativo"),
        new ConnectorItem("Tenable Nessus", "tenable", "0.9.3", "Vulnerability Mgmt", "Comunidade", "apikey", new[] { "vulns", "assets" }, "error", 0, "—", "0", "erro"),
        new ConnectorItem("Cloudflare", "cloudflare", "1.0.7", "WAF / CDN", "L'okta IA", "apikey", new[] { "alerts", "events" }, "healthy", 99, "1 min", "41.2k", "ativo"),
    };

    // ---------- Cofre de credenciais ----------
    public record VaultItem(string N, string Slug, string Kind, string Enc, string Rot, string Status);

    public static readonly IReadOnlyList<VaultItem> Vault = new[]
    {
        new VaultItem("Microsoft Graph · OAuth 2.0", "defender", "client secret + refresh token", "AES-256-GCM", "rotaciona em 42 d", "ok"),
        new VaultItem("Sentinel · Workspace ID + chave", "sentinel", "shared key", "AES-256-GCM", "rotaciona em 18 d", "ok"),
        new VaultItem("Wazuh · API Key", "wazuh", "bearer estático", "AES-256-GCM", "rotaciona em 3 d", "atencao"),
        new VaultItem("FortiGate · Certificado mTLS", "fortigate", "par de chaves X.509", "HSM · não exportável", "expira em 96 d", "ok"),
        new VaultItem("CrowdStrike · OAuth 2.0", "crowdstrike", "client id + secret", "AES-256-GCM", "rotaciona em 61 d", "ok"),
        new VaultItem("AWS · IAM Role assumida", "aws-hub", "STS temporário · 1 h", "sem segredo em repouso", "renovação automática", "ok"),
        new VaultItem("Tenable · API Key", "tenable", "access + secret key", "AES-256-GCM", "revogada pelo fornecedor", "erro"),
    };

    // ---------- Modelo de dados unificado ----------
    public record UnifiedEntity(string N, string Schema, string Fields, string Sources, int MappedCount);

    public static readonly IReadOnlyList<UnifiedEntity> Unified = new[]
    {
        new UnifiedEntity("Alert", "ok.alert.v2", "id · severity · title · source · firstSeen · entities[] · rawRef", "Defender · Sentinel · Wazuh · FortiGate · Falcon", 18),
        new UnifiedEntity("Asset", "ok.asset.v2", "id · type · hostname · ips[] · os · owner · criticality · tags[]", "Defender · Wazuh · Falcon · AWS Hub · Tenable", 14),
        new UnifiedEntity("Vulnerability", "ok.vuln.v2", "id · cve · cvss · epss · assetRef · exposure · state · dueAt", "Wazuh · Falcon · Tenable · AWS Hub", 9),
        new UnifiedEntity("Incident", "ok.incident.v2", "id · severity · status · mitre[] · alertRefs[] · owner · slaAt", "Defender · Sentinel · Falcon", 7),
        new UnifiedEntity("User", "ok.user.v2", "id · upn · displayName · roles[] · mfaMethod · riskScore", "Defender · Okta · Entra ID", 6),
        new UnifiedEntity("Event", "ok.event.v2", "ts · category · outcome · actor · target · geo · rawRef", "Sentinel · Wazuh · FortiGate · Cloudflare", 22),
    };

    // ---------- Motor de eventos e agendador ----------
    public record BusTopic(string N, string D, string Rate, int Subs, string C);

    public static readonly IReadOnlyList<BusTopic> BusTopics = new[]
    {
        new BusTopic("alert.received", "Novo alerta normalizado publicado por qualquer conector", "12.4k/min", 4, "#FF3B5C"),
        new BusTopic("asset.discovered", "Ativo inédito identificado no inventário", "84/min", 3, "#4D9BFF"),
        new BusTopic("vuln.detected", "Vulnerabilidade nova ou reaberta em ativo conhecido", "210/min", 5, "#FF8A3D"),
        new BusTopic("incident.opened", "Incidente criado por regra, correlação ou IA", "7/h", 6, "#FF3B5C"),
        new BusTopic("connector.failed", "Falha de sincronização, autenticação ou cota excedida", "2/h", 3, "#FFC93C"),
        new BusTopic("workflow.completed", "Automação SOAR concluída com resultado e trilha", "48/h", 2, "#00E0A4"),
    };

    public record SyncSchedule(string N, string Scope, string Freq, string Mode, string Ok);

    public static readonly IReadOnlyList<SyncSchedule> Schedules = new[]
    {
        new SyncSchedule("Sincronização incremental", "todos os conectores", "a cada 60 s", "cursor", "98,7%"),
        new SyncSchedule("Reconciliação completa", "ativos e vulnerabilidades", "diária · 03:00", "full scan", "100%"),
        new SyncSchedule("Health check", "todos os conectores", "a cada 30 s", "ping", "99,9%"),
        new SyncSchedule("Rotação de credenciais", "cofre", "conforme política", "on-demand", "100%"),
        new SyncSchedule("Entrega de webhooks", "assinantes externos", "evento + retry", "backoff 5×", "99,4%"),
        new SyncSchedule("Limpeza de cursores órfãos", "fila de sync", "semanal · domingo", "GC", "100%"),
    };

    // ---------- Automações · SOAR ----------
    public record FlowStep(string K, string T, string D, string C, bool BranchAfter = false);

    public static readonly IReadOnlyList<FlowStep> SoarFlow = new[]
    {
        new FlowStep("TRIGGER", "alert.received", "severity ≥ high · source em (Defender, Falcon)", "#FF3B5C"),
        new FlowStep("ENRIQUECER", "Threat Intelligence", "reputação de IP, hash e domínio · CISA KEV · EPSS", "#4D9BFF"),
        new FlowStep("ENRIQUECER", "Contexto do ativo", "criticidade, responsável e janela de manutenção", "#4D9BFF"),
        new FlowStep("IA", "Classificar e pontuar", "confiança, falso positivo provável e ação sugerida", "#00E0A4"),
        new FlowStep("CONDIÇÃO", "confiança ≥ 90% e ativo não crítico?", "ramifica em contenção automática ou fila humana", "#FFC93C", true),
        new FlowStep("NOTIFICAR", "Teams · WhatsApp · e-mail", "canal escolhido pela severidade e pelo turno ativo", "#4D9BFF"),
        new FlowStep("AUDITAR", "Registrar na trilha imutável", "assinatura, entradas, saídas e tempo de execução", "#00E0A4"),
    };

    public record FlowBranch(string Lbl, string Lc, string K, string T, string D, string C);

    public static readonly IReadOnlyList<FlowBranch> SoarBranches = new[]
    {
        new FlowBranch("SE VERDADEIRO", "#00E0A4", "AÇÃO", "Isolar host · Defender", "contenção automática com reversão em 1 clique", "#8A7BFF"),
        new FlowBranch("SE FALSO", "#FF8A3D", "AÇÃO", "Abrir chamado · Jira", "prioridade herdada da severidade e do SLA do plano", "#8A7BFF"),
    };

    public record PlaybookSummary(string N, string Exec, string C);

    public static readonly IReadOnlyList<PlaybookSummary> Playbooks = new[]
    {
        new PlaybookSummary("Contenção de endpoint por alerta crítico", "48 exec", "#00E0A4"),
        new PlaybookSummary("Bloqueio de IP hostil no firewall de borda", "214 exec", "#00E0A4"),
        new PlaybookSummary("Reset de credencial após phishing confirmado", "11 exec", "#00E0A4"),
        new PlaybookSummary("Abertura de chamado para CVE do CISA KEV", "rascunho", "#FFC93C"),
        new PlaybookSummary("Quarentena de e-mail por veredito de IA", "86 exec", "#00E0A4"),
    };

    // ---------- API Gateway ----------
    public record GatewayPolicy(string N, string D, string C);

    public static readonly IReadOnlyList<GatewayPolicy> GatewayPolicies = new[]
    {
        new GatewayPolicy("Autenticação", "OAuth 2.0 · OpenID Connect · JWT · mTLS para parceiros", "#00E0A4"),
        new GatewayPolicy("Rate limiting", "por tenant, por chave e por rota · burst e sustained separados", "#4D9BFF"),
        new GatewayPolicy("Versionamento", "/v1 e /v2 simultâneos · depreciação anunciada com 6 meses", "#8A7BFF"),
        new GatewayPolicy("Auditoria", "toda chamada registrada com ator, escopo, latência e resultado", "#00E0A4"),
        new GatewayPolicy("Escopos", "leitura, escrita e administração separados por módulo", "#FFC93C"),
        new GatewayPolicy("Idempotência", "chave de idempotência obrigatória em POST e PATCH", "#4D9BFF"),
    };

    public record GatewayKey(string Key, string Org, string Scope, string Quota, string Status);

    public static readonly IReadOnlyList<GatewayKey> GatewayKeys = new[]
    {
        new GatewayKey("ok_live_8f2a…c41d", "Grupo Vector", "leitura + escrita", "12.4k / 50k", "ativa"),
        new GatewayKey("ok_live_3b7e…9a02", "Banco Meridiano", "somente leitura", "48.1k / 200k", "ativa"),
        new GatewayKey("ok_live_d9c1…7f31", "NetSul Provedor", "administração", "2.8k / 50k", "ativa"),
        new GatewayKey("ok_test_a01f…22be", "Sandbox de parceiro", "somente leitura", "118 / 1k", "sandbox"),
        new GatewayKey("ok_live_5e88…13cc", "Loja Ativa", "somente leitura", "0 / 10k", "revogada"),
    };

    // ---------- Observabilidade ----------
    public record ObsMetric(string L, string V, int P, string C);

    public static readonly IReadOnlyList<ObsMetric> ObsMetrics = new[]
    {
        new ObsMetric("Conectores saudáveis", "6 / 8", 75, "#FFC93C"),
        new ObsMetric("Taxa de sucesso de sync", "98,7%", 99, "#00E0A4"),
        new ObsMetric("Defasagem média do cursor", "42 s", 14, "#00E0A4"),
        new ObsMetric("Eventos normalizados / min", "12,4k", 62, "#4D9BFF"),
        new ObsMetric("Fila de retry", "18 itens", 9, "#00E0A4"),
        new ObsMetric("Cota de API consumida", "61%", 61, "#4D9BFF"),
    };

    public record ObsIncident(string T, string Slug, string D, string Sev, string SevColor, string Status, string StatusColor);

    public static readonly IReadOnlyList<ObsIncident> ObsIncidents = new[]
    {
        new ObsIncident("14:38", "tenable", "Chave de API revogada pelo fornecedor · sync interrompido", "CRÍTICO", "#FF3B5C", "ABERTO", "#FF3B5C"),
        new ObsIncident("13:52", "wazuh", "Latência de resposta acima de 4 s em 12% das chamadas", "AVISO", "#FFC93C", "MONITORANDO", "#FFC93C"),
        new ObsIncident("11:20", "fortigate", "Cota diária em 84% · projeção de estouro às 22:00", "AVISO", "#FFC93C", "MONITORANDO", "#FFC93C"),
        new ObsIncident("09:04", "sentinel", "Reautenticação OAuth executada automaticamente", "INFO", "#4D9BFF", "RESOLVIDO", "#00E0A4"),
        new ObsIncident("07:41", "aws-hub", "Cursor reposicionado após falha de rede transitória", "INFO", "#4D9BFF", "RESOLVIDO", "#00E0A4"),
    };

    // ---------- SDK ----------
    public record SdkStep(string N, string T, string D);

    public static readonly IReadOnlyList<SdkStep> SdkSteps = new[]
    {
        new SdkStep("01", "Instalar o SDK", "dotnet add package LoktaIa.Connector.Sdk — ou npm i @loktaia/connector-sdk para conectores em TypeScript."),
        new SdkStep("02", "Implementar a interface", "Herde de ConnectorBase e implemente os seis métodos do contrato. O núcleo cuida de retry, cursor, cota e cofre."),
        new SdkStep("03", "Declarar capacidades", "Em getCapabilities() diga quais entidades você entrega, em qual direção e com qual granularidade."),
        new SdkStep("04", "Mapear para o esquema", "Traduza o payload do fornecedor para ok.alert.v2, ok.asset.v2 e demais esquemas versionados."),
        new SdkStep("05", "Testar no sandbox", "loktaia connector test executa o conjunto de conformidade: 84 asserções de contrato, idempotência e retomada."),
        new SdkStep("06", "Publicar", "Assine o pacote e submeta ao marketplace. Revisão de segurança em até 5 dias úteis para selo verificado."),
    };

    public static readonly IReadOnlyList<string> SdkCode = new[]
    {
        "public class AcmeConnector : ConnectorBase",
        "{",
        "    public override Capabilities GetCapabilities() => new()",
        "    {",
        "        Entities = [Alert, Asset, Vulnerability],",
        "        Direction = Direction.Inbound | Direction.Outbound,",
        "        Incremental = true,",
        "        MaxPageSize = 500,",
        "        RateLimit = new(600, TimeSpan.FromMinutes(1))",
        "    };",
        "",
        "    public override async Task<TestResult> TestConnectionAsync(CancellationToken ct)",
        "        => await Http.ProbeAsync(\"/api/v1/health\", ct);",
        "",
        "    public override async IAsyncEnumerable<Envelope> SyncAsync(",
        "        SyncScope scope, Cursor cursor, [EnumeratorCancellation] CancellationToken ct)",
        "    {",
        "        await foreach (var page in Http.PagedAsync(scope.Path, cursor, ct))",
        "            foreach (var raw in page.Items)",
        "                yield return Map.ToAlertV2(raw);   // esquema unificado",
        "    }",
        "}",
    };

    public record SdkRule(string T, string D);

    public static readonly IReadOnlyList<SdkRule> SdkRules = new[]
    {
        new SdkRule("Isolamento de processo", "Cada conector roda em sandbox próprio, sem acesso ao banco nem ao código do núcleo."),
        new SdkRule("Sem estado local", "Cursores e checkpoints ficam no núcleo. O conector pode ser reiniciado ou reescalado a qualquer momento."),
        new SdkRule("Credencial nunca em claro", "O SDK entrega um handle; o segredo é resolvido pelo cofre no momento da chamada."),
        new SdkRule("Versão semântica", "Quebra de contrato exige major. O núcleo mantém as duas últimas majors em execução."),
    };

    // ---------- Notificações ----------
    public record NotifyChannel(string N, string D, string Cat, string SyncInfo, bool On);

    public static readonly IReadOnlyList<NotifyChannel> NotifyChannels = new[]
    {
        new NotifyChannel("Microsoft Teams", "Alertas críticos em canal dedicado do SOC", "CHAT", "agora", true),
        new NotifyChannel("WhatsApp Business", "Alerta e aprovação de contenção por mensagem", "MENSAGEM", "agora", true),
        new NotifyChannel("Slack", "Notificação por severidade em canais separados", "CHAT", "2 min", true),
        new NotifyChannel("Telegram", "Canal reserva para plantão fora do horário", "MENSAGEM", "8 min", true),
        new NotifyChannel("E-mail corporativo", "Resumo diário e alertas de severidade alta", "E-MAIL", "agora", true),
        new NotifyChannel("SMS", "Somente severidade crítica · fallback do WhatsApp", "SMS", "1 h", true),
        new NotifyChannel("Push do aplicativo", "Notificação no app de plantão do analista", "PUSH", "agora", true),
        new NotifyChannel("Webhook genérico", "Entrega assinada em endpoint do cliente", "WEBHOOK", "4 min", true),
        new NotifyChannel("Jira Service", "Abertura automática de chamado por incidente", "TICKET", "7 min", true),
        new NotifyChannel("ServiceNow", "Sincronização de incidentes e mudanças", "TICKET", "—", false),
        new NotifyChannel("Discord", "Canal de comunidade para provedores parceiros", "CHAT", "—", false),
        new NotifyChannel("PagerDuty", "Escalonamento de plantão por rotação", "PLANTÃO", "12 min", true),
    };

    // ---------- Faturamento ----------
    public record BillingKpi(string L, string V, string D, string C);

    public static readonly IReadOnlyList<BillingKpi> BillingKpis = new[]
    {
        new BillingKpi("Receita recorrente", "R$ 284,7k", "+11,4%", "#00E0A4"),
        new BillingKpi("Faturado no mês", "R$ 302,1k", "+8,2%", "#4D9BFF"),
        new BillingKpi("Em aberto", "R$ 27,1k", "2 notas", "#FF8A3D"),
        new BillingKpi("Inadimplência", "0,6%", "-0,3 p.p.", "#00E0A4"),
    };

    public record Invoice(string Num, string Co, string Comp, string Due, string Amt, string Status);

    public static readonly IReadOnlyList<Invoice> Invoices = new[]
    {
        new Invoice("NF-8841", "Banco Meridiano", "07/2026", "10/08/2026", "R$ 21.900", "paga"),
        new Invoice("NF-8840", "Grupo Vector", "07/2026", "10/08/2026", "R$ 8.400", "paga"),
        new Invoice("NF-8839", "NetSul Provedor", "07/2026", "10/08/2026", "R$ 14.200", "paga"),
        new Invoice("NF-8838", "Hospital Santa Clara", "07/2026", "25/07/2026", "R$ 8.400", "vencida"),
        new Invoice("NF-8837", "Prefeitura Digital", "07/2026", "15/08/2026", "R$ 18.700", "aberta"),
        new Invoice("NF-8836", "Pagou Fintech", "07/2026", "10/08/2026", "R$ 8.400", "paga"),
        new Invoice("NF-8835", "Loja Ativa", "07/2026", "20/07/2026", "R$ 1.890", "vencida"),
        new Invoice("NF-8834", "Escritório Lemos", "07/2026", "30/08/2026", "R$ 1.890", "aberta"),
    };

    public static readonly IReadOnlyList<NavGroup> NavGroups = new[]
    {
        new NavGroup("OPERAÇÃO", new[]
        {
            new NavItem("overview", "/Admin/Index", "01", "Visão geral"),
            new NavItem("companies", "/Admin/Empresas", "02", "Empresas", "8", "#4D9BFF"),
            new NavItem("users", "/Admin/Usuarios", "03", "Usuários", "42", "#4D9BFF"),
            new NavItem("roles", "/Admin/Perfis", "04", "Perfis e permissões"),
        }),
        new NavGroup("PLATAFORMA DE INTEGRAÇÃO", new[]
        {
            new NavItem("infoconectores", "/Admin/Informacoes", "25", "Informações"),
            new NavItem("market", "/Admin/Marketplace", "26", "Marketplace", "18", "#00E0A4"),
            new NavItem("connectors", "/Admin/Conectores", "27", "Conectores", "1", "#FF3B5C"),
            new NavItem("vault", "/Admin/Credenciais", "28", "Credenciais"),
            new NavItem("schema", "/Admin/ModeloUnificado", "29", "Modelo unificado"),
            new NavItem("bus", "/Admin/EventosSync", "30", "Eventos e sync"),
            new NavItem("soar", "/Admin/Automacoes", "31", "Automações"),
            new NavItem("gateway", "/Admin/ApiGateway", "32", "API Gateway"),
            new NavItem("observ", "/Admin/Observabilidade", "33", "Observabilidade"),
            new NavItem("sdk", "/Admin/Sdk", "34", "SDK"),
        }),
        new NavGroup("GOVERNANÇA", new[]
        {
            new NavItem("notify", "/Admin/Notificacoes", "25", "Notificações", "12", "#00E0A4"),
            new NavItem("audit", "/Admin/Auditoria", "16", "Auditoria"),
        }),
        new NavGroup("COMERCIAL", new[]
        {
            new NavItem("billing", "/Admin/Faturamento", "22", "Faturamento", "2", "#FF8A3D"),
        }),
    };
}
