using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class PerfisModel : PageModel
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly AdminAuditService _audit;

    // "Admin"/"Analista" são checados por string em [Authorize(Roles="...")] espalhado pelo
    // código (todo o console Admin depende de "Admin" existir com esse nome exato) — renomear ou
    // excluir qualquer um dos dois quebraria login/autorização de forma silenciosa e permanente.
    private static readonly HashSet<string> PerfisProtegidos = new(StringComparer.OrdinalIgnoreCase) { "Admin", "Analista" };

    public PerfisModel(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, ApplicationDbContext db, AdminAuditService audit)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _db = db;
        _audit = audit;
    }

    [BindProperty]
    public NovoPerfilInput Input { get; set; } = new();

    [BindProperty]
    public EditarPerfilInput EditInput { get; set; } = new();

    public bool MostrarForm { get; set; }
    public List<PerfilView> Perfis { get; private set; } = new();
    public HashSet<string> PermissoesAtuais { get; private set; } = new();

    public record PerfilView(string Id, string Nome, int UsuariosCount, bool Protegido);

    public static string ChaveGrade(string roleId, string areaKey) => $"{roleId}|{areaKey}";

    public async Task OnGetAsync()
    {
        await CarregarAsync();
    }

    public async Task<IActionResult> OnPostCriarAsync()
    {
        MostrarForm = true;
        // ModelState.Clear() + TryValidateModel escopado: Input e EditInput são ambos
        // [BindProperty] na mesma página — sem isso, um required vazio de um reprova o outro
        // (mesmo bug de Admin/Usuarios e Admin/Empresas).
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            await CarregarAsync();
            return Page();
        }

        var nome = Input.Nome.Trim();
        if (await _roleManager.RoleExistsAsync(nome))
        {
            ModelState.AddModelError(nameof(Input.Nome), "Já existe um perfil com esse nome.");
            await CarregarAsync();
            return Page();
        }

        await _roleManager.CreateAsync(new IdentityRole(nome));

        var autor = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("CREATE", $"Perfil {nome} criado", autor?.Email ?? "admin");

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditarAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(EditInput, nameof(EditInput)))
        {
            await CarregarAsync();
            return Page();
        }

        var role = await _roleManager.FindByIdAsync(EditInput.Id);
        if (role?.Name is null)
        {
            await CarregarAsync();
            return Page();
        }

        if (PerfisProtegidos.Contains(role.Name))
        {
            ModelState.AddModelError(string.Empty, $"O perfil \"{role.Name}\" é do sistema e não pode ser renomeado.");
            await CarregarAsync();
            return Page();
        }

        var novoNome = EditInput.Nome.Trim();
        if (!string.Equals(novoNome, role.Name, StringComparison.OrdinalIgnoreCase) && await _roleManager.RoleExistsAsync(novoNome))
        {
            ModelState.AddModelError(nameof(EditInput.Nome), "Já existe um perfil com esse nome.");
            await CarregarAsync();
            return Page();
        }

        var nomeAntigo = role.Name;
        role.Name = novoNome;
        role.NormalizedName = _roleManager.NormalizeKey(novoNome);
        await _roleManager.UpdateAsync(role);

        var autor = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("UPDATE", $"Perfil {nomeAntigo} renomeado para {novoNome}", autor?.Email ?? "admin");

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExcluirAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role?.Name is null)
        {
            return RedirectToPage();
        }

        if (PerfisProtegidos.Contains(role.Name))
        {
            await CarregarAsync();
            ModelState.AddModelError(string.Empty, $"O perfil \"{role.Name}\" é do sistema e não pode ser excluído.");
            return Page();
        }

        var nome = role.Name;
        await _roleManager.DeleteAsync(role);

        var autor = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("DELETE", $"Perfil {nome} excluído", autor?.Email ?? "admin");

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSalvarPermissoesAsync(List<string>? selecionados)
    {
        selecionados ??= new();

        // Perfis "editáveis" excluem Admin de propósito: a coluna dele na grade é só leitura
        // (sempre tem acesso total, ver AreaPermissionFilter) — mesmo que alguém falsifique o
        // POST incluindo o RoleId do Admin, o filtro abaixo descarta porque ele não está na lista.
        var roleIdsEditaveis = await _roleManager.Roles
            .Where(r => r.Name != "Admin")
            .Select(r => r.Id)
            .ToListAsync();
        var roleIdsEditaveisSet = roleIdsEditaveis.ToHashSet();

        var existentes = await _db.RolePermissions.Where(rp => roleIdsEditaveisSet.Contains(rp.RoleId)).ToListAsync();
        _db.RolePermissions.RemoveRange(existentes);

        foreach (var par in selecionados)
        {
            var partes = par.Split('|', 2);
            if (partes.Length != 2)
            {
                continue;
            }

            var (roleId, areaKey) = (partes[0], partes[1]);
            if (!roleIdsEditaveisSet.Contains(roleId) || !AreaCatalog.PorChave.ContainsKey(areaKey))
            {
                continue;
            }

            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, AreaKey = areaKey });
        }

        await _db.SaveChangesAsync();

        var autor = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("UPDATE", "Permissões de perfis atualizadas", autor?.Email ?? "admin");

        return RedirectToPage();
    }

    private async Task CarregarAsync()
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
        var lista = new List<PerfilView>();
        foreach (var r in roles)
        {
            if (r.Name is null)
            {
                continue;
            }

            var usuarios = await _userManager.GetUsersInRoleAsync(r.Name);
            lista.Add(new PerfilView(r.Id, r.Name, usuarios.Count, PerfisProtegidos.Contains(r.Name)));
        }

        Perfis = lista;

        PermissoesAtuais = (await _db.RolePermissions.Select(rp => new { rp.RoleId, rp.AreaKey }).ToListAsync())
            .Select(rp => ChaveGrade(rp.RoleId, rp.AreaKey))
            .ToHashSet();
    }

    public class NovoPerfilInput
    {
        [Required(ErrorMessage = "Informe o nome do perfil.")]
        public string Nome { get; set; } = "";
    }

    public class EditarPerfilInput
    {
        [Required]
        public string Id { get; set; } = "";

        [Required(ErrorMessage = "Informe o nome do perfil.")]
        public string Nome { get; set; } = "";
    }
}
