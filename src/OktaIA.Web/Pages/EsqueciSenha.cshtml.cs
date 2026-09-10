using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

/// <summary>
/// Primeira metade do "esqueci minha senha": pede o e-mail e manda o link. Página anônima — quem
/// esqueceu a senha não consegue entrar. A segunda metade é /RedefinirSenha.
/// </summary>
public class EsqueciSenhaModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEnviadorEmail _email;
    private readonly ILogger<EsqueciSenhaModel> _log;

    public EsqueciSenhaModel(UserManager<ApplicationUser> userManager, IEnviadorEmail email, ILogger<EsqueciSenhaModel> log)
    {
        _userManager = userManager;
        _email = email;
        _log = log;
    }

    [BindProperty]
    public string? Email { get; set; }

    public bool Enviado { get; private set; }
    public string? Erro { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            Erro = "Informe o e-mail da conta.";
            return Page();
        }

        // ⚠️ A resposta é a MESMA existindo ou não a conta — dizer "não há conta com esse e-mail"
        // entrega a lista de clientes a quem quiser testar endereços. A única exceção é o envio
        // falhar com a conta existindo: aí a pessoa precisa saber, senão espera para sempre.
        var user = await _userManager.FindByEmailAsync(Email.Trim());
        if (user is null || user.Email is null)
        {
            _log.LogInformation("Recuperação de senha pedida para e-mail sem conta.");
            Enviado = true;
            return Page();
        }

        // O token do Identity é Base64 comum e vem com "+" e "/" — dentro de uma URL, "+" vira
        // ESPAÇO e o link chega quebrado. Base64Url na ida, decode na volta.
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var tokenUrl = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = Url.Page("/RedefinirSenha", null, new { email = user.Email, token = tokenUrl }, Request.Scheme)!;

        var (ok, motivo) = await _email.EnviarAsync(user.Email, "Redefinir sua senha — L'okta IA", CorpoDoEmail(user, link));
        if (!ok)
        {
            _log.LogError("Falha ao enviar o link de senha para {Para}: {Motivo}", user.Email, motivo);
            Erro = "Não conseguimos enviar o e-mail agora. Tente de novo em alguns minutos ou fale com o suporte em info@loktaia.com.";
            return Page();
        }

        Enviado = true;
        return Page();
    }

    private static string CorpoDoEmail(ApplicationUser user, string link)
    {
        var nome = HtmlEncoder.Default.Encode(user.NomeCompleto ?? user.Email ?? "");
        var href = HtmlEncoder.Default.Encode(link);
        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:15px;color:#1b2431;line-height:1.6;max-width:560px">
              <p style="font-size:12px;letter-spacing:.14em;text-transform:uppercase;color:#5A7191;margin:0 0 18px">L'okta IA · Cyber Security &amp; AI</p>
              <p>Olá, {nome}.</p>
              <p>Recebemos um pedido para redefinir a senha da sua conta na plataforma <strong>L'okta IA</strong>.</p>
              <p style="margin:26px 0">
                <a href="{href}" style="background:#2A6FD6;color:#fff;padding:13px 26px;border-radius:8px;text-decoration:none;display:inline-block;font-weight:bold">Criar uma senha nova</a>
              </p>
              <p style="font-size:13px;color:#5A7191">Se o botão não abrir, copie este endereço no navegador:<br>{href}</p>
              <p style="font-size:13px;color:#5A7191">O link vale por 24 horas e só pode ser usado uma vez.</p>
              <p style="font-size:13px;color:#5A7191">Se não foi você que pediu, não precisa fazer nada — sua senha atual continua valendo.</p>
            </div>
            """;
    }
}
