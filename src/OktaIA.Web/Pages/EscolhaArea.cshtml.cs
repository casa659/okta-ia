using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages;

[Authorize(Roles = "Admin")]
public class EscolhaAreaModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public EscolhaAreaModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public string NomeCompleto { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Iniciais { get; private set; } = "?";

    public async Task<IActionResult> OnGetAsync()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null)
        {
            return NotFound();
        }

        NomeCompleto = usuario.NomeCompleto ?? usuario.Email ?? "";
        Email = usuario.Email ?? "";
        Iniciais = usuario.Iniciais ?? "?";
        return Page();
    }
}
