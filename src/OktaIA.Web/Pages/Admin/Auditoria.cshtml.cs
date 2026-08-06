using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class AuditoriaModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public AuditoriaModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<AdminAuditLog> Linhas { get; private set; } = Array.Empty<AdminAuditLog>();

    public async Task OnGetAsync()
    {
        Linhas = await _db.AdminAuditLogs.OrderByDescending(a => a.CriadoEm).Take(100).ToListAsync();
    }

    public static string CorAcao(string acao) => acao switch
    {
        "LOGIN" => "#00E0A4",
        "UPDATE" => "#4D9BFF",
        "CREATE" => "#4D9BFF",
        "EXPORT" => "#8A7BFF",
        "CONTAIN" => "#FF8A3D",
        "DENY" => "#FF3B5C",
        "READ" => "#6B7F9B",
        "DELETE" => "#FF3B5C",
        "SUSPEND" => "#FF8A3D",
        _ => "#4D9BFF",
    };
}
