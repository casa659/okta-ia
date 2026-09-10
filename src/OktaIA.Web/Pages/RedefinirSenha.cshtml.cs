using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages;

/// <summary>
/// Segunda metade do "esqueci minha senha": a pessoa chega aqui pelo link do e-mail.
///
/// Página anônima de propósito — quem esqueceu a senha não consegue entrar para trocá-la. Quem
/// prova a identidade aqui é o TOKEN do link, não a sessão. A troca de senha de quem já está
/// logado é /TrocarSenha, e lá o Identity exige a senha atual.
/// </summary>
public class RedefinirSenhaModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RedefinirSenhaModel> _log;

    public RedefinirSenhaModel(UserManager<ApplicationUser> userManager, ILogger<RedefinirSenhaModel> log)
    {
        _userManager = userManager;
        _log = log;
    }

    // `string?` de propósito: o binder converte string vazia em nulo e reclamaria de uma
    // obrigatoriedade que ninguém pediu (ver feedback_binding_string_nao_anulavel).
    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty]
    public string? Senha { get; set; }

    [BindProperty]
    public string? Confirmacao { get; set; }

    public bool LinkValido { get; set; }
    public bool Concluido { get; set; }
    public string? Erro { get; set; }

    public void OnGet()
    {
        LinkValido = !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Token);
        if (!LinkValido)
        {
            Erro = "Este link está incompleto.";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LinkValido = !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Token);
        if (!LinkValido)
        {
            Erro = "Este link está incompleto.";
            return Page();
        }

        if (string.IsNullOrEmpty(Senha) || string.IsNullOrEmpty(Confirmacao))
        {
            Erro = "Preencha a senha nova nos dois campos.";
            return Page();
        }

        if (Senha != Confirmacao)
        {
            Erro = "As senhas não conferem.";
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Email!);
        if (user is null)
        {
            // Mesma resposta de um token vencido: dizer "essa conta não existe" aqui devolveria,
            // pelo link, a informação que a tela do pedido se recusou a dar.
            _log.LogWarning("Redefinição de senha com e-mail sem conta.");
            LinkValido = false;
            Erro = "Este link não vale mais.";
            return Page();
        }

        // A volta do Base64Url feito no envio. Link truncado ou colado pela metade estoura aqui
        // como FormatException — e isso é "link inválido", não erro de servidor.
        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Token!));
        }
        catch (FormatException)
        {
            LinkValido = false;
            Erro = "Este link chegou incompleto — provavelmente foi cortado pelo programa de e-mail.";
            return Page();
        }

        var resultado = await _userManager.ResetPasswordAsync(user, token, Senha);
        if (!resultado.Succeeded)
        {
            // Token recusado = o link venceu ou já foi usado; o formulário some. Senha fraca = o
            // link continua bom, e o formulário FICA para a pessoa tentar de novo.
            if (resultado.Errors.Any(e => e.Code == "InvalidToken"))
            {
                LinkValido = false;
                Erro = "Este link não vale mais — ele expira em 24 horas e só pode ser usado uma vez.";
                return Page();
            }

            Erro = string.Join(" ", resultado.Errors.Select(Traduzir));
            return Page();
        }

        // Invalida as sessões abertas em outros aparelhos: quem redefine a senha porque desconfia
        // de invasão precisa que o invasor caia junto. (O Identity relê o carimbo a cada 30 min.)
        await _userManager.UpdateSecurityStampAsync(user);

        // Bloqueio por tentativas erradas não deve sobreviver a uma senha nova legítima.
        if (await _userManager.IsLockedOutAsync(user))
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
        }

        _log.LogInformation("Senha redefinida pelo link de e-mail para o usuário {Id}", user.Id);
        Concluido = true;
        LinkValido = true;
        return Page();
    }

    private static string Traduzir(IdentityError e) => e.Code switch
    {
        "PasswordTooShort" => "A senha precisa ter pelo menos 8 caracteres.",
        "PasswordRequiresDigit" => "A senha precisa ter pelo menos um número.",
        "PasswordRequiresLower" => "A senha precisa ter pelo menos uma letra minúscula.",
        "PasswordRequiresUpper" => "A senha precisa ter pelo menos uma letra maiúscula.",
        _ => e.Description,
    };
}
