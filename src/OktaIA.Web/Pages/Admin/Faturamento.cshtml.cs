using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class FaturamentoModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public FaturamentoModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Company> EmpresasPorPlano { get; private set; } = Array.Empty<Company>();

    public async Task OnGetAsync()
    {
        EmpresasPorPlano = await _db.Companies.Where(c => c.Ativo).OrderBy(c => c.Plano).ToListAsync();
    }
}
