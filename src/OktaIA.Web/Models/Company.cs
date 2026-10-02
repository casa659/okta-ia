namespace OktaIA.Web.Models;

// Empresa gerida pelo MSSP (tenant) — visão consolidada no cabeçalho (seletor "ORGANIZAÇÃO") e
// detalhada no módulo Empresas. Setor/Plano ficam denormalizados em pt/en aqui (não é enum
// compartilhado) porque são só rótulos de exibição de dado semi-estático, sem regra de negócio
// própria — evita uma tabela de tradução pra algo que não muda.
public class Company
{
    public int Id { get; set; }

    public required string Nome { get; set; }
    public required string SetorPt { get; set; }
    public required string SetorEn { get; set; }
    public required string Plano { get; set; } // Business, Enterprise, Enterprise+, Gov, MSP

    public int ScoreRisco { get; set; } // 0-100, quanto maior pior (igual ao mockup)
    public int AtivosCount { get; set; }
    public int VulnsCount { get; set; }
    public int IncidentesCount { get; set; }
    public decimal UptimePercentual { get; set; }

    public bool Ativo { get; set; } = true;

    // Usados pelo console Admin (Empresas) — não existiam na Fase 1-6 do SOC, adicionados na
    // Fase 3 do console de administração.
    public string? Cnpj { get; set; }
    public string StatusContrato { get; set; } = "ativa"; // ativa, inadimplente, trial
    public int UsuariosCount { get; set; }

    // Domínio principal da empresa — usado pra sugerir/prefill o campo domínio ao adicionar um
    // ativo real em /Ativos, já vinculado a esta empresa. Sem regra de unicidade: uma empresa
    // pode não ter domínio, e nada impede o operador de trocar o valor sugerido.
    public string? Dominio { get; set; }

    // Empresa fictícia do seed de demonstração (Grupo Vector, Hospital Santa Clara, etc.), com
    // ativos, eventos, incidentes e CVEs inventados pra dar corpo às telas. Existe pra que a UI
    // possa AVISAR que aquele ambiente não é real: sem o rótulo, um prospect vê "vpn-sp01 sob
    // ataque de 3 ASNs russos" achando que é o ambiente dele — e a descoberta de que era enfeite
    // custa a confiança na plataforma inteira, que é o produto que se está vendendo.
    // Empresa criada pelo operador (Admin > Empresas) nasce com false.
    public bool Demo { get; set; }

    // ── Chave de leitura do monitoramento LGPD (02/10/2026) ──────────────────────────────
    //
    // Por que ela existe: o LinkEscola precisa PERGUNTAR a esta plataforma o que ela apurou —
    // quantos alertas, quantos tratados — para mostrar ao DPO da escola. Quem pergunta é máquina,
    // não navegador, então não há cookie: a autorização é esta chave, e só ela.
    //
    // 🔴 UMA CHAVE POR EMPRESA, e é ela que DIZ qual empresa é. O LinkEscola não manda o id junto,
    // de propósito: uma chamada que informa de quem são os dados que quer convida a trocar o
    // número e ler a empresa do vizinho. Com a chave sozinha, errar o id é impossível porque não
    // existe id para errar.
    //
    // 🔴 SÓ O HASH FICA GUARDADO. Esta plataforma nunca precisa LER a chave de volta — só conferir
    // a que chegou. Guardar o valor permitiria que um dump do banco entregasse acesso aos alertas
    // de todos os clientes de uma vez; guardar o hash não entrega nada. O preço é que ela aparece
    // UMA vez, na geração: perdida, gera-se outra. Esse preço é o certo.

    /// <summary>SHA-256 do valor em claro, em hexadecimal. Nulo = esta empresa não expõe leitura.</summary>
    public string? ChaveLgpdHash { get; set; }

    /// <summary>
    /// Os primeiros caracteres do valor, para a tela poder dizer QUAL chave está valendo.
    ///
    /// ⚠️ Serve para conferência, não para autenticação: 8 caracteres não abrem nada. Sem isto, a
    /// tela só saberia dizer "existe uma chave" — e quem administra duas escolas não teria como
    /// saber se a que está no LinkEscola é esta ou a anterior.
    /// </summary>
    public string? ChaveLgpdPrefixo { get; set; }

    public DateTimeOffset? ChaveLgpdEm { get; set; }
    public string? ChaveLgpdPor { get; set; }

    /// <summary>
    /// Último uso da chave.
    ///
    /// ⚠️ É o que responde "a integração do cliente está de pé?" sem precisar perguntar a ele.
    /// Uma chave gerada há um mês e nunca usada significa que o DPO não a colou do outro lado —
    /// e esse caso é indistinguível de "está tudo certo" sem esta coluna.
    /// </summary>
    public DateTimeOffset? ChaveLgpdUsoEm { get; set; }
}
