using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

/// <summary>
/// A postura de LGPD de UMA empresa, medida — e o relatório que sai dela.
///
/// ⚠️ COMEÇA PEDINDO A EMPRESA, e não mostrando a primeira da lista. Numa tela que fala de
/// obrigação legal e de multa, abrir já preenchida é o convite para alguém ler o número de um
/// cliente achando que é de outro. A escolha é um ato, e aqui ela precisa ser.
/// </summary>
[Authorize]
public class ConformidadeModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly PosturaLgpd _postura;

    public ConformidadeModel(ApplicationDbContext db, PosturaLgpd postura)
    {
        _db = db;
        _postura = postura;
    }

    [BindProperty(SupportsGet = true)] public int? Empresa { get; set; }

    public List<(int Id, string Nome, bool TemConector)> Empresas { get; private set; } = [];
    public PosturaLgpd.Resultado? Resultado { get; private set; }

    public async Task OnGetAsync()
    {
        var comConector = (await _db.Conectores.AsNoTracking()
            .Select(c => c.CompanyId).Distinct().ToListAsync()).ToHashSet();

        Empresas = (await _db.Companies.AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .Select(c => new { c.Id, c.Nome })
            .ToListAsync())
            // Empresa monitorada primeiro: é dela que existe algo a dizer. A que não tem conector
            // continua na lista de propósito — escolhê-la é como o gestor descobre que não há
            // medida técnica nenhuma ali, que é uma resposta legítima desta tela.
            .OrderByDescending(c => comConector.Contains(c.Id))
            .ThenBy(c => c.Nome)
            .Select(c => (c.Id, c.Nome, comConector.Contains(c.Id)))
            .ToList();

        if (Empresa is { } id)
        {
            Resultado = await _postura.DeAsync(id, HttpContext.RequestAborted);
        }
    }

    /// <summary>O relatório de postura, em PDF, para a empresa escolhida.</summary>
    public async Task<IActionResult> OnGetRelatorioAsync(int empresa,
        [FromServices] RelatorioLgpdPdfService pdf)
    {
        var r = await _postura.DeAsync(empresa, HttpContext.RequestAborted);

        // ⚠️ A MESMA TRAVA DA TELA, aqui também. Este endereço é adivinhável
        // (`?handler=Relatorio&empresa=13`), e trava que existe só na marcação é trava que
        // qualquer pessoa contorna digitando a URL. Sem conector, o PDF sairia com zeros em toda
        // parte, assinado pela nossa marca — um documento que afirma postura medida sobre uma
        // empresa em que nunca se mediu nada. Ver PosturaLgpd.Implantado.
        if (!r.Implantado)
        {
            return RedirectToPage("/Conformidade", new { empresa });
        }

        var bytes = pdf.Gerar(r);
        return File(bytes, "application/pdf", $"postura-lgpd-{Arquivo(r.Empresa)}.pdf");
    }

    private static string Arquivo(string nome)
    {
        var limpo = new string(nome.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (limpo.Contains("--")) { limpo = limpo.Replace("--", "-"); }
        return limpo.Trim('-');
    }
}
