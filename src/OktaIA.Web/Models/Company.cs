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
}
