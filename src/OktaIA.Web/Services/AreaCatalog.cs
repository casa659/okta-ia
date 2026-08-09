namespace OktaIA.Web.Services;

// Catálogo das "áreas" que a matriz de Perfis e permissões consegue liberar/bloquear por perfil —
// uma entrada por página protegida (10 do console SOC + 16 do console Admin, mesmo inventário de
// [Authorize] espalhado pelas PageModels). AreaKey é a chave estável gravada em RolePermission;
// AdminCatalog.NavGroups já tinha Id+Page prontos pros 16 itens do Admin, só reaproveitei.
public static class AreaCatalog
{
    public record Area(string Key, string Label, string Page, string Grupo);

    public static readonly IReadOnlyList<Area> Soc = new[]
    {
        new Area("soc.dashboard", "Dashboard", "/Dashboard", "Centro de Operações · SOC"),
        new Area("soc.empresas", "Empresas", "/Empresas", "Centro de Operações · SOC"),
        new Area("soc.ativos", "Ativos", "/Ativos", "Centro de Operações · SOC"),
        new Area("soc.twin", "Digital Twin", "/DigitalTwin", "Centro de Operações · SOC"),
        new Area("soc.vulnerabilidades", "Vulnerabilidades", "/Vulnerabilidades", "Centro de Operações · SOC"),
        new Area("soc.alertas", "Alertas", "/Alertas", "Centro de Operações · SOC"),
        new Area("soc.incidentes", "Incidentes", "/Incidentes", "Centro de Operações · SOC"),
        new Area("soc.siem", "SIEM", "/Siem", "Centro de Operações · SOC"),
        new Area("soc.ia", "IA", "/Ia", "Centro de Operações · SOC"),
        new Area("soc.relatorios", "Relatórios", "/Relatorios", "Centro de Operações · SOC"),
        new Area("soc.financeiro", "Financeiro", "/Financeiro", "Centro de Operações · SOC"),
    };

    public static readonly IReadOnlyList<Area> Admin = AdminCatalog.NavGroups
        .SelectMany(g => g.Items.Select(i => new Area($"admin.{i.Id}", i.Label, i.Page, g.Title)))
        .ToList();

    public static readonly IReadOnlyList<Area> Todas = Soc.Concat(Admin).ToList();

    public static readonly IReadOnlyDictionary<string, Area> PorPagina =
        Todas.ToDictionary(a => a.Page, a => a, StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyDictionary<string, Area> PorChave =
        Todas.ToDictionary(a => a.Key, a => a);
}
