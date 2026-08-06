using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class UsuariosModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AdminAuditService _audit;

    public UsuariosModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AdminAuditService audit)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _audit = audit;
    }

    [BindProperty]
    public ConvidarInput Input { get; set; } = new();

    [BindProperty]
    public EditarInput EditInput { get; set; } = new();

    public bool MostrarForm { get; set; }
    public string? SenhaTemporaria { get; set; }
    public string? UsuarioAtualId { get; private set; }
    public List<string> PapeisDisponiveis { get; private set; } = new();
    public List<(ApplicationUser Usuario, List<string> Papeis, bool Ativo)> Usuarios { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await CarregarAsync();
    }

    public async Task<IActionResult> OnPostConvidarAsync()
    {
        MostrarForm = true;
        // ModelState.Clear() + TryValidateModel escopado: como Input e EditInput são ambos
        // [BindProperty] na mesma página, o bind automático valida os dois em TODO POST — sem
        // isso, um required vazio do EditInput (não enviado neste form) reprovaria o Convidar.
        ModelState.Clear();
        var valido = TryValidateModel(Input, nameof(Input));
        if (Input.Papeis is null || Input.Papeis.Count == 0)
        {
            ModelState.AddModelError(nameof(Input.Papeis), "Selecione ao menos um perfil.");
            valido = false;
        }

        if (!valido)
        {
            await CarregarAsync();
            return Page();
        }

        var senha = GerarSenhaTemporaria();
        var novoUsuario = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = true,
            NomeCompleto = Input.NomeCompleto,
            Iniciais = string.Concat(Input.NomeCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0])).ToUpperInvariant(),
        };

        var resultado = await _userManager.CreateAsync(novoUsuario, senha);
        if (!resultado.Succeeded)
        {
            foreach (var erro in resultado.Errors)
            {
                ModelState.AddModelError(string.Empty, erro.Description);
            }

            await CarregarAsync();
            return Page();
        }

        await _userManager.AddToRolesAsync(novoUsuario, Input.Papeis!);

        var autor = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("CREATE", $"Usuário {novoUsuario.Email} convidado como {string.Join("/", Input.Papeis!)}", autor?.Email ?? "admin");

        SenhaTemporaria = senha;
        await CarregarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostEditarAsync()
    {
        ModelState.Clear();
        var valido = TryValidateModel(EditInput, nameof(EditInput));
        if (EditInput.Papeis is null || EditInput.Papeis.Count == 0)
        {
            ModelState.AddModelError(nameof(EditInput.Papeis), "Selecione ao menos um perfil.");
            valido = false;
        }

        if (!valido)
        {
            await CarregarAsync();
            return Page();
        }

        var usuario = await _userManager.FindByIdAsync(EditInput.Id);
        if (usuario is null)
        {
            await CarregarAsync();
            return Page();
        }

        usuario.NomeCompleto = EditInput.NomeCompleto;
        usuario.Iniciais = string.Concat(EditInput.NomeCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0])).ToUpperInvariant();
        usuario.Email = EditInput.Email;
        usuario.UserName = EditInput.Email;
        usuario.NormalizedEmail = _userManager.NormalizeEmail(EditInput.Email);
        usuario.NormalizedUserName = _userManager.NormalizeName(EditInput.Email);

        var resultado = await _userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            foreach (var erro in resultado.Errors)
            {
                ModelState.AddModelError(string.Empty, erro.Description);
            }

            await CarregarAsync();
            return Page();
        }

        var papeisAtuais = await _userManager.GetRolesAsync(usuario);
        var remover = papeisAtuais.Except(EditInput.Papeis!).ToList();
        var adicionar = EditInput.Papeis!.Except(papeisAtuais).ToList();
        if (remover.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(usuario, remover);
        }

        if (adicionar.Count > 0)
        {
            await _userManager.AddToRolesAsync(usuario, adicionar);
        }

        var autor = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("UPDATE", $"Usuário {usuario.Email} editado (perfis: {string.Join("/", EditInput.Papeis!)})", autor?.Email ?? "admin");

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAtivarAsync(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is not null)
        {
            await _userManager.SetLockoutEndDateAsync(usuario, null);

            var autor = await _userManager.GetUserAsync(User);
            await _audit.RegistrarAsync("UPDATE", $"Usuário {usuario.Email} reativado", autor?.Email ?? "admin");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDesativarAsync(string id)
    {
        var usuarioAtual = await _userManager.GetUserAsync(User);
        if (id == usuarioAtual?.Id)
        {
            await CarregarAsync();
            ModelState.AddModelError(string.Empty, "Você não pode desativar a própria conta.");
            return Page();
        }

        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is not null)
        {
            await _userManager.SetLockoutEnabledAsync(usuario, true);
            await _userManager.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);

            await _audit.RegistrarAsync("UPDATE", $"Usuário {usuario.Email} desativado", usuarioAtual?.Email ?? "admin");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExcluirAsync(string id)
    {
        var usuarioAtual = await _userManager.GetUserAsync(User);
        if (id == usuarioAtual?.Id)
        {
            await CarregarAsync();
            ModelState.AddModelError(string.Empty, "Você não pode excluir a própria conta.");
            return Page();
        }

        var usuario = await _userManager.FindByIdAsync(id);
        if (usuario is not null)
        {
            var email = usuario.Email;
            await _userManager.DeleteAsync(usuario);
            await _audit.RegistrarAsync("DELETE", $"Usuário {email} excluído", usuarioAtual?.Email ?? "admin");
        }

        return RedirectToPage();
    }

    private async Task CarregarAsync()
    {
        var usuarioAtual = await _userManager.GetUserAsync(User);
        UsuarioAtualId = usuarioAtual?.Id;

        PapeisDisponiveis = await _roleManager.Roles
            .Where(r => r.Name != null)
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();

        var todos = _userManager.Users.ToList();
        var lista = new List<(ApplicationUser, List<string>, bool)>();
        foreach (var u in todos)
        {
            var papeis = (await _userManager.GetRolesAsync(u)).OrderBy(p => p).ToList();
            var ativo = !await _userManager.IsLockedOutAsync(u);
            lista.Add((u, papeis, ativo));
        }

        Usuarios = lista;
    }

    private static string GerarSenhaTemporaria()
    {
        var bytes = RandomNumberGenerator.GetBytes(9);
        return "Ok!" + Convert.ToBase64String(bytes).Replace("+", "8").Replace("/", "9").Replace("=", "").Substring(0, 10);
    }

    public class ConvidarInput
    {
        [Required(ErrorMessage = "Informe o nome.")]
        public string NomeCompleto { get; set; } = "";

        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string Email { get; set; } = "";

        public List<string> Papeis { get; set; } = new() { "Analista" };
    }

    public class EditarInput
    {
        [Required]
        public string Id { get; set; } = "";

        [Required(ErrorMessage = "Informe o nome.")]
        public string NomeCompleto { get; set; } = "";

        [Required(ErrorMessage = "Informe o e-mail.")]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string Email { get; set; } = "";

        public List<string> Papeis { get; set; } = new();
    }
}
