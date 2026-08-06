using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages;

[Authorize]
public class TrocarSenhaModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public TrocarSenhaModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Sucesso { get; private set; }
    public string? Erro { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null)
        {
            return NotFound();
        }

        var resultado = await _userManager.ChangePasswordAsync(usuario, Input.SenhaAtual, Input.NovaSenha);
        if (!resultado.Succeeded)
        {
            Erro = resultado.Errors.Any(e => e.Code.Contains("Password"))
                ? "Senha atual incorreta ou nova senha não atende aos requisitos mínimos (8+ caracteres)."
                : string.Join(" ", resultado.Errors.Select(e => e.Description));
            return Page();
        }

        await _signInManager.RefreshSignInAsync(usuario);
        Sucesso = true;
        Input = new InputModel();
        return Page();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Informe a senha atual.")]
        [DataType(DataType.Password)]
        public string SenhaAtual { get; set; } = "";

        [Required(ErrorMessage = "Informe a nova senha.")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "A nova senha precisa ter ao menos 8 caracteres.")]
        public string NovaSenha { get; set; } = "";

        [Required(ErrorMessage = "Confirme a nova senha.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NovaSenha), ErrorMessage = "As senhas não coincidem.")]
        public string ConfirmarSenha { get; set; } = "";
    }
}
