using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class AtivosModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly I18nService _i18n;
    private readonly SecurityScanService _scanner;

    public AtivosModel(ApplicationDbContext db, I18nService i18n, SecurityScanService scanner)
    {
        _db = db;
        _i18n = i18n;
        _scanner = scanner;
    }

    public record VulnBadgeView(string Numero, string Cor, string Fundo);
    public record AssetView(int Id, string Nome, string Ip, string Tipo, string Stack, string DotCor,
        string Uptime, string UptimeCor, string Tls, string TlsCor, List<VulnBadgeView> Vulns, int Saude, string SaudeCor,
        bool Real, bool AutorizadoParaScan, string? UltimoScan);

    [BindProperty]
    public NovoAtivoInput Input { get; set; } = new();

    public bool MostrarForm { get; set; }
    public List<AssetView> Ativos { get; private set; } = [];
    public List<(int Id, string Nome, string? Dominio)> EmpresasDisponiveis { get; private set; } = [];
    public int? EmpresaSelecionadaId { get; private set; }

    [TempData]
    public string? ScanDominio { get; set; }

    [TempData]
    public int ScanAchados { get; set; }

    [TempData]
    public string? AtivoAdicionadoEmpresa { get; set; }

    public async Task OnGetAsync(int? empresa)
    {
        await CarregarAsync(empresa);
        Input.EmpresaId = EmpresaSelecionadaId ?? 0;
    }

    public async Task<IActionResult> OnPostAdicionarAsync(int? empresa)
    {
        MostrarForm = true;
        ModelState.Clear();
        var valido = TryValidateModel(Input, nameof(Input));
        if (!Input.AutorizoScan)
        {
            ModelState.AddModelError(nameof(Input.AutorizoScan), _i18n.Lang == "pt"
                ? "Você precisa confirmar que tem autorização pra escanear esse domínio."
                : "You must confirm you're authorized to scan this domain.");
            valido = false;
        }

        var empresaEscolhida = await _db.Companies.FirstOrDefaultAsync(c => c.Id == Input.EmpresaId && c.Ativo);
        if (empresaEscolhida is null)
        {
            ModelState.AddModelError(nameof(Input.EmpresaId), _i18n.Lang == "pt" ? "Escolha a empresa dona do domínio." : "Choose the company that owns the domain.");
            valido = false;
        }

        if (!valido)
        {
            await CarregarAsync(empresa);
            return Page();
        }

        var dominio = ExtrairHostname(Input.Dominio);
        var agora = DateTimeOffset.UtcNow;

        _db.Assets.Add(new Asset
        {
            CompanyId = empresaEscolhida!.Id,
            Nome = dominio,
            Ip = "—",
            Tipo = "WEB",
            Stack = "—",
            UptimePercentual = 100,
            TlsDias = null,
            TlsStatus = AssetTlsStatus.NaoAplicavel,
            VulnsBaixas = 0,
            VulnsMedias = 0,
            VulnsAltas = 0,
            VulnsCriticas = 0,
            Saude = 100,
            Real = true,
            AutorizadoParaScan = true,
            AutorizadoEm = agora,
        });
        await _db.SaveChangesAsync();

        AtivoAdicionadoEmpresa = empresaEscolhida.Nome;
        return RedirectToPage(new { empresa = empresaEscolhida.Id });
    }

    public async Task<IActionResult> OnPostScanAsync(int id, int? empresa)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id);

        // Defesa em profundidade: mesmo que alguém falsifique o POST, só escaneia ativo real
        // explicitamente autorizado — nunca um ativo do seed (nome pode ser domínio real de terceiro).
        if (asset is null || !asset.Real || !asset.AutorizadoParaScan)
        {
            return RedirectToPage(new { empresa });
        }

        var achados = await _scanner.ExecutarAsync(asset.Nome);

        var antigos = await _db.Vulnerabilities
            .Where(v => v.CompanyId == asset.CompanyId && v.AssetNome == asset.Nome && v.FonteScan)
            .ToListAsync();
        _db.Vulnerabilities.RemoveRange(antigos);

        foreach (var achado in achados)
        {
            _db.Vulnerabilities.Add(new Vulnerability
            {
                CompanyId = asset.CompanyId,
                FonteScan = true,
                CategoriaScan = achado.Categoria,
                RiscoPt = achado.RiscoPt,
                RiscoEn = achado.RiscoEn,
                RecomendacaoPt = achado.RecomendacaoPt,
                RecomendacaoEn = achado.RecomendacaoEn,
                InstrucoesPt = achado.InstrucoesPt,
                InstrucoesEn = achado.InstrucoesEn,
                Cve = "—",
                Cvss = achado.Severidade switch { Severidade.Critica => 9.5m, Severidade.Alta => 7.5m, Severidade.Media => 5.0m, _ => 2.5m },
                Componente = "Perímetro externo",
                TituloPt = achado.TituloPt,
                TituloEn = achado.TituloEn,
                Cwe = "—",
                AssetNome = asset.Nome,
                ExposicaoPt = "Público",
                ExposicaoEn = "Public",
                PrioridadeIa = achado.Severidade switch { Severidade.Critica => 95, Severidade.Alta => 75, Severidade.Media => 45, _ => 20 },
                StatusPt = "Aberto",
                StatusEn = "Open",
                Severidade = achado.Severidade,
            });
        }

        asset.UltimoScanEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        await AssetScoreCalculator.RecalcularAsync(_db, asset.CompanyId, asset.Nome);

        ScanDominio = asset.Nome;
        ScanAchados = achados.Count;

        return RedirectToPage(new { empresa });
    }

    // Aceita tanto "okta-ia.com" quanto "https://okta-ia.com/" (usuário costuma colar a URL da
    // barra de endereço) — extrai só o hostname, senão TcpClient/HttpClient/DNS falham em
    // silêncio pra "https://okta-ia.com/" (não é hostname válido) e todo achado vira falso
    // "ausente"/"fechado" só por não conseguir nem conectar.
    private static string ExtrairHostname(string entrada)
    {
        var bruto = entrada.Trim();
        var comEsquema = bruto.Contains("://", StringComparison.Ordinal) ? bruto : $"https://{bruto}";
        var host = Uri.TryCreate(comEsquema, UriKind.Absolute, out var uri) ? uri.Host : bruto.TrimEnd('/');
        return host.ToLowerInvariant();
    }

    // Empresa explicitamente escolhida no filtro local da página tem prioridade; senão cai no
    // tenant global do cabeçalho — mesmo padrão usado em Vulnerabilidades.
    private async Task<Company?> ResolverEmpresaAsync(int? empresaParam)
    {
        if (empresaParam.HasValue)
        {
            var empresa = await _db.Companies.FirstOrDefaultAsync(c => c.Id == empresaParam.Value && c.Ativo);
            if (empresa is not null)
            {
                return empresa;
            }
        }

        return await TenantResolver.ResolverAtualAsync(HttpContext, _db);
    }

    private async Task CarregarAsync(int? empresaParam = null)
    {
        const string accent = "#00E0A4";
        var empresaAtual = await ResolverEmpresaAsync(empresaParam);
        EmpresasDisponiveis = (await _db.Companies.Where(c => c.Ativo).OrderBy(c => c.Nome)
            .Select(c => new { c.Id, c.Nome, c.Dominio }).ToListAsync())
            .Select(c => (c.Id, c.Nome, c.Dominio)).ToList();
        EmpresaSelecionadaId = empresaAtual?.Id;

        var ativos = await _db.Assets.Include(a => a.Company)
            .Where(a => a.CompanyId == empresaAtual!.Id)
            .OrderBy(a => a.Nome).ToListAsync();
        // (empresaAtual só é nulo se não houver nenhuma empresa ativa cadastrada — mesma
        // premissa já assumida por _Layout.cshtml pro botão de seletor.)

        string SaudeCor(int v) => v > 85 ? accent : v > 60 ? "#FFC93C" : "#FF3B5C";
        string TlsCor(AssetTlsStatus s) => s switch
        {
            AssetTlsStatus.Critico => "#FF3B5C",
            AssetTlsStatus.Alerta => "#FF8A3D",
            AssetTlsStatus.NaoAplicavel => "#3C4C60",
            _ => accent,
        };

        Ativos = ativos.Select(a =>
        {
            // Ordem visual: Crítica, Alta, Média, Baixa (esquerda pra direita) — mesma ordem do mockup.
            var vulns = new List<(int N, string Cor)>
            {
                (a.VulnsCriticas, "#FF3B5C"), (a.VulnsAltas, "#FF8A3D"), (a.VulnsMedias, "#FFC93C"), (a.VulnsBaixas, "#4D9BFF"),
            };

            return new AssetView(
                a.Id, a.Nome, a.Ip, a.Tipo, a.Stack, SaudeCor(a.Saude),
                a.UptimePercentual.ToString("0.00") + "%", a.UptimePercentual > 99.5m ? accent : "#FF8A3D",
                a.TlsDias is null ? "—" : $"{a.TlsDias}d", TlsCor(a.TlsStatus),
                vulns.Select(v => new VulnBadgeView(v.N.ToString(), v.N > 0 ? v.Cor : "#3C4C60", v.N > 0 ? v.Cor + "1F" : "#0F1720")).ToList(),
                a.Saude, SaudeCor(a.Saude),
                a.Real, a.AutorizadoParaScan, a.UltimoScanEm?.ToString("dd/MM HH:mm"));
        }).ToList();
    }

    public class NovoAtivoInput
    {
        [Required(ErrorMessage = "Informe o domínio.")]
        public string Dominio { get; set; } = "";

        public bool AutorizoScan { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Escolha a empresa dona do domínio.")]
        public int EmpresaId { get; set; }
    }
}
