using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class DigitalTwinModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DigitalTwinModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public record NodeView(int Id, string Nome, string Tipo, int Saude, string Cor, int VulnsCriticas, int VulnsAltas);
    public record LayerView(string LabelKey, List<NodeView> Nos);

    public List<(int Id, string Nome, string? Dominio)> EmpresasDisponiveis { get; private set; } = [];
    public int? EmpresaSelecionadaId { get; private set; }

    public List<LayerView> Layers { get; private set; } = [];
    public int TotalAtivos { get; private set; }
    public int SaudeMedia { get; private set; }
    public int TotalCriticos { get; private set; }
    public int TotalVulnsGraves { get; private set; }

    // Tipos livres já usados no inventário (comentário em Models/Asset.cs): API, WEB, VPN, DB,
    // MAIL, CI, FW. Qualquer tipo fora dessas listas cai na camada "Aplicações" — nunca some do mapa.
    private static readonly string[] TiposPerimetro = ["FW", "VPN"];
    private static readonly string[] TiposDados = ["DB"];

    public async Task OnGetAsync(int? empresa)
    {
        var empresaAtual = await ResolverEmpresaAsync(empresa);
        EmpresasDisponiveis = (await TenantResolver.EmpresasVisiveis(HttpContext, _db).OrderBy(c => c.Nome)
            .Select(c => new { c.Id, c.Nome, c.Dominio }).ToListAsync())
            .Select(c => (c.Id, c.Nome, c.Dominio)).ToList();
        EmpresaSelecionadaId = empresaAtual?.Id;

        if (empresaAtual is null)
        {
            return;
        }

        var ativos = await _db.Assets
            .Where(a => a.CompanyId == empresaAtual.Id)
            .OrderBy(a => a.Nome)
            .ToListAsync();

        string SaudeCor(int v) => v > 85 ? "#00E0A4" : v > 60 ? "#F5D547" : "#FF3B5C";

        NodeView ParaNode(Asset a) => new(a.Id, a.Nome, a.Tipo, a.Saude, SaudeCor(a.Saude), a.VulnsCriticas, a.VulnsAltas);

        var perimetro = ativos.Where(a => TiposPerimetro.Contains(a.Tipo, StringComparer.OrdinalIgnoreCase)).Select(ParaNode).ToList();
        var dados = ativos.Where(a => TiposDados.Contains(a.Tipo, StringComparer.OrdinalIgnoreCase)).Select(ParaNode).ToList();
        var apps = ativos.Where(a => !TiposPerimetro.Contains(a.Tipo, StringComparer.OrdinalIgnoreCase)
                                   && !TiposDados.Contains(a.Tipo, StringComparer.OrdinalIgnoreCase))
            .Select(ParaNode).ToList();

        Layers =
        [
            new LayerView("twinLayerPerimetro", perimetro),
            new LayerView("twinLayerApps", apps),
            new LayerView("twinLayerDados", dados),
        ];

        TotalAtivos = ativos.Count;
        SaudeMedia = ativos.Count > 0 ? (int)Math.Round(ativos.Average(a => a.Saude)) : 0;
        TotalCriticos = ativos.Count(a => a.Saude <= 60);
        TotalVulnsGraves = ativos.Sum(a => a.VulnsCriticas + a.VulnsAltas);
    }

    // Delegado ao TenantResolver de propósito: conta de cliente é presa à própria empresa e o
    // parâmetro  é descartado. Resolver isso aqui, em cinco cópias, era como o furo
    // sobreviveria à correção do resolvedor.
    private async Task<Company?> ResolverEmpresaAsync(int? empresaParam)
        => await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaParam);
}
