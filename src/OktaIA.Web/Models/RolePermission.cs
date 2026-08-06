using Microsoft.AspNetCore.Identity;

namespace OktaIA.Web.Models;

// Concede a um perfil (IdentityRole) acesso a uma "área" da plataforma — chave estável definida
// em Services/AreaCatalog.cs, não o nome da página (que pode mudar). "Admin" nunca precisa de
// linha aqui: tem acesso total sempre, verificado antes de qualquer consulta a esta tabela
// (ver AreaPermissionFilter) — evita alguém se autobloquear do próprio painel de permissões.
public class RolePermission
{
    public int Id { get; set; }

    public required string RoleId { get; set; }
    public IdentityRole? Role { get; set; }

    public required string AreaKey { get; set; }
}
