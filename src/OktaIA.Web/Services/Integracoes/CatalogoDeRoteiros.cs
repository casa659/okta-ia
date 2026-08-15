namespace OktaIA.Web.Services.Integracoes;

/// <summary>
/// Roteiros por fabricante, cobrindo os itens do Marketplace.
///
/// REGRA DE HONESTIDADE DESTE ARQUIVO: descrevemos o que a integração exige — tipo de credencial,
/// onde o alerta mora, sentido da conexão, se há liberação de rede — porque isso depende do produto
/// do fabricante e é estável. NÃO inventamos sequência de cliques dentro do console de cada
/// fabricante: caminho de menu muda por versão, e um passo a passo falso num documento que vai ao
/// cliente destrói a confiança mais rápido do que a ausência dele. Onde o caminho exato importa, o
/// texto manda confirmar na documentação do fabricante.
///
/// Só `wazuh` está implementado. Os demais existem para preparar a conversa comercial e o
/// levantamento — e saem carimbados como não implementados.
/// </summary>
public static class CatalogoDeRoteiros
{
    private const string ConfiraDoc = "Confirme o caminho exato no console na documentação vigente do fabricante — ela muda entre versões.";

    private static readonly IReadOnlyList<string> PassosTecnicoPadraoLocal =
    [
        "Confirme que os IPs de saída da plataforma foram liberados no firewall do cliente — a lista completa está na página de Informações, e é preciso liberar todos, não só os ativos hoje.",
        "Em Conectores, selecione a empresa do cliente no seletor da tela.",
        "Instale o conector do fabricante e preencha o endereço do serviço e as credenciais que o cliente enviou. Apague qualquer valor que o navegador tenha preenchido sozinho.",
        "Clique em Testar conexão. Não avance enquanto não responder com sucesso.",
        "Sincronize uma vez e confira os alertas na tela de Alertas, com a mesma empresa selecionada.",
        "Sincronize uma segunda vez: precisa inserir zero. Se inserir de novo, é defeito — reporte.",
    ];

    private static readonly IReadOnlyList<string> PassosTecnicoPadraoNuvem =
    [
        "Confirme com o cliente que o consentimento foi concedido por alguém com poder de administrador no portal do fabricante.",
        "Em Conectores, selecione a empresa do cliente no seletor da tela.",
        "Instale o conector e preencha as credenciais de aplicação que o cliente enviou. Apague qualquer valor que o navegador tenha preenchido sozinho.",
        "Clique em Testar conexão. Não avance enquanto não responder com sucesso.",
        "Sincronize uma vez e confira os alertas na tela de Alertas, com a mesma empresa selecionada.",
        "Sincronize uma segunda vez: precisa inserir zero.",
    ];

    private static readonly IReadOnlyList<string> ObsNaoImplementado =
    [
        "Este conector AINDA NÃO está implementado na plataforma. Este documento serve ao levantamento junto ao cliente e à preparação comercial — não há, hoje, tela onde instalá-lo.",
        "Use-o para descobrir cedo se o ambiente do cliente comporta a integração; assim, quando o adaptador existir, a implantação é só execução.",
    ];

