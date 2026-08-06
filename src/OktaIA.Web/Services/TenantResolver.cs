using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

// Resolve a organização (tenant) selecionada no seletor do header a partir do cookie
// "okia_tenant" — mesma lógica usada por _Layout.cshtml pra desenhar o botão, agora
// compartilhada com as páginas que precisam filtrar dado real por empresa.
public static class TenantResolver
{
    public const string CookieName = "okia_tenant";

    public static async Task<Company?> ResolverAtualAsync(HttpContext context, ApplicationDbContext db)
    {
        var tenants = await db.Companies.Where(c => c.Ativo).OrderBy(c => c.Id).ToListAsync();
        var cookie = context.Request.Cookies[CookieName];
        return tenants.FirstOrDefault(t => t.Id.ToString() == cookie) ?? tenants.FirstOrDefault();
    }
}
