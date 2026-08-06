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
public class EmpresasModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AdminAuditService _audit;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmpresasModel(ApplicationDbContext db, AdminAuditService audit, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _audit = audit;
        _userManager = userManager;
    }

    [BindProperty]
    public NovaEmpresaInput Input { get; set; } = new();

    [BindProperty]
    public EditarEmpresaInput EditInput { get; set; } = new();

    public bool MostrarForm { get; set; }
    public IReadOnlyList<Company> Empresas { get; private set; } = Array.Empty<Company>();

    public async Task OnGetAsync()
    {
        Empresas = await _db.Companies.OrderBy(c => c.Id).ToListAsync();
    }

    public async Task<IActionResult> OnPostCadastrarAsync()
    {
        MostrarForm = true;

        // ModelState.Clear() + TryValidateModel escopado: Input e EditInput são ambos
        // [BindProperty] na mesma página — sem isso, um required vazio do EditInput (não
        // enviado neste form) reprovaria o Cadastrar (mesmo bug já visto em Admin/Usuarios).
        ModelState.Clear();
        TryValidateModel(Input, nameof(Input));

        if (!string.IsNullOrWhiteSpace(Input.Cnpj) && !CnpjValidator.IsValid(Input.Cnpj))
        {
            ModelState.AddModelError(nameof(Input.Cnpj), "CNPJ inválido — confira os dígitos.");
        }

        if (!ModelState.IsValid)
        {
            Empresas = await _db.Companies.OrderBy(c => c.Id).ToListAsync();
            return Page();
        }

        var empresa = new Company
        {
            Nome = Input.Nome,
            SetorPt = Input.Setor,
            SetorEn = Input.Setor,
            Plano = Input.Plano,
            StatusContrato = "trial",
            Cnpj = string.IsNullOrWhiteSpace(Input.Cnpj) ? null : CnpjValidator.Formatar(Input.Cnpj),
            Dominio = ExtrairHostnameOuNulo(Input.Dominio),
        };
        _db.Companies.Add(empresa);
        await _db.SaveChangesAsync();

        var usuario = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("CREATE", $"Organização {empresa.Nome} provisionada", usuario?.Email ?? "admin");

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditarAsync()
    {
        ModelState.Clear();
        TryValidateModel(EditInput, nameof(EditInput));

        if (!string.IsNullOrWhiteSpace(EditInput.Cnpj) && !CnpjValidator.IsValid(EditInput.Cnpj))
        {
            ModelState.AddModelError(nameof(EditInput.Cnpj), "CNPJ inválido — confira os dígitos.");
        }

        if (!ModelState.IsValid)
        {
            Empresas = await _db.Companies.OrderBy(c => c.Id).ToListAsync();
            return Page();
        }

        var empresa = await _db.Companies.FindAsync(EditInput.Id);
        if (empresa is null)
        {
            Empresas = await _db.Companies.OrderBy(c => c.Id).ToListAsync();
            return Page();
        }

        empresa.Nome = EditInput.Nome;
        empresa.SetorPt = EditInput.Setor;
        empresa.SetorEn = EditInput.Setor;
        empresa.Plano = EditInput.Plano;
        empresa.Cnpj = string.IsNullOrWhiteSpace(EditInput.Cnpj) ? null : CnpjValidator.Formatar(EditInput.Cnpj);
        empresa.Dominio = ExtrairHostnameOuNulo(EditInput.Dominio);
        await _db.SaveChangesAsync();

        var usuario = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("UPDATE", $"Organização {empresa.Nome} editada", usuario?.Email ?? "admin");

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAtivarAsync(int id)
    {
        var empresa = await _db.Companies.FindAsync(id);
        if (empresa is not null)
        {
            empresa.Ativo = true;
            await _db.SaveChangesAsync();

            var usuario = await _userManager.GetUserAsync(User);
            await _audit.RegistrarAsync("UPDATE", $"Organização {empresa.Nome} reativada", usuario?.Email ?? "admin");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDesativarAsync(int id)
    {
        var empresa = await _db.Companies.FindAsync(id);
        if (empresa is null)
        {
            return RedirectToPage();
        }

        // Sem isso, desativar a última empresa ativa zera o seletor de organização (TenantResolver
        // usa .FirstOrDefault() sobre as ativas) e derruba todo o console SOC, que assume sempre
        // haver um tenant selecionado (ver Ativos/Vulnerabilidades/Incidentes/Siem).
        if (empresa.Ativo && await _db.Companies.CountAsync(c => c.Ativo) <= 1)
        {
            Empresas = await _db.Companies.OrderBy(c => c.Id).ToListAsync();
            ModelState.AddModelError(string.Empty, "Não é possível desativar a última organização ativa — o console SOC precisa de pelo menos uma.");
            return Page();
        }

        empresa.Ativo = false;
        await _db.SaveChangesAsync();

        var usuario = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("UPDATE", $"Organização {empresa.Nome} desativada", usuario?.Email ?? "admin");

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExcluirAsync(int id)
    {
        var empresa = await _db.Companies.FindAsync(id);
        if (empresa is null)
        {
            return RedirectToPage();
        }

        if (empresa.Ativo && await _db.Companies.CountAsync(c => c.Ativo) <= 1)
        {
            Empresas = await _db.Companies.OrderBy(c => c.Id).ToListAsync();
            ModelState.AddModelError(string.Empty, "Não é possível excluir a última organização ativa — o console SOC precisa de pelo menos uma.");
            return Page();
        }

        var nome = empresa.Nome;
        _db.Companies.Remove(empresa);
        await _db.SaveChangesAsync();

        var usuario = await _userManager.GetUserAsync(User);
        await _audit.RegistrarAsync("DELETE", $"Organização {nome} excluída", usuario?.Email ?? "admin");

        return RedirectToPage();
    }

    // Aceita "exemplo.com.br" ou "https://exemplo.com.br/" (mesmo comportamento tolerante do
    // campo de domínio em /Ativos) — guarda só o hostname, pra prefill funcionar direto no
    // campo de domínio do "Adicionar ativo real".
    private static string? ExtrairHostnameOuNulo(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            return null;
        }

        var bruto = entrada.Trim();
        var comEsquema = bruto.Contains("://", StringComparison.Ordinal) ? bruto : $"https://{bruto}";
        var host = Uri.TryCreate(comEsquema, UriKind.Absolute, out var uri) ? uri.Host : bruto.TrimEnd('/');
        return host.ToLowerInvariant();
    }

    public class NovaEmpresaInput
    {
        [Required(ErrorMessage = "Informe o nome.")]
        public string Nome { get; set; } = "";

        [Required(ErrorMessage = "Informe o setor.")]
        public string Setor { get; set; } = "";

        [Required(ErrorMessage = "Escolha o plano.")]
        public string Plano { get; set; } = "Business";

        public string? Cnpj { get; set; }

        public string? Dominio { get; set; }
    }

    public class EditarEmpresaInput
    {
        [Required]
        public int Id { get; set; }

        [Required(ErrorMessage = "Informe o nome.")]
        public string Nome { get; set; } = "";

        [Required(ErrorMessage = "Informe o setor.")]
        public string Setor { get; set; } = "";

        [Required(ErrorMessage = "Escolha o plano.")]
        public string Plano { get; set; } = "Business";

        public string? Cnpj { get; set; }

        public string? Dominio { get; set; }
    }
}
