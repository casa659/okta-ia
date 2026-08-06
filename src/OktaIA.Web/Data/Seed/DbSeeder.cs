using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Data.Seed;

public static class DbSeeder
{
    public const string AdminRole = "Admin";
    public const string AnalistaRole = "Analista";

    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager, config);
        await SeedCompaniesAsync(db);
        await SeedAssetsAsync(db);
        await SeedSecurityEventsAsync(db);
        await SeedInfraHealthAsync(db);
        await SeedVulnerabilitiesAsync(db);
        await SeedIncidentsAsync(db);
        await SeedAdminAuditLogAsync(db);
        await SeedContactChannelsAsync(db);
        await SeedRolePermissionsAsync(db, roleManager);
        await BackfillDigitalTwinPermissionAsync(db);
    }

    // Dá aos perfis nativos exatamente o acesso que eles já tinham via [Authorize(Roles="...")]
    // hardcoded antes da grade de permissões existir — Admin ganha tudo (embora ele sempre passe
    // direto no AreaPermissionFilter, sem nem consultar esta tabela) e Analista ganha só o SOC,
    // preservando o comportamento real que já existia em produção.
    private static async Task SeedRolePermissionsAsync(ApplicationDbContext db, RoleManager<IdentityRole> roleManager)
    {
        if (await db.RolePermissions.AnyAsync())
        {
            return;
        }

        var admin = await roleManager.FindByNameAsync(AdminRole);
        var analista = await roleManager.FindByNameAsync(AnalistaRole);

        if (admin is not null)
        {
            foreach (var area in AreaCatalog.Todas)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = admin.Id, AreaKey = area.Key });
            }
        }

        if (analista is not null)
        {
            foreach (var area in AreaCatalog.Soc)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = analista.Id, AreaKey = area.Key });
            }
        }

        await db.SaveChangesAsync();
    }

    // SeedRolePermissionsAsync só roda uma vez (early-return se a tabela já tem qualquer linha) —
    // então uma área nova adicionada depois do primeiro deploy nunca seria concedida a ninguém além
    // do Admin (que sempre passa direto, sem consultar a tabela). Backfill idempotente: dá "soc.twin"
    // a todo papel que já tinha "soc.ativos" (o Digital Twin é só outra visão do mesmo inventário).
    private static async Task BackfillDigitalTwinPermissionAsync(ApplicationDbContext db)
    {
        const string area = "soc.twin";
        if (await db.RolePermissions.AnyAsync(rp => rp.AreaKey == area))
        {
            return;
        }

        var roleIdsComAtivos = await db.RolePermissions
            .Where(rp => rp.AreaKey == "soc.ativos")
            .Select(rp => rp.RoleId)
            .Distinct()
            .ToListAsync();

        foreach (var roleId in roleIdsComAtivos)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, AreaKey = area });
        }

        await db.SaveChangesAsync();
    }

    // Mesmos 4 canais fixos que existiam hardcoded em MarketingContent.Channels — viram dado real
    // pra Admin poder editar/adicionar/excluir direto em /Contato.
    private static async Task SeedContactChannelsAsync(ApplicationDbContext db)
    {
        if (await db.ContactChannels.AnyAsync())
        {
            return;
        }

        (string Chave, string Cor, string Valor, string Descricao)[] dados =
        [
            ("COMERCIAL", "#4D9BFF", "info@okta-ia.com", "Propostas, demonstrações e parcerias"),
            ("TELEFONE", "#00E0A4", "+55 11 3042-9392", "Segunda a sexta, 8h às 18h"),
            ("WHATSAPP", "#00E0A4", "+55 34 9 9677-8585", "Atendimento comercial e suporte"),
            ("ENDEREÇO", "#8A7BFF", "Av. Paulista, 2006, cj 1314, Bela Vista, São Paulo-SP, CEP 01.310-926", "Atendimento presencial mediante agendamento"),
        ];

        for (var i = 0; i < dados.Length; i++)
        {
            var d = dados[i];
            db.ContactChannels.Add(new ContactChannel { Chave = d.Chave, Cor = d.Cor, Valor = d.Valor, Descricao = d.Descricao, Ordem = i });
        }

        await db.SaveChangesAsync();
    }

    // Histórico anterior à existência do recurso de auditoria em si — backfill de lançamento,
    // igual ao histórico de 48h do SIEM. Ações reais do console Admin (via AdminAuditService)
    // se acumulam a partir daqui.
    private static async Task SeedAdminAuditLogAsync(ApplicationDbContext db)
    {
        if (await db.AdminAuditLogs.AnyAsync())
        {
            return;
        }

        (string Hora, string Acao, string Detalhe, string Autor, string Ip)[] linhas =
        [
            ("14:42", "LOGIN", "Autenticação bem-sucedida com passkey", "ricardo.silva", "189.44.12.8"),
            ("14:31", "UPDATE", "Perfil de Diego Moraes alterado para Gestor", "ricardo.silva", "189.44.12.8"),
            ("14:18", "CREATE", "Organização Escritório Lemos provisionada", "ricardo.silva", "189.44.12.8"),
            ("13:57", "EXPORT", "Relatório de vulnerabilidades exportado em XLSX", "b.teixeira", "200.19.8.61"),
            ("13:40", "CONTAIN", "Bloqueio de 96 IPs aplicado no firewall de borda", "sistema · IA", "interno"),
            ("13:22", "DENY", "Tentativa de acesso negada · MFA inválido", "p.lemos", "45.132.8.19"),
            ("12:55", "UPDATE", "Chave de API rotacionada para Banco Meridiano", "b.teixeira", "200.19.8.61"),
            ("12:14", "READ", "Consulta a logs de auditoria do último trimestre", "a.nakamura", "189.44.12.9"),
            ("11:48", "CREATE", "Regra de correlação SIEM adicionada", "c.duarte", "189.44.12.9"),
            ("11:20", "UPDATE", "Política de retenção alterada de 3 para 5 anos", "ricardo.silva", "189.44.12.8"),
            ("10:36", "DELETE", "Integração GitLab desconectada", "ricardo.silva", "189.44.12.8"),
            ("09:52", "LOGIN", "Autenticação bem-sucedida com TOTP", "m.rocha", "200.19.8.61"),
            ("09:11", "EXPORT", "Trilha de auditoria exportada para o encarregado", "b.teixeira", "200.19.8.61"),
            ("08:44", "SUSPEND", "Conta de Paula Lemos suspensa por inatividade", "política", "interno"),
        ];

        var hoje = DateTime.UtcNow.Date;
        foreach (var l in linhas)
        {
            var hora = TimeSpan.Parse(l.Hora);
            db.AdminAuditLogs.Add(new AdminAuditLog
            {
                CriadoEm = hoje.Add(hora),
                Acao = l.Acao,
                Detalhe = l.Detalhe,
                Autor = l.Autor,
                OrigemIp = l.Ip,
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { AdminRole, AnalistaRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        var email = config["AdminSeed:Email"];
        var senha = config["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            return;
        }

        var existente = await userManager.FindByEmailAsync(email);
        if (existente is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NomeCompleto = "Rafael Souza",
            Iniciais = "RS",
        };

        var resultado = await userManager.CreateAsync(user, senha);
        if (resultado.Succeeded)
        {
            await userManager.AddToRoleAsync(user, AdminRole);
        }
    }

    // Empresas de exemplo — os mesmos 8 tenants do mockup original, pra manter fidelidade de
    // design (nomes, setores, score de risco e contagens aparecem em vários módulos).
    private static async Task SeedCompaniesAsync(ApplicationDbContext db)
    {
        if (await db.Companies.AnyAsync())
        {
            return;
        }

        var culture = CultureInfo.InvariantCulture;
        (string Nome, string SetorPt, string SetorEn, string Plano, int Score, int Ativos, int Vulns, int Incidentes, string Uptime, int Usuarios, string Status)[] dados =
        [
            ("Grupo Vector", "Indústria", "Industry", "Enterprise", 78, 1284, 12, 3, "99.9", 9, "ativa"),
            ("Hospital Santa Clara", "Saúde", "Healthcare", "Enterprise", 64, 412, 7, 1, "99.9", 7, "ativa"),
            ("Banco Meridiano", "Financeiro", "Finance", "Enterprise+", 31, 2140, 3, 0, "100", 12, "ativa"),
            ("Prefeitura Digital", "Governo", "Government", "Gov", 86, 318, 21, 5, "98.4", 6, "ativa"),
            ("Loja Ativa", "Varejo", "Retail", "Business", 52, 96, 9, 1, "99.7", 2, "inadimplente"),
            ("Pagou Fintech", "Fintech", "Fintech", "Enterprise", 44, 588, 5, 2, "99.9", 4, "ativa"),
            ("NetSul Provedor", "ISP", "ISP", "MSP", 69, 1902, 14, 2, "99.6", 2, "ativa"),
            ("Escritório Lemos", "Jurídico", "Legal", "Business", 27, 48, 2, 0, "99.9", 1, "trial"),
        ];

        foreach (var d in dados)
        {
            db.Companies.Add(new Company
            {
                Nome = d.Nome,
                SetorPt = d.SetorPt,
                SetorEn = d.SetorEn,
                Plano = d.Plano,
                ScoreRisco = d.Score,
                AtivosCount = d.Ativos,
                VulnsCount = d.Vulns,
                IncidentesCount = d.Incidentes,
                UptimePercentual = decimal.Parse(d.Uptime, culture),
                Cnpj = $"12.345.678/0001-{10 + d.Score}",
                StatusContrato = d.Status,
                UsuariosCount = d.Usuarios,
            });
        }

        await db.SaveChangesAsync();
    }

    // Gera um histórico plausível das últimas 48h — alimenta KPIs (Eventos 24h/Bloqueados),
    // mapa de origem, "principais origens" e o fluxo de eventos, tudo com dado real de banco em
    // vez do gerador aleatório em memória do mockup.
    private static async Task SeedSecurityEventsAsync(ApplicationDbContext db)
    {
        if (await db.SecurityEvents.AnyAsync())
        {
            return;
        }

        var assetEmpresa = await BuildAssetCompanyMapAsync(db);
        var rnd = new Random(20260802);
        var agora = DateTime.UtcNow;
        var eventos = new List<SecurityEvent>();

        for (var i = 0; i < 420; i++)
        {
            var origem = ThreatCatalog.Origens[rnd.Next(ThreatCatalog.Origens.Length)];
            var tipo = ThreatCatalog.Tipos[rnd.Next(ThreatCatalog.Tipos.Length)];
            var alvo = ThreatCatalog.Alvos[rnd.Next(ThreatCatalog.Alvos.Length)];
            var minutosAtras = rnd.Next(0, 48 * 60);

            eventos.Add(new SecurityEvent
            {
                CompanyId = assetEmpresa.GetValueOrDefault(alvo),
                TipoPt = tipo.Pt,
                TipoEn = tipo.En,
                Severidade = tipo.Sev,
                OrigemPaisCodigo = origem.Cc,
                OrigemPaisNomePt = origem.Pt,
                OrigemPaisNomeEn = origem.En,
                OrigemLat = origem.Lat,
                OrigemLng = origem.Lng,
                OrigemIp = $"{rnd.Next(1, 255)}.{rnd.Next(0, 255)}.{rnd.Next(0, 255)}.{rnd.Next(1, 255)}",
                Alvo = alvo,
                Bloqueado = rnd.NextDouble() > 0.11,
                CriadoEm = agora.AddMinutes(-minutosAtras),
            });
        }

        db.SecurityEvents.AddRange(eventos);
        await db.SaveChangesAsync();
    }

    // Deriva o dono (tenant) de eventos/CVEs/incidentes casando o nome do ativo citado no texto
    // (Alvo/AssetNome/Asset) com Assets.Nome, que já tem CompanyId real — evita reinventar essa
    // associação em 3 lugares diferentes.
    private static async Task<Dictionary<string, int>> BuildAssetCompanyMapAsync(ApplicationDbContext db)
        => await db.Assets.ToDictionaryAsync(a => a.Nome, a => a.CompanyId);

    private static async Task SeedInfraHealthAsync(ApplicationDbContext db)
    {
        if (await db.InfraHealthSnapshots.AnyAsync())
        {
            return;
        }

        db.InfraHealthSnapshots.Add(new InfraHealthSnapshot
        {
            CpuPct = 41,
            RamPct = 63,
            DiscoPct = 72,
            RedePct = 34,
            LatenciaMs = 22,
        });

        await db.SaveChangesAsync();
    }

    // Mesmos 9 ativos de exemplo do mockup original, ligados às empresas correspondentes (o
    // mockup não amarra ativo↔empresa explicitamente; a associação abaixo segue o nome/domínio
    // de cada ativo, que já indica o dono).
    private static async Task SeedAssetsAsync(ApplicationDbContext db)
    {
        if (await db.Assets.AnyAsync())
        {
            return;
        }

        var empresas = await db.Companies.ToDictionaryAsync(c => c.Nome);
        (string Empresa, string Nome, string Ip, string Tipo, string Stack, decimal Uptime, int? TlsDias, AssetTlsStatus TlsStatus, int[] Vulns, int Saude)[] dados =
        [
            ("Grupo Vector", "api.grupovector.com", "189.44.12.8", "API", ".NET 9 / Kestrel", 99.98m, 48, AssetTlsStatus.Ok, [2, 1, 4, 7], 92),
            ("Hospital Santa Clara", "portal.hsanta.br", "200.147.9.44", "WEB", "Angular 20 / Nginx", 99.91m, 12, AssetTlsStatus.Alerta, [1, 3, 6, 11], 74),
            ("Grupo Vector", "vpn-sp01", "10.20.0.4", "VPN", "FortiOS 7.4", 99.99m, 201, AssetTlsStatus.Ok, [1, 0, 2, 3], 61),
            ("Loja Ativa", "wp.lojaativa.com.br", "177.92.4.19", "WEB", "WordPress 6.5", 98.72m, 5, AssetTlsStatus.Critico, [3, 8, 14, 22], 38),
            ("Grupo Vector", "srv-db-prod-02", "10.10.2.11", "DB", "SQL Server 2022", 100m, null, AssetTlsStatus.NaoAplicavel, [1, 2, 5, 9], 67),
            ("Pagou Fintech", "checkout.pagou.io", "198.51.100.7", "API", "Node 22 / Fastify", 99.99m, 87, AssetTlsStatus.Ok, [0, 1, 3, 5], 95),
            ("Prefeitura Digital", "mail.prefdigital.gov.br", "200.19.8.61", "MAIL", "Exchange 2019", 99.84m, 34, AssetTlsStatus.Ok, [0, 4, 7, 12], 71),
            ("Grupo Vector", "ci.grupovector.com", "10.10.4.30", "CI", "Jenkins 2.440", 99.95m, 119, AssetTlsStatus.Ok, [1, 1, 3, 6], 80),
            ("Grupo Vector", "fw-borda-01", "189.44.12.1", "FW", "FortiGate 600F", 100m, null, AssetTlsStatus.NaoAplicavel, [1, 0, 1, 2], 88),
        ];

        foreach (var d in dados)
        {
            if (!empresas.TryGetValue(d.Empresa, out var empresa))
            {
                continue;
            }

            db.Assets.Add(new Asset
            {
                CompanyId = empresa.Id,
                Nome = d.Nome,
                Ip = d.Ip,
                Tipo = d.Tipo,
                Stack = d.Stack,
                UptimePercentual = d.Uptime,
                TlsDias = d.TlsDias,
                TlsStatus = d.TlsStatus,
                // Array do mockup vem em ordem decrescente de severidade [Crítica,Alta,Média,Baixa].
                VulnsCriticas = d.Vulns[0],
                VulnsAltas = d.Vulns[1],
                VulnsMedias = d.Vulns[2],
                VulnsBaixas = d.Vulns[3],
                Saude = d.Saude,
            });
        }

        await db.SaveChangesAsync();
    }

    // Mesmas 8 CVEs de exemplo do mockup original (CVE/CVSS/CWE/exposição/prioridade IA/status).
    private static async Task SeedVulnerabilitiesAsync(ApplicationDbContext db)
    {
        if (await db.Vulnerabilities.AnyAsync())
        {
            return;
        }

        var assetEmpresa = await BuildAssetCompanyMapAsync(db);

        (string Cve, decimal Cvss, string Componente, string TituloPt, string TituloEn, string Cwe, string Asset,
            string ExpPt, string ExpEn, int Prio, string StPt, string StEn, Severidade Sev)[] dados =
        [
            ("CVE-2024-3094", 10.0m, "xz-utils", "Backdoor em biblioteca de compressão (SSH)", "Backdoor in compression library (SSH)", "CWE-506", "srv-db-prod-02", "KEV", "KEV", 97, "Aberto", "Open", Severidade.Critica),
            ("CVE-2024-6387", 8.1m, "OpenSSH", "RCE não autenticado — regreSSHion", "Unauthenticated RCE — regreSSHion", "CWE-364", "vpn-sp01", "KEV", "KEV", 93, "Em correção", "Patching", Severidade.Critica),
            ("CVE-2021-44228", 10.0m, "Log4j", "Execução remota de código via JNDI", "Remote code execution via JNDI", "CWE-502", "api.grupovector.com", "KEV", "KEV", 89, "Aberto", "Open", Severidade.Critica),
            ("CVE-2024-21762", 9.8m, "FortiOS", "Escrita fora de limites no SSL-VPN", "Out-of-bounds write in SSL-VPN", "CWE-787", "fw-borda-01", "Interno", "Internal", 76, "Aberto", "Open", Severidade.Critica),
            ("CVE-2023-44487", 7.5m, "HTTP/2", "Rapid Reset — negação de serviço", "Rapid Reset — denial of service", "CWE-400", "checkout.pagou.io", "Público", "Public", 71, "Mitigado", "Mitigated", Severidade.Alta),
            ("CVE-2022-22965", 9.8m, "Spring", "Spring4Shell — binding de classe", "Spring4Shell — class binding", "CWE-94", "portal.hsanta.br", "Público", "Public", 68, "Em correção", "Patching", Severidade.Critica),
            ("CVE-2023-4863", 8.8m, "libwebp", "Estouro de heap no decodificador WebP", "Heap overflow in WebP decoder", "CWE-787", "wp.lojaativa.com.br", "Público", "Public", 54, "Aberto", "Open", Severidade.Alta),
            ("CVE-2024-23897", 9.8m, "Jenkins", "Leitura arbitrária de arquivos via CLI", "Arbitrary file read via CLI", "CWE-22", "ci.grupovector.com", "Interno", "Internal", 48, "Aceito", "Accepted", Severidade.Critica),
            ("CVE-2023-38545", 8.8m, "curl", "Estouro de buffer no handshake SOCKS5", "Buffer overflow in SOCKS5 handshake", "CWE-787", "srv-app-04", "Interno", "Internal", 33, "Corrigido", "Fixed", Severidade.Alta),
        ];

        foreach (var d in dados)
        {
            db.Vulnerabilities.Add(new Vulnerability
            {
                CompanyId = assetEmpresa.GetValueOrDefault(d.Asset),
                Cve = d.Cve,
                Cvss = d.Cvss,
                Componente = d.Componente,
                TituloPt = d.TituloPt,
                TituloEn = d.TituloEn,
                Cwe = d.Cwe,
                AssetNome = d.Asset,
                ExposicaoPt = d.ExpPt,
                ExposicaoEn = d.ExpEn,
                PrioridadeIa = d.Prio,
                StatusPt = d.StPt,
                StatusEn = d.StEn,
                Severidade = d.Sev,
            });
        }

        await db.SaveChangesAsync();
    }

    // Mesmos 5 incidentes de exemplo do mockup original, com passos recomendados e linha do
    // tempo completos — AbertoEm é derivado da "idade" (12m/1h/3h/...) pra ficar um timestamp
    // real em vez da string fixa do design.
    private static async Task SeedIncidentsAsync(ApplicationDbContext db)
    {
        if (await db.Incidents.AnyAsync())
        {
            return;
        }

        var assetEmpresa = await BuildAssetCompanyMapAsync(db);
        var agora = DateTime.UtcNow;
        var incidentes = new (string Codigo, Severidade Sev, TimeSpan Idade, string Asset, string Mitre, string Analista, int Eventos,
            string TPt, string TEn, string AiPt, string AiEn, string Conf, string SPt, string SEn,
            (string Pt, string En, string K)[] Passos,
            (string Hora, string Cor, string Pt, string En, string Origem)[] Timeline)[]
        {
            ("INC-4821", Severidade.Critica, TimeSpan.FromMinutes(12), "vpn-sp01", "T1110.004", "R. Silva", 3418,
                "Credential stuffing distribuído contra concentrador VPN", "Distributed credential stuffing against VPN concentrator",
                "3 ASNs, 1.842 usuários testados", "3 ASNs, 1,842 users tested", "94%",
                "Detectei 3.418 tentativas de autenticação falhas em 41 minutos contra vpn-sp01, partindo de 214 IPs distribuídos em 3 sistemas autônomos russos. As credenciais testadas coincidem em 61% com um vazamento público de 2024 associado ao domínio corporativo. Nenhuma sessão foi estabelecida, mas duas contas de serviço sem MFA foram alvo repetido — elas representam o caminho mais provável de sucesso caso o ataque continue.",
                "I detected 3,418 failed authentication attempts in 41 minutes against vpn-sp01, from 214 IPs across 3 Russian autonomous systems. Tested credentials match a 2024 public breach tied to the corporate domain at 61%. No session was established, but two service accounts without MFA were repeatedly targeted — they are the most likely path to success if the attack continues.",
                [
                    ("Bloquear os 3 ASNs de origem no firewall de borda por 24h", "Block the 3 source ASNs at the edge firewall for 24h", "FIREWALL"),
                    ("Forçar MFA nas contas svc-backup e svc-integra", "Enforce MFA on svc-backup and svc-integra accounts", "IAM"),
                    ("Rotacionar credenciais das 1.842 contas testadas", "Rotate credentials for the 1,842 tested accounts", "IAM"),
                    ("Ativar rate limit adaptativo no concentrador VPN", "Enable adaptive rate limiting on the VPN concentrator", "NETWORK"),
                ],
                [
                    ("14:02", "#FF3B5C", "Primeiro pico anômalo de falhas de autenticação", "First anomalous spike in auth failures", "siem.auth"),
                    ("14:09", "#FF8A3D", "IA correlacionou 214 IPs a 3 ASNs conhecidos", "AI correlated 214 IPs to 3 known ASNs", "ai.correlation"),
                    ("14:17", "#FFC93C", "Incidente aberto automaticamente · severidade crítica", "Incident auto-opened · critical severity", "soc.rules"),
                    ("14:23", "#00E0A4", "Bloqueio temporário de 96 IPs aplicado", "Temporary block applied to 96 IPs", "firewall.edge"),
                    ("14:31", "#4D9BFF", "Analista R. Silva assumiu o incidente", "Analyst R. Silva took ownership", "soc.console"),
                ]),
            ("INC-4818", Severidade.Critica, TimeSpan.FromHours(1), "wp.lojaativa.com.br", "T1190", "C. Duarte", 842,
                "Exploração ativa de plugin WordPress desatualizado", "Active exploitation of outdated WordPress plugin",
                "Webshell provável em /wp-content/uploads", "Likely webshell in /wp-content/uploads", "88%",
                "Requisições POST com payload ofuscado atingiram um plugin de formulários na versão 4.1.2, vulnerável a upload arbitrário. Um arquivo PHP foi criado em /wp-content/uploads/2026/07 e acessado 6 vezes a partir do mesmo IP holandês. O padrão de acesso indica webshell funcional. Recomendo isolamento imediato antes de qualquer análise forense.",
                "POST requests with obfuscated payload hit a forms plugin on version 4.1.2, vulnerable to arbitrary upload. A PHP file was created in /wp-content/uploads/2026/07 and accessed 6 times from the same Dutch IP. The access pattern indicates a working webshell. I recommend immediate isolation before any forensic analysis.",
                [
                    ("Isolar o host da rede mantendo memória para forense", "Isolate the host from the network, preserving memory for forensics", "CONTAIN"),
                    ("Quarentenar o arquivo suspeito e coletar hash", "Quarantine the suspicious file and collect its hash", "FORENSIC"),
                    ("Atualizar plugin para 4.3.1 e revisar todos os uploads", "Update plugin to 4.3.1 and review all uploads", "PATCH"),
                ],
                [
                    ("13:11", "#FF3B5C", "POST anômalo detectado pelo WAF", "Anomalous POST detected by WAF", "waf.rules"),
                    ("13:14", "#FF8A3D", "Criação de arquivo PHP em diretório de uploads", "PHP file created in uploads directory", "fim.agent"),
                    ("13:22", "#FFC93C", "IA classificou como webshell com 88% de confiança", "AI classified as webshell with 88% confidence", "ai.malware"),
                    ("13:40", "#4D9BFF", "Notificação enviada ao cliente Loja Ativa", "Notification sent to client Loja Ativa", "notify.email"),
                ]),
            ("INC-4815", Severidade.Alta, TimeSpan.FromHours(3), "srv-db-prod-02", "T1046", "A. Nakamura", 1204,
                "Varredura interna lateral a partir de estação comprometida", "Internal lateral scan from a compromised workstation",
                "Movimento lateral em 3 sub-redes", "Lateral movement across 3 subnets", "76%",
                "Uma estação da rede administrativa iniciou varredura de portas em 3 sub-redes, incluindo o segmento de banco de dados. O comportamento diverge da linha de base do usuário em 4 dimensões: horário, volume, destinos e protocolo. Probabilidade elevada de estação comprometida servindo de pivô.",
                "A workstation in the admin network started port scanning across 3 subnets, including the database segment. Behavior diverges from the user baseline on 4 dimensions: time, volume, destinations and protocol. High probability of a compromised workstation acting as pivot.",
                [
                    ("Isolar a estação WKS-ADM-14 via EDR", "Isolate workstation WKS-ADM-14 via EDR", "EDR"),
                    ("Revisar regras de microsegmentação do segmento de dados", "Review microsegmentation rules for the data segment", "NETWORK"),
                    ("Coletar dump de memória para análise de credenciais", "Collect memory dump for credential analysis", "FORENSIC"),
                ],
                [
                    ("11:28", "#FF8A3D", "Varredura de portas iniciada em 10.20.0.0/16", "Port scan started on 10.20.0.0/16", "ids.suricata"),
                    ("11:35", "#FFC93C", "Desvio comportamental de 4σ sinalizado pela IA", "4σ behavioral deviation flagged by AI", "ai.ueba"),
                    ("11:52", "#4D9BFF", "Incidente escalado para nível 2", "Incident escalated to tier 2", "soc.console"),
                ]),
            ("INC-4809", Severidade.Media, TimeSpan.FromHours(6), "api.grupovector.com", "T1499", null!, 96,
                "Abuso de rate limit em endpoint público de consulta", "Rate-limit abuse on public query endpoint",
                "Scraping automatizado, sem exfiltração", "Automated scraping, no exfiltration", "82%",
                "Um único token de API consumiu 96 mil requisições em 2 horas contra /v2/catalog, 40 vezes acima do padrão. Não há indício de exfiltração de dados sensíveis — o endpoint retorna apenas catálogo público — mas o custo de infraestrutura e a latência para clientes legítimos aumentaram.",
                "A single API token consumed 96k requests in 2 hours against /v2/catalog, 40× above baseline. There is no sign of sensitive data exfiltration — the endpoint only returns a public catalog — but infrastructure cost and latency for legitimate clients increased.",
                [
                    ("Aplicar quota escalonada ao token afetado", "Apply tiered quota to the affected token", "API"),
                    ("Habilitar cache de borda para /v2/catalog", "Enable edge caching for /v2/catalog", "CDN"),
                ],
                [
                    ("08:44", "#FFC93C", "Limite de requisições excedido 40×", "Request limit exceeded 40×", "gateway.api"),
                    ("09:10", "#4D9BFF", "IA descartou exfiltração de dados", "AI ruled out data exfiltration", "ai.classifier"),
                ]),
            ("INC-4802", Severidade.Alta, TimeSpan.FromHours(9), "mail.prefdigital.gov.br", "T1566.002", "M. Rocha", 318,
                "Campanha de phishing direcionada a servidores públicos", "Phishing campaign targeting public servants",
                "318 mensagens, 11 cliques confirmados", "318 messages, 11 confirmed clicks", "91%",
                "Campanha com domínio typosquatting registrado há 4 dias entregou 318 mensagens simulando o portal de contracheque. Onze usuários clicaram e três submeteram credenciais na página falsa. As três contas já apresentam tentativas de login externo a partir de IPs não habituais.",
                "A campaign using a typosquatting domain registered 4 days ago delivered 318 messages impersonating the payroll portal. Eleven users clicked and three submitted credentials on the fake page. All three accounts already show external login attempts from unusual IPs.",
                [
                    ("Revogar sessões e resetar senha das 3 contas afetadas", "Revoke sessions and reset passwords for the 3 affected accounts", "IAM"),
                    ("Bloquear domínio typosquatting no gateway de e-mail", "Block the typosquatting domain at the mail gateway", "EMAIL"),
                    ("Remover mensagens das caixas de entrada restantes", "Purge messages from remaining inboxes", "EMAIL"),
                    ("Disparar treinamento direcionado aos 11 usuários", "Trigger targeted training for the 11 users", "AWARENESS"),
                ],
                [
                    ("05:30", "#FF8A3D", "318 mensagens entregues de domínio recém-registrado", "318 messages delivered from newly registered domain", "mail.gateway"),
                    ("06:12", "#FF3B5C", "Primeiro clique confirmado em link malicioso", "First confirmed click on malicious link", "proxy.web"),
                    ("07:05", "#FFC93C", "IA identificou 3 submissões de credenciais", "AI identified 3 credential submissions", "ai.phishing"),
                    ("07:40", "#00E0A4", "Domínio bloqueado em todos os tenants", "Domain blocked across all tenants", "threat.intel"),
                ]),
        };

        foreach (var d in incidentes)
        {
            var incident = new Incident
            {
                CompanyId = assetEmpresa.GetValueOrDefault(d.Asset),
                Codigo = d.Codigo,
                Severidade = d.Sev,
                Asset = d.Asset,
                MitreCode = d.Mitre,
                Analista = d.Analista,
                EventosCount = d.Eventos,
                TituloPt = d.TPt,
                TituloEn = d.TEn,
                AiResumoPt = d.AiPt,
                AiResumoEn = d.AiEn,
                AiConfianca = d.Conf,
                NarrativaPt = d.SPt,
                NarrativaEn = d.SEn,
                AbertoEm = agora - d.Idade,
            };

            for (var i = 0; i < d.Passos.Length; i++)
            {
                incident.Passos.Add(new IncidentStep
                {
                    Ordem = i + 1,
                    DescricaoPt = d.Passos[i].Pt,
                    DescricaoEn = d.Passos[i].En,
                    Categoria = d.Passos[i].K,
                });
            }

            foreach (var t in d.Timeline)
            {
                incident.LinhaDoTempo.Add(new IncidentTimelineEvent
                {
                    Hora = t.Hora,
                    Cor = t.Cor,
                    DescricaoPt = t.Pt,
                    DescricaoEn = t.En,
                    Origem = t.Origem,
                });
            }

            db.Incidents.Add(incident);
        }

        await db.SaveChangesAsync();
    }
}
