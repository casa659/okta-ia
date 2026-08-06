using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages;

[Authorize]
public class AutenticadorModel : PageModel
{
    private const string Issuer = "okta-ia.com";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AutenticadorModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool JaAtivado { get; private set; }
    public string SharedKey { get; private set; } = string.Empty;
    public string AuthenticatorUri { get; private set; } = string.Empty;
    public string[]? RecoveryCodes { get; private set; }
    public string? Erro { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null)
        {
            return NotFound();
        }

        JaAtivado = await _userManager.GetTwoFactorEnabledAsync(usuario);
        if (!JaAtivado)
        {
            await CarregarChaveAsync(usuario);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAtivarAsync()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await CarregarChaveAsync(usuario);
            return Page();
        }

        var codigo = Input.Codigo.Replace(" ", string.Empty).Replace("-", string.Empty);
        var valido = await _userManager.VerifyTwoFactorTokenAsync(
            usuario, _userManager.Options.Tokens.AuthenticatorTokenProvider, codigo);

        if (!valido)
        {
            Erro = "Código inválido. Confira o horário do seu celular e tente novamente.";
            await CarregarChaveAsync(usuario);
            return Page();
        }

        await _userManager.SetTwoFactorEnabledAsync(usuario, true);
        var codigosRecuperacao = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, 10);

        JaAtivado = true;
        RecoveryCodes = codigosRecuperacao?.ToArray();
        return Page();
    }

    public async Task<IActionResult> OnPostDesativarAsync()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null)
        {
            return NotFound();
        }

        await _userManager.SetTwoFactorEnabledAsync(usuario, false);
        await _userManager.ResetAuthenticatorKeyAsync(usuario);
        await _signInManager.RefreshSignInAsync(usuario);

        return RedirectToPage();
    }

    private async Task CarregarChaveAsync(ApplicationUser usuario)
    {
        var chave = await _userManager.GetAuthenticatorKeyAsync(usuario);
        if (string.IsNullOrEmpty(chave))
        {
            await _userManager.ResetAuthenticatorKeyAsync(usuario);
            chave = await _userManager.GetAuthenticatorKeyAsync(usuario);
        }

        SharedKey = FormatarChave(chave!);
        AuthenticatorUri = GerarUriAutenticador(usuario.Email!, chave!);
    }

    private static string FormatarChave(string chave)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < chave.Length; i += 4)
        {
            builder.Append(chave.AsSpan(i, Math.Min(4, chave.Length - i))).Append(' ');
        }

        return builder.ToString().TrimEnd();
    }

    private static string GerarUriAutenticador(string email, string chave)
    {
        return string.Format(
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            UrlEncoder.Default.Encode(Issuer),
            UrlEncoder.Default.Encode(email),
            chave);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Informe o código.")]
        [StringLength(7, MinimumLength = 6)]
        public string Codigo { get; set; } = string.Empty;
    }
}
