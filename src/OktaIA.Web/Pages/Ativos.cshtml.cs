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
    private readonly ScanExecutor _executor;
    private readonly TermoAutorizacaoPdfService _termoPdf;
    private readonly AdminAuditService _auditoria;

    public AtivosModel(ApplicationDbContext db, I18nService i18n, ScanExecutor executor,
        TermoAutorizacaoPdfService termoPdf, AdminAuditService auditoria)
    {
        _db = db;
        _i18n = i18n;
        _executor = executor;
        _termoPdf = termoPdf;
        _auditoria = auditoria;
    }

    public record VulnBadgeView(string Numero, string Cor, string Fundo);
    public record AssetView(int Id, string Nome, string Ip, string Tipo, string Stack, string DotCor,
        string Uptime, string UptimeCor, string Tls, string TlsCor, List<VulnBadgeView> Vulns, int Saude, string SaudeCor,
        bool Real, bool AutorizadoParaScan, string? UltimoScan, int AchadosCount, int AlertasCount);

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

    [TempData]
    public string? AtivoExcluidoNome { get; set; }

    [TempData]
    public int AtivoExcluidoAchados { get; set; }

    [TempData]
    public int AtivoExcluidoAlertas { get; set; }

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
            // Desligado por padrão: a revarredura recorrente é o item que o cliente contrata.
            // Cadastrar o ativo permite o scan manual (diagnóstico); o monitoramento contínuo é
            // ligado depois, no chip da aba Scanner, quando o contrato existir.
            MonitoramentoContinuo = false,
        });
        await _db.SaveChangesAsync();

        AtivoAdicionadoEmpresa = empresaEscolhida.Nome;
        return RedirectToPage(new { empresa = empresaEscolhida.Id });
    }

    public async Task<IActionResult> OnGetTermoAutorizacaoAsync(int empresaId, string? dominio)
    {
        var empresa = await _db.Companies.FirstOrDefaultAsync(c => c.Id == empresaId);
        var pdf = _termoPdf.Gerar(empresa?.Nome ?? "—", dominio ?? "");
        return File(pdf, "application/pdf", "termo-autorizacao-lokta-ia.pdf");
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

        // Mesmo caminho do agendador automático (ScanExecutor) — inclusive o registro das mudanças
        // em relação à varredura anterior.
        var resultado = await _executor.ExecutarAsync(asset, automatico: false);

        ScanDominio = asset.Nome;
        ScanAchados = resultado.Achados;

        return RedirectToPage(new { empresa });
    }

    // Exclusão de ativo cadastrado errado (ex.: domínio lançado na empresa errada). Restrita a
    // Admin e só a ativo Real: os do seed sustentam a demo/vitrine e não têm por que sumir.
    //
    // Achado e alerta NÃO têm FK pro Asset — se ligam por (CompanyId, AssetNome) —, então apagar
    // só a linha do Asset deixaria os achados órfãos, ainda contando no KPI e listados em
    // /Vulnerabilidades. Por isso os três somem juntos, sempre escopados na empresa do ativo pra
    // não tocar no mesmo domínio cadastrado em outra empresa.
    public async Task<IActionResult> OnPostExcluirAsync(int id, int? empresa)
    {
        if (!User.IsInRole("Admin"))
        {
            return RedirectToPage(new { empresa });
        }

        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id && a.Real);
        if (asset is null)
        {
            return RedirectToPage(new { empresa });
        }

        var achados = await _db.Vulnerabilities
            .Where(v => v.CompanyId == asset.CompanyId && v.AssetNome == asset.Nome)
            .ToListAsync();
        var alertas = await _db.ScanAlertas
            .Where(s => s.CompanyId == asset.CompanyId && s.AssetNome == asset.Nome)
            .ToListAsync();

        _db.Vulnerabilities.RemoveRange(achados);
        _db.ScanAlertas.RemoveRange(alertas);
        _db.Assets.Remove(asset);
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync("ativo.excluido",
            $"{asset.Nome} (empresa {asset.CompanyId}) — {achados.Count} achado(s) e {alertas.Count} alerta(s) removidos junto",
            User.Identity?.Name ?? "—");

        AtivoExcluidoNome = asset.Nome;
        AtivoExcluidoAchados = achados.Count;
        AtivoExcluidoAlertas = alertas.Count;

        return RedirectToPage(new { empresa = asset.CompanyId });
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
    // Delegado ao TenantResolver de propósito: conta de cliente é presa à própria empresa e o
    // parâmetro  é descartado. Resolver isso aqui, em cinco cópias, era como o furo
    // sobreviveria à correção do resolvedor.
    private async Task<Company?> ResolverEmpresaAsync(int? empresaParam)
        => await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaParam);

    private async Task CarregarAsync(int? empresaParam = null)
    {
        const string accent = "#00E0A4";
        var empresaAtual = await ResolverEmpresaAsync(empresaParam);
        EmpresasDisponiveis = (await TenantResolver.EmpresasVisiveis(HttpContext, _db).OrderBy(c => c.Nome)
            .Select(c => new { c.Id, c.Nome, c.Dominio }).ToListAsync())
            .Select(c => (c.Id, c.Nome, c.Dominio)).ToList();
        EmpresaSelecionadaId = empresaAtual?.Id;

        var ativos = await _db.Assets.Include(a => a.Company)
            .Where(a => a.CompanyId == empresaAtual!.Id)
            .OrderBy(a => a.Nome).ToListAsync();
        // (empresaAtual só é nulo se não houver nenhuma empresa ativa cadastrada — mesma
        // premissa já assumida por _Layout.cshtml pro botão de seletor.)

        // Quanto some junto se o ativo for excluído — a confirmação mostra o número em vez de um
        // "isso apaga tudo" vago. Agrupado por nome porque é assim que achado/alerta se ligam ao ativo.
        var achadosPorAtivo = (await _db.Vulnerabilities
                .Where(v => v.CompanyId == empresaAtual!.Id)
                .GroupBy(v => v.AssetNome)
                .Select(g => new { Nome = g.Key, Total = g.Count() })
                .ToListAsync())
            .ToDictionary(x => x.Nome, x => x.Total);
        var alertasPorAtivo = (await _db.ScanAlertas
                .Where(s => s.CompanyId == empresaAtual!.Id)
                .GroupBy(s => s.AssetNome)
                .Select(g => new { Nome = g.Key, Total = g.Count() })
                .ToListAsync())
            .ToDictionary(x => x.Nome, x => x.Total);

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
                a.Real, a.AutorizadoParaScan, a.UltimoScanEm?.ToString("dd/MM HH:mm"),
                achadosPorAtivo.GetValueOrDefault(a.Nome), alertasPorAtivo.GetValueOrDefault(a.Nome));
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
