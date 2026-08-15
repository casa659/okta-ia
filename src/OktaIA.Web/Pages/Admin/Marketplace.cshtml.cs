using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Data;
using OktaIA.Web.Services;
using OktaIA.Web.Services.Integracoes;

namespace OktaIA.Web.Pages.Admin;

/// <summary>
/// Catálogo de fabricantes. O conteúdo dos cartões ainda é vitrine (lista fixa em AdminCatalog, com
/// avaliação e contagem de instalações fabricadas), mas os dois botões de roteiro são reais: geram
/// PDF a partir do <see cref="CatalogoDeRoteiros"/>.
///
/// Só o Wazuh tem adaptador. Os PDFs dos demais existem para preparar o levantamento junto ao
/// cliente e saem carimbados como "conector ainda não implementado" — sem isso, alguém enviaria a um
/// cliente o passo a passo de uma integração que não conectamos.
/// </summary>
[Authorize]
public class MarketplaceModel : PageModel
{
    private readonly RoteiroPdfService _pdf;
    private readonly ApplicationDbContext _db;

    public MarketplaceModel(RoteiroPdfService pdf, ApplicationDbContext db)
    {
        _pdf = pdf;
        _db = db;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnGetRoteiroClienteAsync(string fabricante)
        => await GerarAsync(fabricante, cliente: true);

    public async Task<IActionResult> OnGetRoteiroInternoAsync(string fabricante)
        => await GerarAsync(fabricante, cliente: false);

    private async Task<IActionResult> GerarAsync(string fabricante, bool cliente)
    {
        var roteiro = CatalogoDeRoteiros.PorFabricante(fabricante ?? "");
        if (roteiro is null)
        {
            return NotFound($"Não há roteiro cadastrado para '{fabricante}'.");
        }

        // A empresa selecionada entra no cabeçalho do documento — o do cliente costuma ir por
        // e-mail, e chegar sem o nome dele parece modelo genérico.
        var empresa = await TenantResolver.ResolverAtualAsync(HttpContext, _db);

        var bytes = cliente
            ? _pdf.GerarParaCliente(roteiro, empresa?.Nome)
            : _pdf.GerarParaTecnico(roteiro, empresa?.Nome);

        var sufixo = cliente ? "cliente" : "interno";
        var nome = $"roteiro-{roteiro.Slug}-{sufixo}.pdf";
        return File(bytes, "application/pdf", nome);
    }
}
