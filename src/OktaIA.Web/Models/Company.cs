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
}
