using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Os slugs que TÊM adaptador escrito na plataforma. Hoje: só o Wazuh.
    ///
    /// ⚠️ Sai do <see cref="RegistroDeConectores"/>, que é a lista de implementações registradas —
    /// não de uma constante ao lado. Escrever "wazuh" à mão aqui faria a tela continuar dizendo a
    /// mesma coisa no dia em que o segundo adaptador entrasse (ou saísse).
    /// </summary>
    public HashSet<string> ComAdaptador { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Slugs de fato instalados por alguma empresa — da tabela `Conectores`.</summary>
    public HashSet<string> Instalados { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Quantas empresas usam cada conector. Substitui as "2.1k instalações" inventadas.</summary>
    public Dictionary<string, int> EmpresasPorSlug { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public async Task OnGetAsync([FromServices] RegistroDeConectores registro)
    {
        ComAdaptador = registro.Disponiveis
            .Select(c => c.Slug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var instalados = await _db.Conectores.AsNoTracking()
            .Select(c => new { c.Slug, c.CompanyId })
            .ToListAsync();

        Instalados = instalados.Select(c => c.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

        EmpresasPorSlug = instalados
            .GroupBy(c => c.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CompanyId).Distinct().Count(),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O estado REAL de um item do catálogo, na ordem em que ele importa para quem opera.
    ///
    /// ⚠️ "Instalado" exige as duas coisas: adaptador escrito E alguém usando. Um sem o outro é
    /// meia verdade — e foi meia verdade que fez a tela mostrar quatro conectores instalados que
    /// nunca existiram.
    /// </summary>
    public (string Rotulo, string Cor) Situacao(string slug)
    {
        var temAdaptador = ComAdaptador.Contains(slug);

        if (temAdaptador && Instalados.Contains(slug)) { return ("instalado", "#00E0A4"); }
        if (temAdaptador) { return ("pronto para instalar", "#4D9BFF"); }
        return ("sem conector", "#7A8FAB");
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