    public static readonly IReadOnlyList<RoteiroDeImplantacao> Todos =
    [
        new("wazuh", "Wazuh", "HIDS / SIEM", true, ModoDeAcesso.InstaladoNoCliente,
            "No Wazuh Indexer (um OpenSearch), normalmente na porta 9200 — NÃO na API do Manager, porta 55000, que serve agentes, integridade de arquivos e vulnerabilidades.",
            [
                "Crie no Wazuh Indexer um usuário de serviço dedicado a esta integração — não use a conta pessoal de um funcionário.",
                "Conceda a esse usuário permissão apenas de LEITURA, restrita aos índices de alerta (padrão `wazuh-alerts-*`).",
                "Identifique o endereço do Indexer: host e porta, no formato https://host:9200.",
                "Decida com sua equipe de redes como a plataforma alcançará esse endereço: publicação com lista de IPs permitidos, ou VPN.",
                "Se o Indexer usa certificado autoassinado (padrão em instalação nova), avalie substituí-lo por um certificado válido emitido para o mesmo nome de host.",
            ],
            [
                "Endereço do Wazuh Indexer (https://host:9200)",
                "Usuário e senha da conta de serviço somente-leitura",
                "Confirmação de que a liberação de rede foi feita",
            ],
            PassosTecnicoPadraoLocal,
            [
                "O erro de levantamento mais comum é pedir o endereço do Manager (55000). O alerta está no Indexer (9200).",
                "Instalação nova do Wazuh usa certificado autoassinado. Aceitar isso é exceção consciente, não estado permanente.",
                "A primeira carga traz os alertas dos últimos 7 dias, para o painel já nascer com contexto.",
            ]),

        new("defender", "Microsoft Defender", "EDR / XDR", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem da Microsoft, via API de segurança do Microsoft Graph — não há componente na rede do cliente.",
            [
                "Um administrador global do tenant precisa consentir o acesso de leitura da aplicação da L'okta IA aos alertas de segurança.",
                "O consentimento é concedido no portal de identidade da Microsoft (Entra ID), na área de aplicações. " + ConfiraDoc,
                "Nenhuma liberação de firewall é necessária: a conexão sai da nossa nuvem para a nuvem da Microsoft.",
            ],
            [
                "Identificador do tenant (directory/tenant id)",
                "Confirmação de que o consentimento foi concedido, e por quem",
            ],
            PassosTecnicoPadraoNuvem,
            ObsNaoImplementado),

        new("sentinel", "Microsoft Sentinel", "SIEM", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem da Microsoft, no workspace de log associado ao Sentinel.",
            [
                "Autorize a leitura do workspace do Sentinel por uma aplicação registrada, com papel somente-leitura.",
                "Identifique assinatura, grupo de recursos e nome do workspace. " + ConfiraDoc,
                "Nenhuma liberação de firewall é necessária.",
            ],
            ["Identificador do tenant", "Assinatura, grupo de recursos e workspace", "Confirmação do consentimento"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("crowdstrike", "CrowdStrike Falcon", "EDR", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem da CrowdStrike, via API do Falcon.",
            [
                "Crie no console do Falcon um cliente de API dedicado a esta integração.",
                "Conceda a esse cliente escopo apenas de LEITURA para detecções e alertas. " + ConfiraDoc,
                "Anote também a região da nuvem do Falcon em que sua conta está hospedada.",
            ],
            ["Client ID e Client Secret do cliente de API", "Região da nuvem do Falcon"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("fortigate", "Fortinet FortiGate", "Firewall", false, ModoDeAcesso.InstaladoNoCliente,
            "No próprio appliance, via API de administração — ou no FortiAnalyzer, quando os logs são centralizados nele.",
            [
                "Crie um usuário de API com perfil somente-leitura no FortiGate. " + ConfiraDoc,
                "Restrinja o acesso desse usuário aos IPs de origem da plataforma.",
                "Defina com a equipe de redes como a plataforma alcançará a interface de administração.",
                "Verifique se os logs de interesse ficam no próprio appliance ou num FortiAnalyzer — muda o endereço a informar.",
            ],
            ["Endereço e porta da API", "Token do usuário de API somente-leitura", "Confirmação da liberação de rede"],
            PassosTecnicoPadraoLocal, ObsNaoImplementado),

        new("paloalto", "Palo Alto Cortex XDR", "XDR", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem do Cortex XDR.",
            [
                "Gere no console do Cortex uma chave de API com perfil somente-leitura. " + ConfiraDoc,
                "Anote o identificador da chave e o endereço regional da sua instância (FQDN do tenant).",
            ],
            ["Chave de API e seu identificador", "Endereço regional da instância"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("sentinelone", "SentinelOne", "EDR", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem da SentinelOne, no console de gestão.",
            [
                "Gere um token de API vinculado a um usuário de serviço somente-leitura. " + ConfiraDoc,
                "Anote a URL do console de gestão da sua conta.",
            ],
            ["Token de API", "URL do console de gestão"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("sophos", "Sophos Central", "Endpoint", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem do Sophos Central.",
            [
                "Crie credenciais de API no Sophos Central com permissão somente-leitura. " + ConfiraDoc,
                "Anote o identificador do tenant.",
            ],
            ["Client ID e Client Secret", "Identificador do tenant"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("trendmicro", "Trend Micro Vision One", "XDR", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem do Vision One.",
            [
                "Gere uma chave de API com função somente-leitura. " + ConfiraDoc,
                "Anote a região da sua instância — o endereço da API varia por região.",
            ],
            ["Chave de API", "Região da instância"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("elastic", "Elastic Security", "SIEM", false, ModoDeAcesso.InstaladoNoCliente,
            "No cluster Elasticsearch, nos índices de alerta do Elastic Security. Pode ser auto-hospedado ou em nuvem.",
            [
                "Crie uma chave de API com permissão de leitura restrita aos índices de alerta. " + ConfiraDoc,
                "Informe o endereço do cluster.",
                "Se for auto-hospedado, defina com a equipe de redes como a plataforma o alcançará.",
            ],
            ["Endereço do cluster", "Chave de API somente-leitura", "Confirmação da liberação de rede, se auto-hospedado"],
            PassosTecnicoPadraoLocal, ObsNaoImplementado),

        new("splunk", "Splunk Enterprise", "SIEM", false, ModoDeAcesso.InstaladoNoCliente,
            "No Splunk, via API REST de busca. Auto-hospedado na maioria dos casos.",
            [
                "Crie um usuário de serviço com papel somente-leitura e acesso apenas aos índices de interesse. " + ConfiraDoc,
                "Informe o endereço da API de gestão.",
                "Defina com a equipe de redes como a plataforma alcançará esse endereço.",
            ],
            ["Endereço da API de gestão", "Usuário e senha (ou token) somente-leitura", "Quais índices consultar"],
            PassosTecnicoPadraoLocal, ObsNaoImplementado),

        new("qualys", "Qualys VMDR", "Vulnerability Mgmt", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem da Qualys. Traz vulnerabilidades, não alertas de detecção.",
            [
                "Crie um usuário com perfil somente-leitura para a API. " + ConfiraDoc,
                "Anote qual plataforma regional da Qualys sua conta usa — o endereço da API depende disso.",
            ],
            ["Usuário e senha da API", "Plataforma regional da conta"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("rapid7", "Rapid7 InsightVM", "Vulnerability Mgmt", false, ModoDeAcesso.NuvemDoFabricante,
            "Na plataforma Insight. Traz vulnerabilidades, não alertas de detecção.",
            [
                "Gere uma chave de API de organização com permissão de leitura. " + ConfiraDoc,
                "Anote a região da sua conta Insight.",
            ],
            ["Chave de API", "Região da conta"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("okta", "Okta Identity", "IAM", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem da Okta, no log de eventos do sistema — traz eventos de identidade e acesso.",
            [
                "Crie um token de API vinculado a um usuário de serviço com privilégio de leitura. " + ConfiraDoc,
                "Informe a URL da sua organização Okta.",
            ],
            ["URL da organização", "Token de API somente-leitura"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("proofpoint", "Proofpoint", "E-mail Security", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem da Proofpoint — eventos de ameaça em mensagens.",
            [
                "Gere as credenciais de serviço para a API de eventos. " + ConfiraDoc,
                "Confirme quais tipos de evento sua licença expõe pela API.",
            ],
            ["Credenciais de serviço da API", "Tipos de evento disponíveis na licença"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("zscaler", "Zscaler", "SASE / Proxy", false, ModoDeAcesso.NuvemDoFabricante,
            "Na nuvem da Zscaler — eventos de navegação e política.",
            [
                "Gere uma chave de API e um usuário de serviço somente-leitura. " + ConfiraDoc,
                "Anote a nuvem regional em que seu tenant está.",
            ],
            ["Chave de API e credenciais do usuário de serviço", "Nuvem regional do tenant"],
            PassosTecnicoPadraoNuvem, ObsNaoImplementado),

        new("pfsense", "pfSense", "Firewall", false, ModoDeAcesso.InstaladoNoCliente,
            "No próprio appliance. Depende de pacote adicional para expor API — confirme o que está instalado.",
            [
                "Verifique se o appliance tem pacote de API instalado e habilitado. " + ConfiraDoc,
                "Crie um usuário somente-leitura para a integração.",
                "Defina com a equipe de redes como a plataforma alcançará a interface.",
            ],
            ["Endereço e porta da interface", "Credenciais somente-leitura", "Qual pacote de API está em uso"],
            PassosTecnicoPadraoLocal, ObsNaoImplementado),

        new("suricata", "Suricata", "IDS / IPS", false, ModoDeAcesso.InstaladoNoCliente,
            "Nos arquivos de alerta gerados pelo sensor (formato EVE, em JSON) — normalmente coletados por um SIEM antes de chegarem a nós.",
            [
                "Identifique para onde os alertas do sensor são enviados: arquivo local, SIEM ou coletor.",
                "Se houver um SIEM no caminho, a integração provavelmente deve ser feita com ele, e não direto com o sensor.",
                "Defina com a equipe de redes como a plataforma alcançará a origem escolhida.",
            ],
            ["Para onde os alertas do sensor vão hoje", "Endereço da origem escolhida", "Credenciais somente-leitura"],
            PassosTecnicoPadraoLocal,
            [
                .. ObsNaoImplementado,
                "Suricata sozinho normalmente não expõe API: o caminho usual é integrar o SIEM que já recebe os alertas dele.",
            ]),
    ];

    /// <summary>O Marketplace lista por nome do fabricante, não por slug — daí esta busca.</summary>
    public static RoteiroDeImplantacao? PorFabricante(string nome) =>
        Todos.FirstOrDefault(r => string.Equals(r.Fabricante, nome, StringComparison.OrdinalIgnoreCase));

    public static RoteiroDeImplantacao? PorSlug(string slug) =>
        Todos.FirstOrDefault(r => string.Equals(r.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
