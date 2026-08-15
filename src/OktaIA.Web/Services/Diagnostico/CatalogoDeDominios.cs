using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Diagnostico;

/// <summary>
/// O questionário. Vive em código, não no banco — mesma decisão de `AdminCatalog` e
/// `CatalogoDeRoteiros`: é conteúdo versionado junto com a lógica que o interpreta, revisável em
/// diff, e não exige construir um editor de questionário antes de ter o primeiro cliente. As
/// respostas gravadas apontam para os códigos daqui, então o catálogo pode crescer sem invalidar
/// diagnóstico antigo.
///
/// ⚠️ **Código de pergunta é contrato.** Uma vez usado num diagnóstico real, nunca renomear nem
/// reaproveitar com outro sentido — a resposta gravada perderia o vínculo em silêncio.
///
/// As perguntas de detalhe (fabricante, versão, quem administra) existem porque **ter a tecnologia
/// não significa que o controle está adequado**. É a diferença entre "tem firewall" e "tem firewall
/// com licença ativa, regra revisada e log chegando em algum lugar" — e é onde a conversa comercial
/// realmente acontece.
/// </summary>
public static class CatalogoDeDominios
{
    public const string Sim = "sim";
    public const string Parcial = "parcial";
    public const string Nao = "nao";
    public const string NaoSei = "naosei";

    /// <summary>Opções padrão de um controle. "Não sei" é resposta legítima e informativa: quem não
    /// sabe se tem, não tem gestão — e isso precisa aparecer no relatório em vez de virar "não".</summary>
    private static readonly string[] OpcoesControle = [Sim, Parcial, Nao, NaoSei];

    /// <summary>
    /// Todos os domínios, na ordem do levantamento.
    ///
    /// ⚠️ Montado em construtor estático de propósito. Inicializador de propriedade roda na ordem
    /// de declaração do arquivo: como esta lista aparece ANTES dos domínios que referencia, um
    /// `{ get; } = [Perfil, ...]` produziria um array de nulos em silêncio — compila, e só quebra
    /// em execução. O construtor estático roda depois de todos os inicializadores, então aqui a
    /// ordem de leitura do arquivo pode seguir a ordem que faz sentido para quem lê.
    /// </summary>
    public static IReadOnlyList<DominioDeSeguranca> Todos { get; }

    static CatalogoDeDominios()
    {
        Todos =
        [
            Perfil, Rede, Endpoint, Identidade, Email, Backup, Infraestrutura, Nuvem,
            Vulnerabilidades, Monitoramento, RespostaAIncidentes, Governanca, Terceiros,
            DevSecOps, OtIot,
        ];
    }

    public static DominioDeSeguranca? Buscar(string codigo) =>
        Todos.FirstOrDefault(d => d.Codigo == codigo);

    public static PerguntaDoDiagnostico? BuscarPergunta(string codigo) =>
        Todos.SelectMany(d => d.Perguntas).FirstOrDefault(p => p.Codigo == codigo);

    public static DominioDeSeguranca? DominioDaPergunta(string codigo) =>
        Todos.FirstOrDefault(d => d.Perguntas.Any(p => p.Codigo == codigo));

    // ── 1. Perfil ────────────────────────────────────────────────────────────
    // Não pontua: é contexto. Mas define quais domínios aparecem depois — não faz sentido perguntar
    // sobre DevSecOps para quem não desenvolve, nem sobre OT para um escritório de advocacia.

    public static DominioDeSeguranca Perfil { get; } = new()
    {
        Codigo = "perfil",
        Nome = "Perfil da empresa",
        Resumo = "O contexto que decide o que faz sentido perguntar adiante.",
        Ordem = 1,
        Pontua = false,
        Perguntas =
        [
            new() { Codigo = "perfil.segmento", Texto = "Segmento de atuação", Tipo = TipoDePergunta.Escolha, Peso = 0,
                Opcoes = ["Serviços", "Indústria", "Varejo", "Saúde", "Educação", "Financeiro", "Governo", "Tecnologia", "Logística", "Agro", "Outro"] },
            new() { Codigo = "perfil.funcionarios", Texto = "Número de funcionários", Tipo = TipoDePergunta.Numero, Peso = 0 },
            new() { Codigo = "perfil.usuariosti", Texto = "Usuários de TI (contas ativas)", Tipo = TipoDePergunta.Numero, Peso = 0 },
            new() { Codigo = "perfil.unidades", Texto = "Unidades e filiais", Tipo = TipoDePergunta.Numero, Peso = 0 },
            new() { Codigo = "perfil.endpoints", Texto = "Endpoints aproximados (desktops, notebooks)", Tipo = TipoDePergunta.Numero, Peso = 0 },
            new() { Codigo = "perfil.servidores", Texto = "Servidores aproximados (físicos e virtuais)", Tipo = TipoDePergunta.Numero, Peso = 0 },
            new() { Codigo = "perfil.operacao", Texto = "Regime de operação", Tipo = TipoDePergunta.Escolha, Peso = 0,
                Opcoes = ["Horário comercial", "Estendido", "24x7"],
                Ajuda = "Define se um SOC 8x5 é suficiente ou se há janela desprotegida." },
            new() { Codigo = "perfil.remoto", Texto = "Existe trabalho remoto?", Peso = 0, Opcoes = OpcoesControle },
            new() { Codigo = "perfil.desenvolve", Texto = "A empresa desenvolve software próprio?", Peso = 0, Opcoes = OpcoesControle,
                Ajuda = "Libera o bloco de DevSecOps." },
            new() { Codigo = "perfil.ot", Texto = "Existem equipamentos industriais, médicos ou IoT em rede?", Peso = 0, Opcoes = OpcoesControle,
                Ajuda = "Libera o bloco de OT/IoT." },
            new() { Codigo = "perfil.sistemascriticos", Texto = "Sistemas críticos (ERP, CRM, sistemas próprios)", Tipo = TipoDePergunta.Texto, Peso = 0 },
            new() { Codigo = "perfil.dados", Texto = "Tipos de dado sensível tratados", Tipo = TipoDePergunta.Multipla, Peso = 0,
                Opcoes = ["Dados pessoais", "Dados financeiros", "Dados de saúde", "Propriedade intelectual", "Informação estratégica", "Dados de menores"],
                Ajuda = "Define o peso do bloco de governança e o tom do relatório executivo." },
        ],
    };

    // ── 2. Rede ──────────────────────────────────────────────────────────────

