using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages;

public class ContatoModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public ContatoModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public ContactRequest Input { get; set; } = new();

    [BindProperty]
    public CanalForm CanalInput { get; set; } = new();

    public bool Enviado { get; private set; }
    public List<ContactChannel> Channels { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await CarregarCanaisAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.NomeCompleto) || string.IsNullOrWhiteSpace(Input.Email))
        {
            ModelState.AddModelError(string.Empty, "Informe ao menos nome e e-mail.");
            await CarregarCanaisAsync();
            return Page();
        }

        Input.Id = 0;
        Input.CriadoEm = DateTime.UtcNow;
        _db.ContactRequests.Add(Input);
        await _db.SaveChangesAsync();

        Enviado = true;
        Input = new ContactRequest();
        await CarregarCanaisAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSalvarCanalAsync()
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage();
        }

        // ModelState.Clear() + TryValidateModel escopado: Input (form público) e CanalInput
        // (admin) são ambos [BindProperty] na mesma página — sem isso, um required vazio de um
        // reprovaria o outro (mesmo bug já visto em Admin/Usuarios e Admin/Empresas).
        ModelState.Clear();
        if (!TryValidateModel(CanalInput, nameof(CanalInput)))
        {
            await CarregarCanaisAsync();
            return Page();
        }

        var cor = string.IsNullOrWhiteSpace(CanalInput.Cor) ? "#4D9BFF" : CanalInput.Cor;

        if (CanalInput.Id is int id && id > 0)
        {
            var canal = await _db.ContactChannels.FindAsync(id);
            if (canal is not null)
            {
                canal.Chave = CanalInput.Chave;
                canal.Cor = cor;
                canal.Valor = CanalInput.Valor;
                canal.Descricao = CanalInput.Descricao;
            }
        }
        else
        {
            var proximaOrdem = await _db.ContactChannels.CountAsync();
            _db.ContactChannels.Add(new ContactChannel
            {
                Chave = CanalInput.Chave,
                Cor = cor,
                Valor = CanalInput.Valor,
                Descricao = CanalInput.Descricao,
                Ordem = proximaOrdem,
            });
        }

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExcluirCanalAsync(int id)
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage();
        }

        var canal = await _db.ContactChannels.FindAsync(id);
        if (canal is not null)
        {
            _db.ContactChannels.Remove(canal);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    private async Task CarregarCanaisAsync()
    {
        Channels = await _db.ContactChannels.OrderBy(c => c.Ordem).ToListAsync();
    }

    public class CanalForm
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Informe o rótulo.")]
        public string Chave { get; set; } = "";

        public string Cor { get; set; } = "#4D9BFF";

        [Required(ErrorMessage = "Informe o valor.")]
        public string Valor { get; set; } = "";

        [Required(ErrorMessage = "Informe a descrição.")]
        public string Descricao { get; set; } = "";
    }
}
