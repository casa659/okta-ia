using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;

namespace OktaIA.Web.Services;

// Gate real por área — roda em toda página Razor (registrado global no Program.cs). "Admin"
// sempre passa (bypass antes de qualquer consulta, ver Models/RolePermission.cs). Páginas fora do
// AreaCatalog (Login, TrocarSenha, marketing, etc.) não são controladas por aqui — passam direto,
// continuam com seu [Authorize] normal decidindo autenticação.
public class AreaPermissionFilter : IAsyncPageFilter
{
    private readonly ApplicationDbContext _db;

    public AreaPermissionFilter(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || user.IsInRole("Admin"))
        {
            await next();
            return;
        }

        var path = ((PageModel)context.HandlerInstance).PageContext.ActionDescriptor.ViewEnginePath;
        if (!AreaCatalog.PorPagina.TryGetValue(path, out var area))
        {
            await next();
            return;
        }

        var papeis = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var permitido = await _db.RolePermissions
            .Join(_db.Roles, rp => rp.RoleId, r => r.Id, (rp, r) => new { rp.AreaKey, r.Name })
            .AnyAsync(x => x.AreaKey == area.Key && papeis.Contains(x.Name!));

        if (!permitido)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
