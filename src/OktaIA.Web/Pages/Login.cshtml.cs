using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? Erro { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Page();
        }

        var usuarioAtual = await _userManager.GetUserAsync(User);
        return await RedirecionarPosLoginAsync(usuarioAtual);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var resultado = await _signInManager.PasswordSignInAsync(Input.Email, Input.Senha, isPersistent: true, lockoutOnFailure: true);
        if (resultado.Succeeded)
        {
            var usuario = await _userManager.FindByEmailAsync(Input.Email);
            return await RedirecionarPosLoginAsync(usuario);
        }

        if (resultado.RequiresTwoFactor)
        {
            return RedirectToPage("/LoginWith2fa", new { RememberMe = true });
        }

        if (resultado.IsLockedOut)
        {
            var usuarioBloqueado = await _userManager.FindByEmailAsync(Input.Email);
            var lockoutEnd = usuarioBloqueado is not null ? await _userManager.GetLockoutEndDateAsync(usuarioBloqueado) : null;
            Erro = lockoutEnd > DateTimeOffset.UtcNow.AddYears(50)
                ? "Conta desativada. Fale com um administrador."
                : "Conta temporariamente bloqueada por várias tentativas inválidas.";
            return Page();
        }

        Erro = "E-mail ou senha inválidos.";
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return RedirectToPage("/Index");
    }

    // Quem tem a role Admin acumula acesso ao SOC e à Administração — passa pela tela de
    // escolha de área. Analista só tem SOC, vai direto pro Dashboard.
    private async Task<IActionResult> RedirecionarPosLoginAsync(ApplicationUser? usuario)
    {
        if (usuario is not null && await _userManager.IsInRoleAsync(usuario, "Admin"))
        {
            return RedirectToPage("/EscolhaArea");
        }

        return RedirectToPage("/Dashboard");
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Informe o e-mail.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Informe a senha.")]
        public string Senha { get; set; } = "";
    }
}