    public static DominioDeSeguranca Rede { get; } = new()
    {
        Codigo = "rede",
        Nome = "Rede e perímetro",
        Resumo = "O que separa a rede interna da internet, e o que acontece dentro dela.",
        Ordem = 2,
        Perguntas =
        [
            new() { Codigo = "rede.firewall", Texto = "A empresa possui firewall corporativo?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "Perímetro sem firewall corporativo",
                SeNaoTratar = "Sem controle de perímetro, qualquer serviço publicado fica exposto direto à internet e não há registro de quem tentou entrar.",
                Recomendacao = "Implantar firewall de próxima geração com política de bloqueio por padrão e registro de eventos.",
                Frameworks = ["NIST PR.AC-5", "CIS 4.4", "ISO A.8.20"] },

            // Ramificação quando NÃO tem: o levantamento precisa dimensionar o buraco.
            new() { Codigo = "rede.links", Texto = "Quantos links de internet existem?", Tipo = TipoDePergunta.Numero, Peso = 0,
                SomenteSe = new("rede.firewall", [Nao, NaoSei]) },
            new() { Codigo = "rede.publicados", Texto = "Existem serviços publicados na internet?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("rede.firewall", [Nao, NaoSei]),
                RiscoSeNao = null,
                Ajuda = "Servidor de e-mail, VPN, portal, câmera, acesso remoto." },

            // Ramificação quando TEM: possuir não é o mesmo que estar adequado.
            new() { Codigo = "rede.firewall.fabricante", Texto = "Fabricante do firewall", Tipo = TipoDePergunta.Escolha, Peso = 0,
                SomenteSe = new("rede.firewall", [Sim, Parcial]),
                Opcoes = ["Fortinet", "Palo Alto", "Cisco", "Sophos", "Check Point", "SonicWall", "WatchGuard", "pfSense/OPNsense", "Mikrotik", "Outro"] },
            new() { Codigo = "rede.firewall.licenca", Texto = "O licenciamento de segurança está ativo?", Peso = 3,
                Opcoes = OpcoesControle, SomenteSe = new("rede.firewall", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Firewall com licenciamento de segurança vencido",
                SeNaoTratar = "Firewall com licença vencida continua roteando pacote, mas para de inspecionar: o equipamento parece funcionar e a proteção não existe mais.",
                Recomendacao = "Renovar as assinaturas de IPS, antivírus e filtro de conteúdo, e monitorar a data de expiração.",
                Frameworks = ["CIS 4.4"] },
            new() { Codigo = "rede.firewall.firmware", Texto = "O firmware está atualizado?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("rede.firewall", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Firewall com firmware desatualizado",
                SeNaoTratar = "Falhas em firewall de borda são alvo preferencial e costumam ter exploração pública dias após a divulgação.",
                Recomendacao = "Definir janela de atualização e acompanhar os boletins do fabricante.",
                Frameworks = ["CIS 7.3"] },
            new() { Codigo = "rede.firewall.ha", Texto = "Existe redundância (alta disponibilidade)?", Peso = 1,
                Opcoes = OpcoesControle, SomenteSe = new("rede.firewall", [Sim, Parcial]) },
            new() { Codigo = "rede.firewall.regras", Texto = "As regras são revisadas periodicamente?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("rede.firewall", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Regras de firewall sem revisão periódica",
                SeNaoTratar = "Regras acumuladas ao longo de anos costumam liberar mais do que ninguém lembra de ter liberado.",
                Recomendacao = "Revisar a base de regras ao menos semestralmente, removendo o que não tem dono.",
                Frameworks = ["CIS 4.4", "ISO A.8.20"] },
            new() { Codigo = "rede.firewall.backupconfig", Texto = "Existe backup da configuração?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("rede.firewall", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Configuração do firewall sem backup",
                SeNaoTratar = "Perder o equipamento sem backup de configuração transforma uma troca de hardware em dias de indisponibilidade.",
                Recomendacao = "Exportar a configuração automaticamente e guardá-la fora do próprio equipamento." },
            new() { Codigo = "rede.firewall.quemadmin", Texto = "Quem administra?", Tipo = TipoDePergunta.Escolha, Peso = 0,
                SomenteSe = new("rede.firewall", [Sim, Parcial]),
                Opcoes = ["TI interna", "Fornecedor / revenda", "Ninguém em especial", "Não sei"] },

            new() { Codigo = "rede.logs", Texto = "Os logs do firewall são armazenados?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Logs de firewall não armazenados",
                SeNaoTratar = "Sem log guardado não há como reconstruir um incidente depois — e a pergunta 'o que o atacante levou' fica sem resposta possível.",
                Recomendacao = "Enviar os eventos para armazenamento externo com retenção definida.",
                Frameworks = ["NIST DE.AE-3", "CIS 8.2", "ISO A.8.15"] },
            new() { Codigo = "rede.ips", Texto = "Existe IDS/IPS ativo?", Peso = 2, Opcoes = OpcoesControle,
                RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Perímetro sem inspeção de intrusão (IDS/IPS)",
                SeNaoTratar = "Exploração de vulnerabilidade conhecida passa sem ser notada.",
                Recomendacao = "Habilitar e ajustar a inspeção de intrusão no perímetro." },
            new() { Codigo = "rede.vpn", Texto = "O acesso remoto é feito por VPN corporativa?", Peso = 3,
                Opcoes = OpcoesControle, SomenteSe = new("perfil.remoto", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "Acesso remoto fora de VPN corporativa",
                SeNaoTratar = "Acesso remoto exposto direto (RDP aberto, por exemplo) é a porta de entrada mais explorada em ataques de ransomware.",
                Recomendacao = "Concentrar o acesso remoto em VPN com múltiplo fator, e fechar o acesso direto.",
                Frameworks = ["NIST PR.AC-3", "CIS 12.7"] },
            new() { Codigo = "rede.segmentacao", Texto = "A rede é segmentada (VLAN, separação de ambientes)?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Rede sem segmentação entre ambientes",
                SeNaoTratar = "Rede plana faz um único equipamento comprometido alcançar servidores, backup e estações no mesmo movimento.",
                Recomendacao = "Separar servidores, estações, visitantes e dispositivos não gerenciados.",
                Frameworks = ["NIST PR.AC-5", "CIS 12.2"] },
            new() { Codigo = "rede.wifi", Texto = "A rede sem fio de visitantes é isolada da rede corporativa?", Peso = 1, Opcoes = OpcoesControle,
                RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Rede sem fio de visitantes sem isolamento da rede corporativa",
                SeNaoTratar = "Visitante na mesma rede dos servidores é acesso não autenticado ao ambiente interno." },
            new() { Codigo = "rede.dns", Texto = "Existe filtro de DNS ou de navegação?", Peso = 2, Opcoes = OpcoesControle,
                RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Navegação sem filtro de DNS ou de conteúdo",
                SeNaoTratar = "Boa parte do phishing e do malware depende de resolver um domínio recém-criado; sem filtro, resolve.",
                Recomendacao = "Adotar filtro de DNS com bloqueio por reputação e categoria." },
            new() { Codigo = "rede.ddos", Texto = "Existe proteção contra negação de serviço nos serviços publicados?", Peso = 1,
                Opcoes = OpcoesControle, SomenteSe = new("rede.publicados", [Sim, Parcial]) },
        ],
    };

    // ── 3. Endpoint ──────────────────────────────────────────────────────────

    public static DominioDeSeguranca Endpoint { get; } = new()
    {
        Codigo = "endpoint",
        Nome = "Endpoint",
        Resumo = "Onde o usuário clica — e onde a maioria dos incidentes começa.",
        Ordem = 3,
        Perguntas =
        [
            new() { Codigo = "endpoint.protecao", Texto = "Existe antivírus ou EDR em todos os endpoints?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "Endpoints sem antivírus ou EDR",
                SeNaoTratar = "Máquina sem proteção é o ponto de entrada mais provável, e a infecção só aparece quando já se espalhou.",
                Recomendacao = "Padronizar a proteção em 100% das estações e servidores, com console central.",
                Frameworks = ["NIST DE.CM-4", "CIS 10.1", "ISO A.8.7"] },
            new() { Codigo = "endpoint.fabricante", Texto = "Qual solução?", Tipo = TipoDePergunta.Escolha, Peso = 0,
                SomenteSe = new("endpoint.protecao", [Sim, Parcial]),
                Opcoes = ["Microsoft Defender", "CrowdStrike", "SentinelOne", "Sophos", "Trend Micro", "Bitdefender", "ESET", "Kaspersky", "Trellix", "Outro"] },
            new() { Codigo = "endpoint.edr", Texto = "A solução é EDR/XDR (e não apenas antivírus)?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("endpoint.protecao", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Proteção de endpoint sem EDR/XDR",
                SeNaoTratar = "Antivírus por assinatura não vê ataque que usa ferramenta legítima do próprio sistema — que é como a maioria opera hoje.",
                Recomendacao = "Migrar para EDR com detecção comportamental e capacidade de isolar a máquina." },
            new() { Codigo = "endpoint.cobertura", Texto = "A cobertura é conferida contra o inventário?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("endpoint.protecao", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Cobertura da proteção não conferida contra o inventário",
                SeNaoTratar = "Quase toda empresa que acredita ter 100% de cobertura descobre um grupo de máquinas sem agente — normalmente as mais antigas.",
                Recomendacao = "Comparar periodicamente a lista do console com o inventário de ativos.",
                Frameworks = ["CIS 1.1"] },
            new() { Codigo = "endpoint.alertas", Texto = "Alguém trata os alertas gerados?", Peso = 3,
                Opcoes = OpcoesControle, SomenteSe = new("endpoint.protecao", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Alertas de endpoint gerados sem ninguém tratar",
                SeNaoTratar = "Ferramenta que alerta para ninguém é custo, não proteção: a detecção acontece e o incidente segue.",
                Recomendacao = "Definir quem recebe, em quanto tempo responde, e o que faz — ou terceirizar a triagem." },
            new() { Codigo = "endpoint.ransomware", Texto = "Existe proteção específica contra ransomware?", Peso = 2, Opcoes = OpcoesControle,
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem proteção específica contra ransomware",
                SeNaoTratar = "Ransomware é o incidente com maior chance de parar a operação inteira de uma PME." },
            new() { Codigo = "endpoint.patch", Texto = "Existe gestão de atualizações (patch management)?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem gestão de atualizações (patch management)",
                SeNaoTratar = "A maioria das invasões usa falha conhecida com correção disponível há meses.",
                Recomendacao = "Implantar atualização automática com janela definida e relatório de aderência.",
                Frameworks = ["NIST PR.IP-12", "CIS 7.3", "ISO A.8.8"] },
            new() { Codigo = "endpoint.criptografia", Texto = "Os discos dos notebooks são criptografados?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Discos de notebooks sem criptografia",
                SeNaoTratar = "Notebook perdido sem criptografia é vazamento de dado pessoal com dever de comunicação à ANPD.",
                Recomendacao = "Ativar BitLocker ou equivalente com custódia central da chave.",
                Frameworks = ["LGPD art. 46", "CIS 3.6", "ISO A.8.24"] },
            new() { Codigo = "endpoint.usb", Texto = "Existe controle de dispositivos USB?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "endpoint.admin", Texto = "Os usuários trabalham sem privilégio de administrador local?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Usuários trabalhando com privilégio de administrador local",
                SeNaoTratar = "Usuário administrador transforma um clique errado em comprometimento completo da máquina.",
                Recomendacao = "Remover o administrador local e conceder elevação sob demanda.",
                Frameworks = ["CIS 5.4", "NIST PR.AC-4"] },
            new() { Codigo = "endpoint.mdm", Texto = "Os celulares que acessam dados corporativos são gerenciados (MDM)?", Peso = 1, Opcoes = OpcoesControle },
        ],
    };

    // ── 4. Identidade ────────────────────────────────────────────────────────

    public static DominioDeSeguranca Identidade { get; } = new()
    {
        Codigo = "identidade",
        Nome = "Identidade e acesso",
        Resumo = "Quem é quem, e o que cada um pode. O perímetro real hoje.",
        Ordem = 4,
        Perguntas =
        [
            new() { Codigo = "identidade.diretorio", Texto = "Existe diretório central (Active Directory / Entra ID)?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Identidades sem diretório central",
                SeNaoTratar = "Sem diretório central, desligar alguém exige lembrar de cada sistema — e sempre sobra um.",
                Recomendacao = "Centralizar identidades e integrar os sistemas ao diretório." },
            new() { Codigo = "identidade.mfa", Texto = "Há múltiplo fator de autenticação (MFA)?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "Acesso sem múltiplo fator de autenticação",
                SeNaoTratar = "Credencial vazada vira acesso imediato. É o controle isolado que mais reduz risco de comprometimento de conta.",
                Recomendacao = "Exigir MFA para todos, começando por administradores, acesso remoto e e-mail.",
                Frameworks = ["NIST PR.AC-7", "CIS 6.3", "ISO A.8.5"] },
            new() { Codigo = "identidade.mfa.cobertura", Texto = "O MFA cobre todos os usuários e sistemas?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("identidade.mfa", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Contas e sistemas fora do MFA",
                SeNaoTratar = "MFA parcial costuma deixar de fora exatamente as contas de serviço e de diretoria, que são as mais visadas." },
            new() { Codigo = "identidade.privilegiadas", Texto = "As contas privilegiadas são identificadas e controladas?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Contas privilegiadas sem identificação e controle",
                SeNaoTratar = "Conta de administrador compartilhada impede saber quem fez o quê e sobrevive à saída de quem a conhecia.",
                Recomendacao = "Nomear contas administrativas, separá-las do uso diário e revisar quem as detém.",
                Frameworks = ["CIS 5.1", "ISO A.8.2"] },
            new() { Codigo = "identidade.pam", Texto = "Existe cofre de senhas ou solução de acesso privilegiado (PAM)?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "identidade.desligados", Texto = "Contas de desligados são removidas com processo definido?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Contas de desligados sem processo de remoção",
                SeNaoTratar = "Ex-funcionário com acesso ativo é risco jurídico e operacional, e costuma ser descoberto tarde demais.",
                Recomendacao = "Ligar o desligamento do RH à revogação de acesso, com conferência mensal.",
                Frameworks = ["CIS 5.3", "ISO A.8.5", "LGPD art. 46"] },
            new() { Codigo = "identidade.revisao", Texto = "Os acessos são revisados periodicamente?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Acessos concedidos sem revisão periódica",
                SeNaoTratar = "Permissão acumulada por troca de área é como um usuário comum termina com acesso a tudo." },
            new() { Codigo = "identidade.senhas", Texto = "Existe política de senha aplicada tecnicamente?", Peso = 2, Opcoes = OpcoesControle,
                Ajuda = "Aplicada tecnicamente, não apenas escrita em documento." },
            new() { Codigo = "identidade.servico", Texto = "As contas de serviço são inventariadas?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "identidade.terceiros", Texto = "Terceiros têm acesso nominal e temporário?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Terceiros com acesso não nominal ou sem prazo",
                SeNaoTratar = "Acesso permanente de fornecedor é caminho de invasão que não depende de errar nada internamente." },
            new() { Codigo = "identidade.sso", Texto = "Existe SSO para os principais sistemas?", Peso = 1, Opcoes = OpcoesControle },
        ],
    };

    // ── 5. E-mail ────────────────────────────────────────────────────────────

    public static DominioDeSeguranca Email { get; } = new()
    {
        Codigo = "email",
        Nome = "E-mail",
        Resumo = "O canal por onde a maior parte dos ataques chega.",
        Ordem = 5,
        Perguntas =
        [
            new() { Codigo = "email.plataforma", Texto = "Qual plataforma de e-mail?", Tipo = TipoDePergunta.Escolha, Peso = 0,
                Opcoes = ["Microsoft 365", "Google Workspace", "Servidor próprio", "Provedor de hospedagem", "Outro"] },
            new() { Codigo = "email.antiphishing", Texto = "Existe proteção contra phishing além do antispam básico?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "E-mail sem proteção contra phishing além do antispam",
                SeNaoTratar = "Phishing é o vetor inicial mais comum; filtro básico não detém mensagem direcionada.",
                Recomendacao = "Habilitar proteção avançada com análise de link e de anexo.",
                Frameworks = ["CIS 9.7", "NIST PR.AT-1"] },
            new() { Codigo = "email.spf", Texto = "SPF está publicado e correto?", Peso = 2, Opcoes = OpcoesControle,
                RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "SPF ausente ou incorreto no domínio",
                SeNaoTratar = "Sem SPF, qualquer um envia e-mail em nome do domínio da empresa.",
                Recomendacao = "Publicar SPF cobrindo todos os remetentes legítimos.",
                Frameworks = ["CIS 9.2"] },
            new() { Codigo = "email.dkim", Texto = "DKIM está configurado?", Peso = 2, Opcoes = OpcoesControle,
                RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Mensagens sem assinatura DKIM",
                SeNaoTratar = "Sem assinatura, o destinatário não tem como distinguir a mensagem legítima da falsificada." },
            new() { Codigo = "email.dmarc", Texto = "DMARC está publicado em política de rejeição?", Peso = 3, Opcoes = OpcoesControle,
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "DMARC fora de política de rejeição",
                SeNaoTratar = "Sem DMARC em rejeição, fraude de cobrança em nome da empresa chega à caixa do cliente dela.",
                Recomendacao = "Evoluir o DMARC de monitoramento para quarentena e depois rejeição.",
                Frameworks = ["CIS 9.2"] },
            new() { Codigo = "email.impersonation", Texto = "Há proteção contra falsificação de executivos?", Peso = 2, Opcoes = OpcoesControle,
                SeNaoTratar = "A fraude do falso diretor pedindo transferência depende exatamente da ausência desse controle." },
            new() { Codigo = "email.sandbox", Texto = "Anexos passam por análise em ambiente isolado?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "email.treinamento", Texto = "Os usuários recebem treinamento de conscientização?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Usuários sem treinamento de conscientização",
                SeNaoTratar = "Controle técnico não cobre o clique; sem treino, a taxa de clique em simulação costuma passar de 20%.",
                Recomendacao = "Programa contínuo, com simulação periódica e reforço para quem clica.",
                Frameworks = ["NIST PR.AT-1", "CIS 14.1", "ISO A.6.3"] },
            new() { Codigo = "email.simulacao", Texto = "São feitas campanhas simuladas de phishing?", Peso = 1,
                Opcoes = OpcoesControle, SomenteSe = new("email.treinamento", [Sim, Parcial]) },
        ],
    };

    // ── 6. Backup ────────────────────────────────────────────────────────────
    // O domínio que decide se um ransomware é um susto de dois dias ou o fim da empresa.

    public static DominioDeSeguranca Backup { get; } = new()
    {
        Codigo = "backup",
        Nome = "Backup e recuperação",
        Resumo = "O que separa um incidente grave de uma empresa que não volta.",
        Ordem = 6,
        Perguntas =
        [
            new() { Codigo = "backup.existe", Texto = "Existe backup dos sistemas críticos?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "Sistemas críticos sem backup",
                SeNaoTratar = "Sem backup, um ransomware bem-sucedido significa perda definitiva do dado — não há negociação que devolva o que não existe.",
                Recomendacao = "Implantar backup dos sistemas críticos com verificação de sucesso.",
                Frameworks = ["NIST PR.IP-4", "CIS 11.1", "ISO A.8.13"] },
            new() { Codigo = "backup.solucao", Texto = "Qual solução de backup?", Tipo = TipoDePergunta.Texto, Peso = 0,
                SomenteSe = new("backup.existe", [Sim, Parcial]) },
            new() { Codigo = "backup.offline", Texto = "Existe cópia offline ou fora do domínio?", Peso = 3,
                Opcoes = OpcoesControle, SomenteSe = new("backup.existe", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "Backup sem cópia offline ou fora do domínio",
                SeNaoTratar = "Ransomware moderno procura e apaga o backup antes de cifrar. Backup alcançável pela mesma credencial não é backup.",
                Recomendacao = "Manter uma cópia fora do domínio, sem credencial compartilhada com a rede.",
                Frameworks = ["CIS 11.4"] },
            new() { Codigo = "backup.imutavel", Texto = "Existe backup imutável?", Peso = 3,
                Opcoes = OpcoesControle, SomenteSe = new("backup.existe", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Backup sem retenção imutável",
                SeNaoTratar = "Sem imutabilidade, quem obtiver a credencial de backup apaga a cópia antes do ataque aparecer.",
                Recomendacao = "Ativar retenção imutável no destino do backup." },
            new() { Codigo = "backup.externo", Texto = "Existe cópia em outro local físico ou nuvem?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("backup.existe", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Backup sem cópia em outro local físico ou em nuvem",
                SeNaoTratar = "Incêndio, furto ou alagamento levam produção e backup juntos quando estão na mesma sala." },
            new() { Codigo = "backup.retencao", Texto = "Retenção em dias", Tipo = TipoDePergunta.Numero, Peso = 0,
                SomenteSe = new("backup.existe", [Sim, Parcial]) },
            new() { Codigo = "backup.rpo", Texto = "O RPO está definido (quanto de dado se aceita perder)?", Peso = 1,
                Opcoes = OpcoesControle, SomenteSe = new("backup.existe", [Sim, Parcial]) },
            new() { Codigo = "backup.rto", Texto = "O RTO está definido (em quanto tempo precisa voltar)?", Peso = 1,
                Opcoes = OpcoesControle, SomenteSe = new("backup.existe", [Sim, Parcial]) },
            new() { Codigo = "backup.teste", Texto = "A restauração é testada periodicamente?", Peso = 3,
                Opcoes = OpcoesControle, SomenteSe = new("backup.existe", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "Restauração de backup nunca testada",
                SeNaoTratar = "Backup nunca restaurado é uma hipótese, não um plano. A hora do incidente é o pior momento para descobrir que o arquivo não abre.",
                Recomendacao = "Testar restauração real ao menos trimestralmente e registrar o tempo gasto.",
                Frameworks = ["CIS 11.5", "ISO A.8.13"] },
            new() { Codigo = "backup.dr", Texto = "Existe plano de recuperação de desastre documentado?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem plano de recuperação de desastre documentado",
                SeNaoTratar = "Sem plano, a recuperação depende de quem estiver disponível lembrar a ordem certa, sob pressão." },
        ],
    };

    // ── 7. Infraestrutura ────────────────────────────────────────────────────

    public static DominioDeSeguranca Infraestrutura { get; } = new()
    {
        Codigo = "infra",
        Nome = "Servidores e infraestrutura",
        Resumo = "O que sustenta a operação e o que se sabe sobre ele.",
        Ordem = 7,
        Perguntas =
        [
            new() { Codigo = "infra.inventario", Texto = "Existe inventário atualizado de ativos?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem inventário atualizado de ativos",
                SeNaoTratar = "Não se protege o que não se sabe que existe. Toda falha de cobertura começa num ativo fora da lista.",
                Recomendacao = "Manter inventário vivo, alimentado por descoberta automática.",
                Frameworks = ["NIST ID.AM-1", "CIS 1.1", "ISO A.5.9"] },
            new() { Codigo = "infra.eol", Texto = "Existem sistemas fora de suporte do fabricante?", Peso = 3,
                Opcoes = OpcoesControle, RespostaBoaEhNao = true, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sistemas em produção fora do suporte do fabricante",
                Ajuda = "Windows Server 2012, Windows 7, versões antigas de banco. Aqui 'sim' é o problema.",
                SeNaoTratar = "Sistema fora de suporte não recebe mais correção: a falha descoberta amanhã fica aberta para sempre.",
                Recomendacao = "Levantar o parque fora de suporte, priorizar por exposição e planejar substituição." },
            new() { Codigo = "infra.hardening", Texto = "Os servidores seguem alguma linha de configuração segura?", Peso = 2, Opcoes = OpcoesControle },
            new() { Codigo = "infra.virtualizacao", Texto = "Plataforma de virtualização", Tipo = TipoDePergunta.Escolha, Peso = 0,
                Opcoes = ["VMware", "Hyper-V", "Proxmox", "Nutanix", "Só físico", "Outro"] },
            new() { Codigo = "infra.bancos", Texto = "Bancos de dados em uso", Tipo = TipoDePergunta.Multipla, Peso = 0,
                Opcoes = ["SQL Server", "Oracle", "PostgreSQL", "MySQL/MariaDB", "MongoDB", "Outro"] },
            new() { Codigo = "infra.monitoramento", Texto = "Existe monitoramento de disponibilidade?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "infra.acessofisico", Texto = "O acesso físico ao rack ou sala de servidores é controlado?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "infra.energia", Texto = "Existe proteção de energia (nobreak, gerador)?", Peso = 1, Opcoes = OpcoesControle },
        ],
    };

    // ── 8. Nuvem ─────────────────────────────────────────────────────────────

    public static DominioDeSeguranca Nuvem { get; } = new()
    {
        Codigo = "cloud",
        Nome = "Nuvem",
        Resumo = "O ambiente que cresce sem passar pelo perímetro.",
        Ordem = 8,
        Perguntas =
        [
            new() { Codigo = "cloud.usa", Texto = "A empresa utiliza nuvem pública?", Peso = 0, Opcoes = OpcoesControle },
            new() { Codigo = "cloud.quais", Texto = "Quais provedores?", Tipo = TipoDePergunta.Multipla, Peso = 0,
                SomenteSe = new("cloud.usa", [Sim, Parcial]),
                Opcoes = ["Azure", "AWS", "Google Cloud", "Oracle Cloud", "Outro"] },
            new() { Codigo = "cloud.mfa", Texto = "As contas administrativas da nuvem têm MFA?", Peso = 3,
                Opcoes = OpcoesControle, SomenteSe = new("cloud.usa", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "Contas administrativas de nuvem sem MFA",
                SeNaoTratar = "Conta administrativa de nuvem sem MFA dá controle total do ambiente a quem obtiver a senha — inclusive para apagar tudo.",
                Recomendacao = "Exigir MFA em todas as contas privilegiadas do provedor.",
                Frameworks = ["CIS 6.5"] },
            new() { Codigo = "cloud.exposicao", Texto = "Há conferência de recursos expostos publicamente?", Peso = 3,
                Opcoes = OpcoesControle, SomenteSe = new("cloud.usa", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Recursos de nuvem publicados sem conferência do que está exposto",
                SeNaoTratar = "Bucket ou banco aberto por engano é uma das causas mais frequentes de vazamento — e não depende de invasão.",
                Recomendacao = "Revisar periodicamente o que está público e ativar alerta de mudança." },
            new() { Codigo = "cloud.logs", Texto = "O log de auditoria do provedor está ativo e retido?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("cloud.usa", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Log de auditoria do provedor inativo ou sem retenção",
                SeNaoTratar = "Sem log do provedor não há como saber o que foi alterado, por quem, nem desfazer com segurança." },
            new() { Codigo = "cloud.cspm", Texto = "Existe verificação contínua de configuração (CSPM)?", Peso = 1,
                Opcoes = OpcoesControle, SomenteSe = new("cloud.usa", [Sim, Parcial]) },
            new() { Codigo = "cloud.segredos", Texto = "Segredos e chaves ficam em cofre?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("cloud.usa", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Segredos e chaves fora de cofre",
                SeNaoTratar = "Chave em arquivo de configuração ou repositório vaza junto com o código e costuma sobreviver por anos." },
            new() { Codigo = "cloud.backup", Texto = "Os dados em nuvem também têm backup próprio?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("cloud.usa", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Dados em nuvem sem backup próprio",
                Ajuda = "Microsoft 365 e Google Workspace NÃO fazem backup no sentido de recuperação — o modelo é de responsabilidade compartilhada.",
                SeNaoTratar = "Exclusão acidental ou maliciosa em SaaS é definitiva depois da janela de lixeira do provedor." },
        ],
    };

    // ── 9. Vulnerabilidades ──────────────────────────────────────────────────

    public static DominioDeSeguranca Vulnerabilidades { get; } = new()
    {
        Codigo = "vulnerabilidade",
        Nome = "Vulnerabilidades",
        Resumo = "Saber onde estão as brechas antes de quem procura por elas.",
        Ordem = 9,
        Perguntas =
        [
            new() { Codigo = "vuln.gestao", Texto = "Existe processo de gestão de vulnerabilidades?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem processo de gestão de vulnerabilidades",
                SeNaoTratar = "Sem varredura, a empresa descobre a falha pelo incidente.",
                Recomendacao = "Varredura recorrente com prioridade por exposição e prazo de correção acordado.",
                Frameworks = ["NIST ID.RA-1", "CIS 7.1", "ISO A.8.8"] },
            new() { Codigo = "vuln.ferramenta", Texto = "Qual ferramenta de varredura?", Tipo = TipoDePergunta.Texto, Peso = 0,
                SomenteSe = new("vuln.gestao", [Sim, Parcial]) },
            new() { Codigo = "vuln.frequencia", Texto = "Com que frequência?", Tipo = TipoDePergunta.Escolha, Peso = 0,
                SomenteSe = new("vuln.gestao", [Sim, Parcial]),
                Opcoes = ["Contínua", "Mensal", "Trimestral", "Anual", "Só quando pedem"] },
            new() { Codigo = "vuln.sla", Texto = "Existe prazo acordado para corrigir o que é crítico?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("vuln.gestao", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Correção do que é crítico sem prazo acordado",
                SeNaoTratar = "Relatório de vulnerabilidade sem prazo de correção vira arquivo: o achado envelhece junto com o risco." },
            new() { Codigo = "vuln.externa", Texto = "A superfície externa é verificada (o que se vê da internet)?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Superfície externa sem verificação recorrente",
                SeNaoTratar = "É exatamente por essa superfície que um ataque não direcionado começa.",
                Recomendacao = "Varredura externa recorrente — é o que o L'Okta IA já faz de forma autônoma." },
            new() { Codigo = "vuln.pentest", Texto = "Já foi feito teste de intrusão?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "vuln.pentest.quando", Texto = "Quando foi o último?", Tipo = TipoDePergunta.Texto, Peso = 0,
                SomenteSe = new("vuln.pentest", [Sim, Parcial]) },
        ],
    };

    // ── 10. Monitoramento ────────────────────────────────────────────────────

    public static DominioDeSeguranca Monitoramento { get; } = new()
    {
        Codigo = "monitoramento",
        Nome = "Monitoramento e detecção",
        Resumo = "Se algo acontecer às duas da manhã, alguém fica sabendo?",
        Ordem = 10,
        Perguntas =
        [
            new() { Codigo = "mon.siem", Texto = "Existe SIEM ou centralização de logs?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem SIEM ou centralização de logs",
                SeNaoTratar = "Sem correlação central, cada ferramenta enxerga um pedaço e ninguém enxerga o ataque inteiro.",
                Recomendacao = "Centralizar os eventos das ferramentas existentes — sem trocá-las.",
                Frameworks = ["NIST DE.AE-3", "CIS 8.9", "ISO A.8.15"] },
            new() { Codigo = "mon.siem.qual", Texto = "Qual solução?", Tipo = TipoDePergunta.Escolha, Peso = 0,
                SomenteSe = new("mon.siem", [Sim, Parcial]),
                Opcoes = ["Microsoft Sentinel", "Splunk", "Elastic", "Wazuh", "QRadar", "OpenSearch", "Graylog", "Outro"] },
            new() { Codigo = "mon.fontes", Texto = "As principais fontes enviam log ao SIEM?", Peso = 3,
                Opcoes = OpcoesControle, SomenteSe = new("mon.siem", [Sim, Parcial]),
                RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Fontes principais sem enviar log ao SIEM",
                Ajuda = "Firewall, endpoint, diretório, nuvem, e-mail.",
                SeNaoTratar = "SIEM sem fonte é painel vazio com custo de licença — e dá a sensação de cobertura que não existe." },
            new() { Codigo = "mon.retencao", Texto = "Os logs são retidos por prazo definido?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("mon.siem", [Sim, Parcial]) },
            new() { Codigo = "mon.soc", Texto = "Existe alguém monitorando (SOC próprio ou contratado)?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem ninguém monitorando os alertas",
                SeNaoTratar = "Detecção sem quem responda apenas adia a descoberta até o horário comercial seguinte.",
                Recomendacao = "Contratar triagem gerenciada, ao menos para os alertas de maior severidade." },
            new() { Codigo = "mon.soc.horario", Texto = "Em que regime?", Tipo = TipoDePergunta.Escolha, Peso = 0,
                SomenteSe = new("mon.soc", [Sim, Parcial]),
                Opcoes = ["8x5", "12x7", "24x7", "Sob demanda"] },
            new() { Codigo = "mon.regras", Texto = "Existem regras de correlação ajustadas ao ambiente?", Peso = 1,
                Opcoes = OpcoesControle, SomenteSe = new("mon.siem", [Sim, Parcial]) },
            new() { Codigo = "mon.ti", Texto = "Há inteligência de ameaças alimentando a detecção?", Peso = 1,
                Opcoes = OpcoesControle, SomenteSe = new("mon.siem", [Sim, Parcial]) },
        ],
    };

    // ── 11. Resposta a incidentes ────────────────────────────────────────────

    public static DominioDeSeguranca RespostaAIncidentes { get; } = new()
    {
        Codigo = "resposta",
        Nome = "Resposta a incidentes",
        Resumo = "O que acontece na primeira hora — decidido antes, não durante.",
        Ordem = 11,
        Perguntas =
        [
            new() { Codigo = "resp.plano", Texto = "Existe plano de resposta a incidentes?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem plano de resposta a incidentes",
                SeNaoTratar = "Sem plano, a primeira hora se gasta decidindo quem decide — e é a hora que mais importa.",
                Recomendacao = "Documentar acionamento, papéis, contatos e critérios de escalada.",
                Frameworks = ["NIST RS.RP-1", "CIS 17.1", "ISO A.5.24"] },
            new() { Codigo = "resp.responsavel", Texto = "Há responsável designado?", Peso = 2, Opcoes = OpcoesControle },
            new() { Codigo = "resp.ransomware", Texto = "Existe procedimento específico para ransomware?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem procedimento específico para ransomware",
                SeNaoTratar = "É o cenário mais provável e o de decisão mais difícil sob pressão." },
            new() { Codigo = "resp.vazamento", Texto = "Existe procedimento para vazamento de dados?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem procedimento para vazamento de dados pessoais",
                Ajuda = "A LGPD exige comunicação à ANPD e aos titulares em prazo razoável.",
                SeNaoTratar = "Sem procedimento, o prazo legal de comunicação passa enquanto a empresa decide o que fazer.",
                Frameworks = ["LGPD art. 48"] },
            new() { Codigo = "resp.comunicacao", Texto = "Existe plano de comunicação (interna, clientes, imprensa)?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "resp.juridico", Texto = "Jurídico e encarregado de dados participam do fluxo?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "resp.simulacao", Texto = "Já houve simulação ou exercício de mesa?", Peso = 1, Opcoes = OpcoesControle },
        ],
    };

    // ── 12. Governança e LGPD ────────────────────────────────────────────────

    public static DominioDeSeguranca Governanca { get; } = new()
    {
        Codigo = "governanca",
        Nome = "Governança e LGPD",
        Resumo = "O que sustenta a segurança quando a pessoa que a fazia sai da empresa.",
        Ordem = 12,
        Perguntas =
        [
            new() { Codigo = "gov.encarregado", Texto = "Existe encarregado de dados (DPO) nomeado?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Encarregado de dados (DPO) não nomeado",
                SeNaoTratar = "A LGPD exige a indicação e a divulgação do contato do encarregado.",
                Frameworks = ["LGPD art. 41"] },
            new() { Codigo = "gov.politicas", Texto = "Existem políticas de segurança aprovadas e divulgadas?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Sem políticas de segurança aprovadas e divulgadas",
                SeNaoTratar = "Sem política aprovada, não há base para exigir comportamento nem para aplicar consequência." },
            new() { Codigo = "gov.inventariodados", Texto = "Existe inventário de dados pessoais tratados?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Sem inventário dos dados pessoais tratados",
                SeNaoTratar = "Sem saber onde o dado pessoal está, não há como atender pedido de titular nem dimensionar um vazamento.",
                Recomendacao = "Levantar quais dados existem, onde ficam, por quanto tempo e com quem são compartilhados.",
                Frameworks = ["LGPD art. 37", "ISO A.5.34"] },
            new() { Codigo = "gov.basesLegais", Texto = "As bases legais de cada tratamento estão definidas?", Peso = 2,
                Opcoes = OpcoesControle, SomenteSe = new("gov.inventariodados", [Sim, Parcial]) },
            new() { Codigo = "gov.titulares", Texto = "Existe canal para pedidos de titulares?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Sem canal para pedidos de titulares",
                SeNaoTratar = "O titular tem direito de acesso e exclusão; sem canal, o pedido chega como reclamação na ANPD.",
                Frameworks = ["LGPD art. 18"] },
            new() { Codigo = "gov.retencao", Texto = "Existe política de retenção e descarte?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "gov.classificacao", Texto = "A informação é classificada por sensibilidade?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "gov.treinamento", Texto = "Há treinamento periódico em privacidade e segurança?", Peso = 2, Opcoes = OpcoesControle },
            new() { Codigo = "gov.auditoria", Texto = "Existe auditoria ou revisão independente?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "gov.seguro", Texto = "Existe seguro cibernético contratado?", Peso = 0, Opcoes = OpcoesControle,
                Ajuda = "Não é controle de segurança, mas define exigências que costumam pautar o plano de ação." },
        ],
    };

    // ── 13. Terceiros ────────────────────────────────────────────────────────

    public static DominioDeSeguranca Terceiros { get; } = new()
    {
        Codigo = "terceiros",
        Nome = "Fornecedores e terceiros",
        Resumo = "O risco que entra pela porta de quem já tem a chave.",
        Ordem = 13,
        Perguntas =
        [
            new() { Codigo = "ter.quantos", Texto = "Quantos fornecedores possuem acesso aos sistemas?", Tipo = TipoDePergunta.Numero, Peso = 0 },
            new() { Codigo = "ter.avaliacao", Texto = "Existe avaliação de risco de fornecedores?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Fornecedores com acesso sem avaliação de risco",
                SeNaoTratar = "O comprometimento de um fornecedor com acesso é caminho de invasão que não depende de falha interna.",
                Frameworks = ["NIST ID.SC-2", "ISO A.5.19"] },
            new() { Codigo = "ter.contrato", Texto = "Os contratos trazem exigências de segurança e LGPD?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Contratos sem exigências de segurança e LGPD",
                SeNaoTratar = "Sem cláusula, não há dever contratual de avisar a empresa quando o próprio fornecedor for atacado.",
                Frameworks = ["LGPD art. 39"] },
            new() { Codigo = "ter.acessoremoto", Texto = "O acesso remoto de terceiros é controlado e temporário?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Acesso remoto de terceiros sem controle nem prazo",
                SeNaoTratar = "VPN permanente de fornecedor costuma ser o acesso mais antigo e menos revisado do ambiente.",
                Recomendacao = "Conceder acesso por janela, com conta nominal e registro de sessão." },
            new() { Codigo = "ter.revisao", Texto = "Esses acessos são revisados periodicamente?", Peso = 2, Opcoes = OpcoesControle },
            new() { Codigo = "ter.apis", Texto = "Existem integrações por API com terceiros?", Peso = 1, Opcoes = OpcoesControle },
        ],
    };

    // ── 14. DevSecOps (condicional) ──────────────────────────────────────────

    public static DominioDeSeguranca DevSecOps { get; } = new()
    {
        Codigo = "devsecops",
        Nome = "Desenvolvimento",
        Resumo = "Segurança no software que a própria empresa escreve.",
        Ordem = 14,
        SomenteSe = new("perfil.desenvolve", [Sim, Parcial]),
        Perguntas =
        [
            new() { Codigo = "dev.repositorio", Texto = "O código fica em repositório com controle de acesso?", Peso = 2, Opcoes = OpcoesControle },
            new() { Codigo = "dev.ambientes", Texto = "Os ambientes de desenvolvimento e produção são separados?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Desenvolvimento e produção sem separação de ambientes",
                SeNaoTratar = "Ambiente de teste com dado real e proteção menor é uma das formas mais comuns de vazamento.",
                Frameworks = ["ISO A.8.31"] },
            new() { Codigo = "dev.segredos", Texto = "Há varredura de segredos no código?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Código sem varredura de segredos",
                SeNaoTratar = "Chave commitada permanece no histórico mesmo depois de removida do arquivo." },
            new() { Codigo = "dev.sast", Texto = "Existe análise estática de código (SAST)?", Peso = 1, Opcoes = OpcoesControle },
            new() { Codigo = "dev.dependencias", Texto = "As dependências são verificadas (SCA)?", Peso = 2,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Dependências de terceiros sem verificação (SCA)",
                SeNaoTratar = "A maior parte do código de uma aplicação moderna é de terceiros e envelhece sem aviso." },
            new() { Codigo = "dev.revisao", Texto = "Há revisão de código antes de publicar?", Peso = 2, Opcoes = OpcoesControle },
            new() { Codigo = "dev.dadosprod", Texto = "Dado de produção é usado em teste?", Peso = 2, Opcoes = OpcoesControle,
                RespostaBoaEhNao = true, RiscoSeNao = GravidadeRisco.Medio,
                TituloDoRisco = "Dado de produção em uso no ambiente de teste",
                Ajuda = "Aqui 'sim' é o problema.",
                SeNaoTratar = "Dado pessoal em ambiente de teste amplia a exposição sem base legal correspondente.",
                Frameworks = ["LGPD art. 46"] },
        ],
    };

    // ── 15. OT / IoT (condicional) ───────────────────────────────────────────

    public static DominioDeSeguranca OtIot { get; } = new()
    {
        Codigo = "ot",
        Nome = "OT e dispositivos conectados",
        Resumo = "Equipamento que não aceita agente, não reinicia e não pode parar.",
        Ordem = 15,
        SomenteSe = new("perfil.ot", [Sim, Parcial]),
        Perguntas =
        [
            new() { Codigo = "ot.inventario", Texto = "Esses dispositivos estão inventariados?", Peso = 2, Opcoes = OpcoesControle },
            new() { Codigo = "ot.segmentacao", Texto = "Estão em rede separada da rede administrativa?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "OT/IoT na mesma rede da administrativa",
                SeNaoTratar = "Equipamento industrial ou médico na mesma rede do escritório transforma um incidente de TI em parada de operação — ou em risco à segurança física.",
                Recomendacao = "Segmentar a rede de OT/IoT com regras explícitas de comunicação.",
                Frameworks = ["NIST PR.AC-5", "CIS 12.2"] },
            new() { Codigo = "ot.senhapadrao", Texto = "As senhas padrão de fábrica foram trocadas?", Peso = 3,
                Opcoes = OpcoesControle, RiscoSeNao = GravidadeRisco.Alto,
                TituloDoRisco = "Dispositivos com senha padrão de fábrica",
                SeNaoTratar = "Câmeras e controladores com senha padrão são varridos automaticamente na internet inteira, o dia todo." },
            new() { Codigo = "ot.exposicao", Texto = "Algum deles é acessível pela internet?", Peso = 3, Opcoes = OpcoesControle,
                RespostaBoaEhNao = true, RiscoSeNao = GravidadeRisco.Critico,
                TituloDoRisco = "Dispositivos OT/IoT acessíveis pela internet",
                Ajuda = "Aqui 'sim' é o problema.",
                SeNaoTratar = "Dispositivo exposto sem atualização é a porta de entrada mais barata que existe." },
            new() { Codigo = "ot.atualizacao", Texto = "Existe processo de atualização de firmware?", Peso = 1, Opcoes = OpcoesControle },
        ],
    };
}
