using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Models;
using OktaIA.Web.Services;
using OktaIA.Web.Services.Diagnostico;

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
        await BackfillAlertasPermissionAsync(db);
        await BackfillInformacoesPermissionAsync(db);
        await BackfillRebrandLoktaiaAsync(db);
        await BackfillEmpresasDemoAsync(db);
        await SeedDiagnosticoDemoAsync(db);
    }

    /// <summary>
    /// Um diagnÃ³stico de exemplo, completo, numa empresa de demonstraÃ§Ã£o.
    ///
    /// Existe para que dÃª para ver o mÃ³dulo funcionando sem passar uma hora preenchendo â€” a tela de
    /// resultado, o mapa do ambiente, os riscos e a proposta sÃ³ ficam interessantes com dado denso.
    ///
    /// âš ï¸ O tÃ­tulo comeÃ§a com "EXEMPLO" e o respondente diz que Ã© demonstraÃ§Ã£o, nos dois lugares em
    /// que alguÃ©m olharia. Levantamento fictÃ­cio confundido com o de um cliente real Ã© o pior
    /// desfecho possÃ­vel num produto de conformidade â€” daÃ­ a marcaÃ§Ã£o redundante de propÃ³sito.
    ///
    /// O perfil Ã© o mais comum numa PME industrial brasileira, e Ã© escolhido para mostrar
    /// exatamente a tese do produto: cobertura razoÃ¡vel, maturidade baixa. A empresa comprou
    /// ferramenta ao longo dos anos e nÃ£o opera quase nenhuma.
    /// </summary>
    private static async Task SeedDiagnosticoDemoAsync(ApplicationDbContext db)
    {
        // ⚠️ A checagem é pelo AUTOR, não por "existe algum diagnóstico".
        //
        // A primeira versão voltava se houvesse QUALQUER diagnóstico no banco — e bastou o operador
        // criar um levantamento de teste antes do primeiro boot para o exemplo nunca aparecer, sem
        // erro nenhum, o que é o pior tipo de falha: silenciosa e confundida com bug de tela.
        // `CriadoPor == "seed"` é estável mesmo que alguém renomeie o exemplo.
        if (await db.Diagnosticos.AnyAsync(d => d.CriadoPor == "seed"))
        {
            return;
        }

        var empresa = await db.Companies.FirstOrDefaultAsync(c => c.Demo && c.Nome == "Grupo Vector")
                      ?? await db.Companies.FirstOrDefaultAsync(c => c.Demo);
        if (empresa is null)
        {
            return;
        }

        var diagnostico = MontarDiagnosticoDeExemplo();
        diagnostico.CompanyId = empresa.Id;

        db.Diagnosticos.Add(diagnostico);
        await db.SaveChangesAsync();

        // Congela os nÃºmeros e gera o mapa de riscos, como faria a conclusÃ£o pela tela.
        var resultado = CalculadoraDoDiagnostico.Calcular(diagnostico);
        diagnostico.Cobertura = resultado.Cobertura;
        diagnostico.Maturidade = resultado.Maturidade;
        diagnostico.UsoDoInvestimento = resultado.UsoDoInvestimento;
        diagnostico.Integracao = resultado.Integracao;
        db.DiagnosticoRiscos.AddRange(CalculadoraDoDiagnostico.GerarRiscos(diagnostico));

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// O conteÃºdo do exemplo, sem a mecÃ¢nica de gravaÃ§Ã£o. Separado para poder ser conferido fora do
    /// banco â€” nÃºmeros de demonstraÃ§Ã£o que contam a histÃ³ria errada sÃ£o pior que nÃ£o ter exemplo.
    /// </summary>
    public static Diagnostico MontarDiagnosticoDeExemplo()
    {
        var diagnostico = new Diagnostico
        {
            Titulo = "EXEMPLO â€” DiagnÃ³stico de demonstraÃ§Ã£o",
            CriadoPor = "seed",
            Respondente = "Dados de demonstraÃ§Ã£o",
            RespondenteCargo = "nÃ£o Ã© um cliente real",
            RealizadoEm = DateOnly.FromDateTime(DateTime.Today.AddDays(-9)),
            Status = StatusDiagnostico.Concluido,
            ConcluidoEm = DateTimeOffset.UtcNow.AddDays(-9),
            Observacoes = "Levantamento fictÃ­cio, criado pelo seed para demonstrar o mÃ³dulo. "
                        + "Nenhuma informaÃ§Ã£o aqui descreve uma empresa real.",
        };

        void Responder(string codigo, string valor, OrigemDaInformacao origem = OrigemDaInformacao.Declarado)
        {
            var pergunta = CatalogoDeDominios.BuscarPergunta(codigo);
            if (pergunta is null) { return; }

            var situacao = valor switch
            {
                CatalogoDeDominios.Parcial => SituacaoDoControle.Parcial,
                CatalogoDeDominios.NaoSei => SituacaoDoControle.NaoAvaliado,
                _ => (valor == CatalogoDeDominios.Sim) != pergunta.RespostaBoaEhNao
                    ? SituacaoDoControle.Tem
                    : SituacaoDoControle.NaoTem,
            };

            diagnostico.Respostas.Add(new DiagnosticoResposta
            {
                PerguntaCodigo = codigo,
                Opcao = pergunta.Tipo == TipoDePergunta.Controle || pergunta.Tipo == TipoDePergunta.Escolha ? valor : null,
                Texto = pergunta.Tipo is TipoDePergunta.Texto or TipoDePergunta.Multipla ? valor : null,
                Numero = pergunta.Tipo == TipoDePergunta.Numero && int.TryParse(valor, out var n) ? n : null,
                Situacao = situacao,
                Origem = origem,
                RespondidoEm = DateTimeOffset.UtcNow.AddDays(-9),
            });
        }

        const string Sim = CatalogoDeDominios.Sim;
        const string Nao = CatalogoDeDominios.Nao;
        const string Parcial = CatalogoDeDominios.Parcial;
        const string NaoSei = CatalogoDeDominios.NaoSei;

        // Perfil â€” indÃºstria de porte mÃ©dio, com chÃ£o de fÃ¡brica conectado.
        Responder("perfil.segmento", "IndÃºstria");
        Responder("perfil.funcionarios", "180");
        Responder("perfil.usuariosti", "95");
        Responder("perfil.unidades", "2");
        Responder("perfil.endpoints", "110");
        Responder("perfil.servidores", "9");
        Responder("perfil.operacao", "24x7");
        Responder("perfil.remoto", Sim);
        Responder("perfil.desenvolve", Nao);
        Responder("perfil.ot", Sim);
        Responder("perfil.sistemascriticos", "ERP Protheus, MES prÃ³prio, portal de pedidos");
        Responder("perfil.dados", "Dados pessoais; Dados financeiros; Propriedade intelectual");

        // Rede â€” o perÃ­metro existe e estÃ¡ razoÃ¡vel. O que falha sÃ£o as perguntas de gestÃ£o:
        // licenÃ§a vencida e firmware velho nÃ£o aparecem no organograma, aparecem no incidente.
        Responder("rede.firewall", Sim, OrigemDaInformacao.Validado);
        Responder("rede.firewall.fabricante", "Fortinet");
        Responder("rede.firewall.licenca", Nao);
        Responder("rede.firewall.firmware", Nao);
        Responder("rede.firewall.ha", Sim);
        Responder("rede.firewall.regras", Parcial);
        Responder("rede.firewall.backupconfig", Parcial);
        Responder("rede.firewall.quemadmin", "Fornecedor / revenda");
        Responder("rede.logs", Sim);
        Responder("rede.ips", Sim);
        Responder("rede.vpn", Sim);
        Responder("rede.segmentacao", Parcial);
        Responder("rede.wifi", Sim);
        Responder("rede.dns", Sim);

        // Endpoint â€” Defender comprado e instalado, mas ninguÃ©m abre o console.
        Responder("endpoint.protecao", Sim, OrigemDaInformacao.Evidenciado);
        Responder("endpoint.fabricante", "Microsoft Defender");
        Responder("endpoint.edr", Parcial);
        Responder("endpoint.cobertura", Parcial);
        Responder("endpoint.alertas", Nao);
        Responder("endpoint.ransomware", Sim);
        Responder("endpoint.patch", Sim);
        Responder("endpoint.criptografia", Sim);
        Responder("endpoint.usb", Parcial);
        Responder("endpoint.admin", Parcial);
        Responder("endpoint.mdm", Sim);

        // Identidade â€” a camada mais bem resolvida da casa.
        Responder("identidade.diretorio", Sim, OrigemDaInformacao.Validado);
        Responder("identidade.mfa", Sim, OrigemDaInformacao.Evidenciado);
        Responder("identidade.mfa.cobertura", Parcial);
        Responder("identidade.privilegiadas", Sim);
        Responder("identidade.pam", Nao);
        Responder("identidade.desligados", Sim);
        Responder("identidade.revisao", Parcial);
        Responder("identidade.senhas", Sim);
        Responder("identidade.servico", NaoSei);
        Responder("identidade.terceiros", Parcial);
        Responder("identidade.sso", Sim);

        // E-mail â€” falta o DMARC, que Ã© o que permite fraude em nome da empresa.
        Responder("email.plataforma", "Microsoft 365");
        Responder("email.antiphishing", Sim);
        Responder("email.spf", Sim, OrigemDaInformacao.Validado);
        Responder("email.dkim", Sim, OrigemDaInformacao.Validado);
        Responder("email.dmarc", Nao, OrigemDaInformacao.Validado);
        Responder("email.impersonation", Parcial);
        Responder("email.sandbox", Sim);
        Responder("email.treinamento", Parcial);
        Responder("email.simulacao", Nao);

        // Backup â€” existe e dÃ¡ conforto falso. Ã‰ exatamente aqui que um ransomware venceria:
        // cÃ³pia alcanÃ§Ã¡vel pela mesma credencial da rede, e restauraÃ§Ã£o nunca testada.
        Responder("backup.existe", Sim);
        Responder("backup.solucao", "Veeam Backup & Replication");
        Responder("backup.offline", Nao);
        Responder("backup.imutavel", Nao);
        Responder("backup.externo", Parcial);
        Responder("backup.retencao", "30");
        Responder("backup.rpo", Nao);
        Responder("backup.rto", Nao);
        Responder("backup.teste", Nao);
        Responder("backup.dr", Nao);

        // Infraestrutura â€” organizada, com um parque legado que ninguÃ©m trocou.
        Responder("infra.inventario", Sim);
        Responder("infra.eol", Sim);
        Responder("infra.hardening", Parcial);
        Responder("infra.virtualizacao", "VMware");
        Responder("infra.bancos", "SQL Server; PostgreSQL");
        Responder("infra.monitoramento", Sim);
        Responder("infra.acessofisico", Sim);
        Responder("infra.energia", Sim);

        // Nuvem â€” deliberadamente EM BRANCO alÃ©m do "usa": quem cuida do Azure nÃ£o estava na
        // reuniÃ£o. Ã‰ o caso mais comum de um levantamento real, e Ã© o que faz a camada aparecer
        // como NÃƒO AVALIADA no mapa em vez de descoberta.
        Responder("cloud.usa", Sim);
        Responder("cloud.quais", "Azure");

        // Vulnerabilidades â€” comeÃ§ou, mas sem prazo de correÃ§Ã£o o achado envelhece.
        Responder("vuln.gestao", Sim);
        Responder("vuln.ferramenta", "Nessus Essentials");
        Responder("vuln.frequencia", "Trimestral");
        Responder("vuln.sla", Nao);
        Responder("vuln.externa", Parcial);
        Responder("vuln.pentest", Nao);

        // Monitoramento â€” tem SIEM, quase nada envia log, e ninguÃ©m olha fora do horÃ¡rio.
        Responder("mon.siem", Sim);
        Responder("mon.siem.qual", "Wazuh");
        Responder("mon.fontes", Nao);
        Responder("mon.retencao", Parcial);
        Responder("mon.soc", Nao);
        Responder("mon.regras", Parcial);
        Responder("mon.ti", Nao);

        // Resposta a incidentes â€” o plano nunca saiu do papel.
        Responder("resp.plano", Nao);
        Responder("resp.responsavel", Sim);
        Responder("resp.ransomware", Nao);
        Responder("resp.vazamento", Nao);
        Responder("resp.comunicacao", Nao);
        Responder("resp.juridico", Parcial);
        Responder("resp.simulacao", Nao);

        // GovernanÃ§a â€” encarregado nomeado, inventÃ¡rio de dados no comeÃ§o.
        Responder("gov.encarregado", Sim);
        Responder("gov.politicas", Sim);
        Responder("gov.inventariodados", Parcial);
        Responder("gov.titulares", Sim);
        Responder("gov.retencao", Parcial);
        Responder("gov.classificacao", Nao);
        Responder("gov.treinamento", Parcial);
        Responder("gov.auditoria", Nao);
        Responder("gov.seguro", Nao);

        // Terceiros â€” o fornecedor do ERP tem VPN permanente hÃ¡ anos.
        Responder("ter.quantos", "6");
        Responder("ter.avaliacao", Parcial);
        Responder("ter.contrato", Sim);
        Responder("ter.acessoremoto", Nao);
        Responder("ter.revisao", Nao);
        Responder("ter.apis", Sim);

        // OT â€” chÃ£o de fÃ¡brica na mesma rede do escritÃ³rio. A lacuna mais cara de corrigir.
        Responder("ot.inventario", Parcial);
        Responder("ot.segmentacao", Nao);
        Responder("ot.senhapadrao", NaoSei);
        Responder("ot.exposicao", Nao);
        Responder("ot.atualizacao", Nao);

        // InventÃ¡rio: cinco tecnologias compradas, quase nenhuma operada. Ã‰ de onde sai o
        // indicador de uso do investimento que sustenta a conversa comercial.
        void Ferramenta(string dominio, string categoria, string fabricante, string produto,
            bool licenciado, bool atualizado, bool monitorado, bool alertas, bool integrada = false)
            => diagnostico.Ferramentas.Add(new DiagnosticoFerramenta
            {
                DominioCodigo = dominio, Categoria = categoria, Fabricante = fabricante, Produto = produto,
                Licenciado = licenciado, Atualizado = atualizado, Monitorado = monitorado,
                AlertasTratados = alertas, IntegradaAoLokta = integrada,
            });

        Ferramenta("rede", "Firewall", "Fortinet", "FortiGate 100F", licenciado: false, atualizado: false, monitorado: false, alertas: false);
        Ferramenta("endpoint", "EDR / AntivÃ­rus", "Microsoft", "Defender for Endpoint P1", licenciado: true, atualizado: true, monitorado: false, alertas: false);
        Ferramenta("backup", "Backup", "Veeam", "Backup & Replication", licenciado: true, atualizado: true, monitorado: true, alertas: false);
        Ferramenta("identidade", "Identidade", "Microsoft", "Entra ID P1", licenciado: true, atualizado: true, monitorado: false, alertas: false);
        Ferramenta("infra", "Monitoramento", "Zabbix", "Zabbix 6", licenciado: true, atualizado: false, monitorado: true, alertas: false);

        return diagnostico;
    }

    // Nomes exatos das empresas fictÃ­cias criadas por SeedCompaniesAsync. Mantido separado da
    // tupla do seed porque o backfill precisa deles mesmo quando o seed nÃ£o roda (banco jÃ¡
    // populado, que Ã© justamente o caso de produÃ§Ã£o).
    private static readonly string[] EmpresasDemoNomes =
    [
        "Grupo Vector", "Hospital Santa Clara", "Banco Meridiano", "Prefeitura Digital",
        "Loja Ativa", "Pagou Fintech", "NetSul Provedor", "EscritÃ³rio Lemos",
    ];

    // Company.Demo nasceu depois que o banco de produÃ§Ã£o jÃ¡ tinha as 8 empresas fictÃ­cias, e
    // SeedCompaniesAsync faz early-return quando jÃ¡ existe qualquer empresa â€” entÃ£o elas nunca
    // seriam marcadas sozinhas. Casa pelo NOME exato do seed: empresas criadas pelo operador
    // (inclusive as de teste dele, que guardam domÃ­nios reais) nÃ£o sÃ£o tocadas. Idempotente.
    private static async Task BackfillEmpresasDemoAsync(ApplicationDbContext db)
    {
        var pendentes = await db.Companies
            .Where(c => !c.Demo && EmpresasDemoNomes.Contains(c.Nome))
            .ToListAsync();
        if (pendentes.Count == 0)
        {
            return;
        }

        foreach (var empresa in pendentes)
        {
            empresa.Demo = true;
        }
        await db.SaveChangesAsync();
    }

    // Rebranding Okta-IA â†’ L'okta IA (loktaia.com): SeedContactChannelsAsync sÃ³ roda uma vez, entÃ£o
    // o e-mail antigo jÃ¡ gravado em produÃ§Ã£o nunca seria atualizado sozinho. SÃ³ troca se o valor
    // ainda for exatamente o default antigo â€” nunca sobrescreve ediÃ§Ã£o manual feita via /Contato.
    private static async Task BackfillRebrandLoktaiaAsync(ApplicationDbContext db)
    {
        var canal = await db.ContactChannels.FirstOrDefaultAsync(c => c.Chave == "COMERCIAL" && c.Valor == "info@okta-ia.com");
        if (canal is not null)
        {
            canal.Valor = "info@loktaia.com";
            await db.SaveChangesAsync();
        }
    }

    // DÃ¡ aos perfis nativos exatamente o acesso que eles jÃ¡ tinham via [Authorize(Roles="...")]
    // hardcoded antes da grade de permissÃµes existir â€” Admin ganha tudo (embora ele sempre passe
    // direto no AreaPermissionFilter, sem nem consultar esta tabela) e Analista ganha sÃ³ o SOC,
    // preservando o comportamento real que jÃ¡ existia em produÃ§Ã£o.
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

    // SeedRolePermissionsAsync sÃ³ roda uma vez (early-return se a tabela jÃ¡ tem qualquer linha) â€”
    // entÃ£o uma Ã¡rea nova adicionada depois do primeiro deploy nunca seria concedida a ninguÃ©m alÃ©m
    // do Admin (que sempre passa direto, sem consultar a tabela). Backfill idempotente: dÃ¡ "soc.twin"
    // a todo papel que jÃ¡ tinha "soc.ativos" (o Digital Twin Ã© sÃ³ outra visÃ£o do mesmo inventÃ¡rio).
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

    // Mesma armadilha do backfill acima: Ã¡rea criada depois do primeiro deploy nÃ£o Ã© concedida a
    // ninguÃ©m. Quem jÃ¡ podia ver "soc.vulnerabilidades" passa a ver "soc.alertas" â€” sÃ£o as duas
    // faces do mesmo trabalho (achado nosso Ã— alerta da ferramenta do cliente).
    private static async Task BackfillAlertasPermissionAsync(ApplicationDbContext db)
    {
        const string area = "soc.alertas";
        if (await db.RolePermissions.AnyAsync(rp => rp.AreaKey == area))
        {
            return;
        }

        var roleIds = await db.RolePermissions
            .Where(rp => rp.AreaKey == "soc.vulnerabilidades")
            .Select(rp => rp.RoleId)
            .Distinct()
            .ToListAsync();

        foreach (var roleId in roleIds)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, AreaKey = area });
        }

        await db.SaveChangesAsync();
    }

    // Mesma armadilha dos dois backfills acima. Quem jÃ¡ podia mexer em conectores passa a enxergar o
    // manual de implantaÃ§Ã£o â€” de nada adianta a pÃ¡gina existir se quem implanta nÃ£o a vÃª.
    private static async Task BackfillInformacoesPermissionAsync(ApplicationDbContext db)
    {
        const string area = "admin.infoconectores";
        if (await db.RolePermissions.AnyAsync(rp => rp.AreaKey == area))
        {
            return;
        }

        var roleIds = await db.RolePermissions
            .Where(rp => rp.AreaKey == "admin.connectors")
            .Select(rp => rp.RoleId)
            .Distinct()
            .ToListAsync();

        foreach (var roleId in roleIds)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, AreaKey = area });
        }

        await db.SaveChangesAsync();
    }

    // Mesmos 4 canais fixos que existiam hardcoded em MarketingContent.Channels â€” viram dado real
    // pra Admin poder editar/adicionar/excluir direto em /Contato.
    private static async Task SeedContactChannelsAsync(ApplicationDbContext db)
    {
        if (await db.ContactChannels.AnyAsync())
        {
            return;
        }

        (string Chave, string Cor, string Valor, string Descricao)[] dados =
        [
            ("COMERCIAL", "#4D9BFF", "info@loktaia.com", "Propostas, demonstraÃ§Ãµes e parcerias"),
            ("TELEFONE", "#00E0A4", "+55 11 3042-9392", "Segunda a sexta, 8h Ã s 18h"),
            ("WHATSAPP", "#00E0A4", "+55 34 9 9677-8585", "Atendimento comercial e suporte"),
            ("ENDEREÃ‡O", "#8A7BFF", "Av. Paulista, 2006, cj 1314, Bela Vista, SÃ£o Paulo-SP, CEP 01.310-926", "Atendimento presencial mediante agendamento"),
        ];

        for (var i = 0; i < dados.Length; i++)
        {
            var d = dados[i];
            db.ContactChannels.Add(new ContactChannel { Chave = d.Chave, Cor = d.Cor, Valor = d.Valor, Descricao = d.Descricao, Ordem = i });
        }

        await db.SaveChangesAsync();
    }

    // HistÃ³rico anterior Ã  existÃªncia do recurso de auditoria em si â€” backfill de lanÃ§amento,
    // igual ao histÃ³rico de 48h do SIEM. AÃ§Ãµes reais do console Admin (via AdminAuditService)
    // se acumulam a partir daqui.
    private static async Task SeedAdminAuditLogAsync(ApplicationDbContext db)
    {
        if (await db.AdminAuditLogs.AnyAsync())
        {
            return;
        }

        (string Hora, string Acao, string Detalhe, string Autor, string Ip)[] linhas =
        [
            ("14:42", "LOGIN", "AutenticaÃ§Ã£o bem-sucedida com passkey", "ricardo.silva", "189.44.12.8"),
            ("14:31", "UPDATE", "Perfil de Diego Moraes alterado para Gestor", "ricardo.silva", "189.44.12.8"),
            ("14:18", "CREATE", "OrganizaÃ§Ã£o EscritÃ³rio Lemos provisionada", "ricardo.silva", "189.44.12.8"),
            ("13:57", "EXPORT", "RelatÃ³rio de vulnerabilidades exportado em XLSX", "b.teixeira", "200.19.8.61"),
            ("13:40", "CONTAIN", "Bloqueio de 96 IPs aplicado no firewall de borda", "sistema Â· IA", "interno"),
            ("13:22", "DENY", "Tentativa de acesso negada Â· MFA invÃ¡lido", "p.lemos", "45.132.8.19"),
            ("12:55", "UPDATE", "Chave de API rotacionada para Banco Meridiano", "b.teixeira", "200.19.8.61"),
            ("12:14", "READ", "Consulta a logs de auditoria do Ãºltimo trimestre", "a.nakamura", "189.44.12.9"),
            ("11:48", "CREATE", "Regra de correlaÃ§Ã£o SIEM adicionada", "c.duarte", "189.44.12.9"),
            ("11:20", "UPDATE", "PolÃ­tica de retenÃ§Ã£o alterada de 3 para 5 anos", "ricardo.silva", "189.44.12.8"),
            ("10:36", "DELETE", "IntegraÃ§Ã£o GitLab desconectada", "ricardo.silva", "189.44.12.8"),
            ("09:52", "LOGIN", "AutenticaÃ§Ã£o bem-sucedida com TOTP", "m.rocha", "200.19.8.61"),
            ("09:11", "EXPORT", "Trilha de auditoria exportada para o encarregado", "b.teixeira", "200.19.8.61"),
            ("08:44", "SUSPEND", "Conta de Paula Lemos suspensa por inatividade", "polÃ­tica", "interno"),
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

    // Empresas de exemplo â€” os mesmos 8 tenants do mockup original, pra manter fidelidade de
    // design (nomes, setores, score de risco e contagens aparecem em vÃ¡rios mÃ³dulos).
    private static async Task SeedCompaniesAsync(ApplicationDbContext db)
    {
        if (await db.Companies.AnyAsync())
        {
            return;
        }

        var culture = CultureInfo.InvariantCulture;
        (string Nome, string SetorPt, string SetorEn, string Plano, int Score, int Ativos, int Vulns, int Incidentes, string Uptime, int Usuarios, string Status)[] dados =
        [
            ("Grupo Vector", "IndÃºstria", "Industry", "Enterprise", 78, 1284, 12, 3, "99.9", 9, "ativa"),
            ("Hospital Santa Clara", "SaÃºde", "Healthcare", "Enterprise", 64, 412, 7, 1, "99.9", 7, "ativa"),
            ("Banco Meridiano", "Financeiro", "Finance", "Enterprise+", 31, 2140, 3, 0, "100", 12, "ativa"),
            ("Prefeitura Digital", "Governo", "Government", "Gov", 86, 318, 21, 5, "98.4", 6, "ativa"),
            ("Loja Ativa", "Varejo", "Retail", "Business", 52, 96, 9, 1, "99.7", 2, "inadimplente"),
            ("Pagou Fintech", "Fintech", "Fintech", "Enterprise", 44, 588, 5, 2, "99.9", 4, "ativa"),
            ("NetSul Provedor", "ISP", "ISP", "MSP", 69, 1902, 14, 2, "99.6", 2, "ativa"),
            ("EscritÃ³rio Lemos", "JurÃ­dico", "Legal", "Business", 27, 48, 2, 0, "99.9", 1, "trial"),
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
                Demo = true,   // empresa fictÃ­cia â€” a UI avisa que o ambiente nÃ£o Ã© real
            });
        }

        await db.SaveChangesAsync();
    }

    // Gera um histÃ³rico plausÃ­vel das Ãºltimas 48h â€” alimenta KPIs (Eventos 24h/Bloqueados),
    // mapa de origem, "principais origens" e o fluxo de eventos, tudo com dado real de banco em
    // vez do gerador aleatÃ³rio em memÃ³ria do mockup.
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
    // (Alvo/AssetNome/Asset) com Assets.Nome, que jÃ¡ tem CompanyId real â€” evita reinventar essa
    // associaÃ§Ã£o em 3 lugares diferentes.
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

    // Mesmos 9 ativos de exemplo do mockup original, ligados Ã s empresas correspondentes (o
    // mockup nÃ£o amarra ativoâ†”empresa explicitamente; a associaÃ§Ã£o abaixo segue o nome/domÃ­nio
    // de cada ativo, que jÃ¡ indica o dono).
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
                // Array do mockup vem em ordem decrescente de severidade [CrÃ­tica,Alta,MÃ©dia,Baixa].
                VulnsCriticas = d.Vulns[0],
                VulnsAltas = d.Vulns[1],
                VulnsMedias = d.Vulns[2],
                VulnsBaixas = d.Vulns[3],
                Saude = d.Saude,
            });
        }

        await db.SaveChangesAsync();
    }

    // Mesmas 8 CVEs de exemplo do mockup original (CVE/CVSS/CWE/exposiÃ§Ã£o/prioridade IA/status).
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
            ("CVE-2024-3094", 10.0m, "xz-utils", "Backdoor em biblioteca de compressÃ£o (SSH)", "Backdoor in compression library (SSH)", "CWE-506", "srv-db-prod-02", "KEV", "KEV", 97, "Aberto", "Open", Severidade.Critica),
            ("CVE-2024-6387", 8.1m, "OpenSSH", "RCE nÃ£o autenticado â€” regreSSHion", "Unauthenticated RCE â€” regreSSHion", "CWE-364", "vpn-sp01", "KEV", "KEV", 93, "Em correÃ§Ã£o", "Patching", Severidade.Critica),
            ("CVE-2021-44228", 10.0m, "Log4j", "ExecuÃ§Ã£o remota de cÃ³digo via JNDI", "Remote code execution via JNDI", "CWE-502", "api.grupovector.com", "KEV", "KEV", 89, "Aberto", "Open", Severidade.Critica),
            ("CVE-2024-21762", 9.8m, "FortiOS", "Escrita fora de limites no SSL-VPN", "Out-of-bounds write in SSL-VPN", "CWE-787", "fw-borda-01", "Interno", "Internal", 76, "Aberto", "Open", Severidade.Critica),
            ("CVE-2023-44487", 7.5m, "HTTP/2", "Rapid Reset â€” negaÃ§Ã£o de serviÃ§o", "Rapid Reset â€” denial of service", "CWE-400", "checkout.pagou.io", "PÃºblico", "Public", 71, "Mitigado", "Mitigated", Severidade.Alta),
            ("CVE-2022-22965", 9.8m, "Spring", "Spring4Shell â€” binding de classe", "Spring4Shell â€” class binding", "CWE-94", "portal.hsanta.br", "PÃºblico", "Public", 68, "Em correÃ§Ã£o", "Patching", Severidade.Critica),
            ("CVE-2023-4863", 8.8m, "libwebp", "Estouro de heap no decodificador WebP", "Heap overflow in WebP decoder", "CWE-787", "wp.lojaativa.com.br", "PÃºblico", "Public", 54, "Aberto", "Open", Severidade.Alta),
            ("CVE-2024-23897", 9.8m, "Jenkins", "Leitura arbitrÃ¡ria de arquivos via CLI", "Arbitrary file read via CLI", "CWE-22", "ci.grupovector.com", "Interno", "Internal", 48, "Aceito", "Accepted", Severidade.Critica),
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
    // tempo completos â€” AbertoEm Ã© derivado da "idade" (12m/1h/3h/...) pra ficar um timestamp
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
                "Credential stuffing distribuÃ­do contra concentrador VPN", "Distributed credential stuffing against VPN concentrator",
                "3 ASNs, 1.842 usuÃ¡rios testados", "3 ASNs, 1,842 users tested", "94%",
                "Detectei 3.418 tentativas de autenticaÃ§Ã£o falhas em 41 minutos contra vpn-sp01, partindo de 214 IPs distribuÃ­dos em 3 sistemas autÃ´nomos russos. As credenciais testadas coincidem em 61% com um vazamento pÃºblico de 2024 associado ao domÃ­nio corporativo. Nenhuma sessÃ£o foi estabelecida, mas duas contas de serviÃ§o sem MFA foram alvo repetido â€” elas representam o caminho mais provÃ¡vel de sucesso caso o ataque continue.",
                "I detected 3,418 failed authentication attempts in 41 minutes against vpn-sp01, from 214 IPs across 3 Russian autonomous systems. Tested credentials match a 2024 public breach tied to the corporate domain at 61%. No session was established, but two service accounts without MFA were repeatedly targeted â€” they are the most likely path to success if the attack continues.",
                [
                    ("Bloquear os 3 ASNs de origem no firewall de borda por 24h", "Block the 3 source ASNs at the edge firewall for 24h", "FIREWALL"),
                    ("ForÃ§ar MFA nas contas svc-backup e svc-integra", "Enforce MFA on svc-backup and svc-integra accounts", "IAM"),
                    ("Rotacionar credenciais das 1.842 contas testadas", "Rotate credentials for the 1,842 tested accounts", "IAM"),
                    ("Ativar rate limit adaptativo no concentrador VPN", "Enable adaptive rate limiting on the VPN concentrator", "NETWORK"),
                ],
                [
                    ("14:02", "#FF3B5C", "Primeiro pico anÃ´malo de falhas de autenticaÃ§Ã£o", "First anomalous spike in auth failures", "siem.auth"),
                    ("14:09", "#FF8A3D", "IA correlacionou 214 IPs a 3 ASNs conhecidos", "AI correlated 214 IPs to 3 known ASNs", "ai.correlation"),
                    ("14:17", "#FFC93C", "Incidente aberto automaticamente Â· severidade crÃ­tica", "Incident auto-opened Â· critical severity", "soc.rules"),
                    ("14:23", "#00E0A4", "Bloqueio temporÃ¡rio de 96 IPs aplicado", "Temporary block applied to 96 IPs", "firewall.edge"),
                    ("14:31", "#4D9BFF", "Analista R. Silva assumiu o incidente", "Analyst R. Silva took ownership", "soc.console"),
                ]),
            ("INC-4818", Severidade.Critica, TimeSpan.FromHours(1), "wp.lojaativa.com.br", "T1190", "C. Duarte", 842,
                "ExploraÃ§Ã£o ativa de plugin WordPress desatualizado", "Active exploitation of outdated WordPress plugin",
                "Webshell provÃ¡vel em /wp-content/uploads", "Likely webshell in /wp-content/uploads", "88%",
                "RequisiÃ§Ãµes POST com payload ofuscado atingiram um plugin de formulÃ¡rios na versÃ£o 4.1.2, vulnerÃ¡vel a upload arbitrÃ¡rio. Um arquivo PHP foi criado em /wp-content/uploads/2026/07 e acessado 6 vezes a partir do mesmo IP holandÃªs. O padrÃ£o de acesso indica webshell funcional. Recomendo isolamento imediato antes de qualquer anÃ¡lise forense.",
                "POST requests with obfuscated payload hit a forms plugin on version 4.1.2, vulnerable to arbitrary upload. A PHP file was created in /wp-content/uploads/2026/07 and accessed 6 times from the same Dutch IP. The access pattern indicates a working webshell. I recommend immediate isolation before any forensic analysis.",
                [
                    ("Isolar o host da rede mantendo memÃ³ria para forense", "Isolate the host from the network, preserving memory for forensics", "CONTAIN"),
                    ("Quarentenar o arquivo suspeito e coletar hash", "Quarantine the suspicious file and collect its hash", "FORENSIC"),
                    ("Atualizar plugin para 4.3.1 e revisar todos os uploads", "Update plugin to 4.3.1 and review all uploads", "PATCH"),
                ],
                [
                    ("13:11", "#FF3B5C", "POST anÃ´malo detectado pelo WAF", "Anomalous POST detected by WAF", "waf.rules"),
                    ("13:14", "#FF8A3D", "CriaÃ§Ã£o de arquivo PHP em diretÃ³rio de uploads", "PHP file created in uploads directory", "fim.agent"),
                    ("13:22", "#FFC93C", "IA classificou como webshell com 88% de confianÃ§a", "AI classified as webshell with 88% confidence", "ai.malware"),
                    ("13:40", "#4D9BFF", "NotificaÃ§Ã£o enviada ao cliente Loja Ativa", "Notification sent to client Loja Ativa", "notify.email"),
                ]),
            ("INC-4815", Severidade.Alta, TimeSpan.FromHours(3), "srv-db-prod-02", "T1046", "A. Nakamura", 1204,
                "Varredura interna lateral a partir de estaÃ§Ã£o comprometida", "Internal lateral scan from a compromised workstation",
                "Movimento lateral em 3 sub-redes", "Lateral movement across 3 subnets", "76%",
                "Uma estaÃ§Ã£o da rede administrativa iniciou varredura de portas em 3 sub-redes, incluindo o segmento de banco de dados. O comportamento diverge da linha de base do usuÃ¡rio em 4 dimensÃµes: horÃ¡rio, volume, destinos e protocolo. Probabilidade elevada de estaÃ§Ã£o comprometida servindo de pivÃ´.",
                "A workstation in the admin network started port scanning across 3 subnets, including the database segment. Behavior diverges from the user baseline on 4 dimensions: time, volume, destinations and protocol. High probability of a compromised workstation acting as pivot.",
                [
                    ("Isolar a estaÃ§Ã£o WKS-ADM-14 via EDR", "Isolate workstation WKS-ADM-14 via EDR", "EDR"),
                    ("Revisar regras de microsegmentaÃ§Ã£o do segmento de dados", "Review microsegmentation rules for the data segment", "NETWORK"),
                    ("Coletar dump de memÃ³ria para anÃ¡lise de credenciais", "Collect memory dump for credential analysis", "FORENSIC"),
                ],
                [
                    ("11:28", "#FF8A3D", "Varredura de portas iniciada em 10.20.0.0/16", "Port scan started on 10.20.0.0/16", "ids.suricata"),
                    ("11:35", "#FFC93C", "Desvio comportamental de 4Ïƒ sinalizado pela IA", "4Ïƒ behavioral deviation flagged by AI", "ai.ueba"),
                    ("11:52", "#4D9BFF", "Incidente escalado para nÃ­vel 2", "Incident escalated to tier 2", "soc.console"),
                ]),
            ("INC-4809", Severidade.Media, TimeSpan.FromHours(6), "api.grupovector.com", "T1499", null!, 96,
                "Abuso de rate limit em endpoint pÃºblico de consulta", "Rate-limit abuse on public query endpoint",
                "Scraping automatizado, sem exfiltraÃ§Ã£o", "Automated scraping, no exfiltration", "82%",
                "Um Ãºnico token de API consumiu 96 mil requisiÃ§Ãµes em 2 horas contra /v2/catalog, 40 vezes acima do padrÃ£o. NÃ£o hÃ¡ indÃ­cio de exfiltraÃ§Ã£o de dados sensÃ­veis â€” o endpoint retorna apenas catÃ¡logo pÃºblico â€” mas o custo de infraestrutura e a latÃªncia para clientes legÃ­timos aumentaram.",
                "A single API token consumed 96k requests in 2 hours against /v2/catalog, 40Ã— above baseline. There is no sign of sensitive data exfiltration â€” the endpoint only returns a public catalog â€” but infrastructure cost and latency for legitimate clients increased.",
                [
                    ("Aplicar quota escalonada ao token afetado", "Apply tiered quota to the affected token", "API"),
                    ("Habilitar cache de borda para /v2/catalog", "Enable edge caching for /v2/catalog", "CDN"),
                ],
                [
                    ("08:44", "#FFC93C", "Limite de requisiÃ§Ãµes excedido 40Ã—", "Request limit exceeded 40Ã—", "gateway.api"),
                    ("09:10", "#4D9BFF", "IA descartou exfiltraÃ§Ã£o de dados", "AI ruled out data exfiltration", "ai.classifier"),
                ]),
            ("INC-4802", Severidade.Alta, TimeSpan.FromHours(9), "mail.prefdigital.gov.br", "T1566.002", "M. Rocha", 318,
                "Campanha de phishing direcionada a servidores pÃºblicos", "Phishing campaign targeting public servants",
                "318 mensagens, 11 cliques confirmados", "318 messages, 11 confirmed clicks", "91%",
                "Campanha com domÃ­nio typosquatting registrado hÃ¡ 4 dias entregou 318 mensagens simulando o portal de contracheque. Onze usuÃ¡rios clicaram e trÃªs submeteram credenciais na pÃ¡gina falsa. As trÃªs contas jÃ¡ apresentam tentativas de login externo a partir de IPs nÃ£o habituais.",
                "A campaign using a typosquatting domain registered 4 days ago delivered 318 messages impersonating the payroll portal. Eleven users clicked and three submitted credentials on the fake page. All three accounts already show external login attempts from unusual IPs.",
                [
                    ("Revogar sessÃµes e resetar senha das 3 contas afetadas", "Revoke sessions and reset passwords for the 3 affected accounts", "IAM"),
                    ("Bloquear domÃ­nio typosquatting no gateway de e-mail", "Block the typosquatting domain at the mail gateway", "EMAIL"),
                    ("Remover mensagens das caixas de entrada restantes", "Purge messages from remaining inboxes", "EMAIL"),
                    ("Disparar treinamento direcionado aos 11 usuÃ¡rios", "Trigger targeted training for the 11 users", "AWARENESS"),
                ],
                [
                    ("05:30", "#FF8A3D", "318 mensagens entregues de domÃ­nio recÃ©m-registrado", "318 messages delivered from newly registered domain", "mail.gateway"),
                    ("06:12", "#FF3B5C", "Primeiro clique confirmado em link malicioso", "First confirmed click on malicious link", "proxy.web"),
                    ("07:05", "#FFC93C", "IA identificou 3 submissÃµes de credenciais", "AI identified 3 credential submissions", "ai.phishing"),
                    ("07:40", "#00E0A4", "DomÃ­nio bloqueado em todos os tenants", "Domain blocked across all tenants", "threat.intel"),
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

