using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages;

public class LoginWith2faModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginWith2faModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public bool RememberMe { get; set; }

    public string? Erro { get; private set; }
    public string Email { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var usuario = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (usuario is null)
        {
            return RedirectToPage("/Login");
        }

        Email = usuario.Email ?? string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var usuario = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (usuario is null)
        {
            return RedirectToPage("/Login");
        }

        Email = usuario.Email ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var codigo = Input.Codigo.Replace(" ", string.Empty).Replace("-", string.Empty);

        var resultado = Input.UsarCodigoRecuperacao
            ? await _signInManager.TwoFactorRecoveryCodeSignInAsync(codigo)
            : await _signInManager.TwoFactorAuthenticatorSignInAsync(codigo, RememberMe, Input.LembrarDispositivo);

        if (resultado.Succeeded)
        {
            if (await _userManager.IsInRoleAsync(usuario, "Admin"))
            {
                return RedirectToPage("/EscolhaArea");
            }

            return RedirectToPage("/Dashboard");
        }

        if (resultado.IsLockedOut)
        {
            Erro = "Conta temporariamente bloqueada por várias tentativas inválidas.";
            return Page();
        }

        Erro = "Código inválido.";
        return Page();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Informe o código.")]
        public string Codigo { get; set; } = string.Empty;

        public bool LembrarDispositivo { get; set; }

        public bool UsarCodigoRecuperacao { get; set; }
    }
}
