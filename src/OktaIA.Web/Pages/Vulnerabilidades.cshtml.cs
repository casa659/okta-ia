using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

[Authorize]
public class VulnerabilidadesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly I18nService _i18n;
    private readonly SecurityScanService _scanner;
    private readonly RelatorioPdfService _relatorioPdf;
    private readonly PropostaComercialPdfService _propostaPdf;

    public VulnerabilidadesModel(ApplicationDbContext db, I18nService i18n, SecurityScanService scanner, RelatorioPdfService relatorioPdf, PropostaComercialPdfService propostaPdf)
    {
        _db = db;
        _i18n = i18n;
        _scanner = scanner;
        _relatorioPdf = relatorioPdf;
        _propostaPdf = propostaPdf;
    }

    public record KpiView(string Label, string Valor, string Cor);
    public record VulnRow(int Id, string Cve, string Cvss, string ScoreCor, string Titulo, string Cwe, string Asset,
        string Exposicao, string ExposicaoCor, string ExposicaoFundo, int Prioridade, string PrioridadeCor, string Status, string StatusCor,
        bool FonteScan, string? Risco, string? Recomendacao, string? Instrucoes);

    public string Tab { get; private set; } = "v";
    public List<KpiView> Kpis { get; private set; } = [];
    public List<VulnRow> Vulns { get; private set; } = [];

    // Placar da empresa selecionada, calculado a partir dos achados reais (FonteScan=true) — só
    // populado quando a empresa tem pelo menos 1 ativo real cadastrado, senão o placar não
    // significa nada (nunca foi escaneada) e a UI esconde o bloco inteiro.
    public CompanySecurityScoreCalculator.Resultado? Score { get; private set; }
    public string? NarrativaIa { get; private set; }
    public DateTimeOffset? UltimaVarredura { get; private set; }

    // Filtro de empresa local a esta página — independente do seletor de tenant do cabeçalho, pra
    // dar uma olhada rápida (ou baixar o PDF) de outra empresa sem trocar o contexto do console
    // inteiro. Sempre 1 empresa por vez: nunca combina dado de clientes diferentes num só PDF.
    public List<(int Id, string Nome)> EmpresasDisponiveis { get; private set; } = [];
    public int? EmpresaSelecionadaId { get; private set; }

    [TempData]
    public string? ReverificarResultado { get; set; } // "corrigido" | "ainda-presente" | null

    // Scanner — dado operacional/decorativo (não é histórico de negócio), mesma lista fixa do mockup.
    public record ScanView(string Ferramenta, string Alvo, int Pct, string Estado, string Cor);
    public List<ScanView> Scans { get; private set; } = [];
    public string[] ScanTypes { get; private set; } = [];
    public (string Nome, string Atualizacao)[] Feeds { get; private set; } = [];

    public async Task OnGetAsync(string? tab, int? empresa)
    {
        Tab = tab == "s" ? "s" : "v";
        var lang = _i18n.Lang;
        const string accent = "#00E0A4";

        var empresaAtual = await ResolverEmpresaAsync(empresa);
        EmpresasDisponiveis = await _db.Companies.Where(c => c.Ativo).OrderBy(c => c.Nome)
            .Select(c => new { c.Id, c.Nome }).ToListAsync()
            is var lista ? lista.Select(c => (c.Id, c.Nome)).ToList() : [];
        EmpresaSelecionadaId = empresaAtual?.Id;

        var vulns = await _db.Vulnerabilities.Where(v => v.CompanyId == empresaAtual!.Id)
            .OrderByDescending(v => v.PrioridadeIa).ToListAsync();

        var ativosReais = await _db.Assets.Where(a => a.CompanyId == empresaAtual!.Id && a.Real).ToListAsync();
        if (ativosReais.Count > 0)
        {
            var achadosReais = vulns.Where(v => v.FonteScan).ToList();
            var portasAbertas = achadosReais.Count(v => v.CategoriaScan == SecurityScanService.CategoriaPortas);
            Score = CompanySecurityScoreCalculator.Calcular(achadosReais, ativosReais.Count, portasAbertas);
            var datasScan = ativosReais.Where(a => a.UltimoScanEm.HasValue).Select(a => a.UltimoScanEm!.Value).ToList();
            UltimaVarredura = datasScan.Count > 0 ? datasScan.Max() : null;

            var prioridade = CompanySecurityScoreCalculator.PrioridadeDaSemana(achadosReais);
            if (prioridade is not null)
            {
                var tempo = CompanySecurityScoreCalculator.TempoEstimadoMinutos(prioridade);
                var titulo = lang == "pt" ? prioridade.TituloPt : prioridade.TituloEn;
                var risco = lang == "pt" ? prioridade.RiscoPt : prioridade.RiscoEn;
                NarrativaIa = lang == "pt"
                    ? $"Sua empresa tem risco {Score.RiscoLabelPt.ToLowerInvariant()} de invasão externa. Entretanto, existe \"{titulo}\"" + (string.IsNullOrWhiteSpace(risco) ? "." : $" — {risco}") + $" Este é o item mais importante para corrigir esta semana. Tempo estimado: {tempo} minutos."
                    : $"Your company has {Score.RiscoLabelEn.ToLowerInvariant()} risk of external breach. However, there is \"{titulo}\"" + (string.IsNullOrWhiteSpace(risco) ? "." : $" — {risco}") + $" This is the most important item to fix this week. Estimated time: {tempo} minutes.";
            }
            else
            {
                NarrativaIa = lang == "pt"
                    ? "Nenhum achado aberto nos ativos reais monitorados — mantenha o monitoramento contínuo ativo pra pegar qualquer mudança cedo."
                    : "No open findings on the monitored real assets — keep continuous monitoring active to catch any change early.";
            }
        }

        var totalAberto = vulns.Count(v => v.StatusPt is "Aberto" or "Em correção");
        var criticas = vulns.Count(v => v.Severidade == Severidade.Critica);
        var altas = vulns.Count(v => v.Severidade == Severidade.Alta);
        var kev = vulns.Count(v => v.ExposicaoPt == "KEV");
        Kpis =
        [
            new(lang == "pt" ? "Total aberto" : "Total open", totalAberto.ToString(), "#D4DDEA"),
            new(lang == "pt" ? "Crítico" : "Critical", criticas.ToString(), "#FF3B5C"),
            new(lang == "pt" ? "Alto" : "High", altas.ToString(), "#FF8A3D"),
            new("CISA KEV", kev.ToString(), "#FF3B5C"),
            new(lang == "pt" ? "Corrigidos 30d" : "Fixed 30d", vulns.Count(v => v.StatusPt == "Corrigido").ToString(), accent),
        ];

        Vulns = vulns.Select(v =>
        {
            var scoreCor = v.Cvss >= 9 ? "#FF3B5C" : v.Cvss >= 7 ? "#FF8A3D" : "#FFC93C";
            var exposicao = lang == "pt" ? v.ExposicaoPt : v.ExposicaoEn;
            var isKev = exposicao == "KEV";
            var status = lang == "pt" ? v.StatusPt : v.StatusEn;
            var statusCor = status is "Aberto" or "Open" ? "#FF3B5C"
                : (status.Contains("Corrig") || status.Contains("Fixed") || status.Contains("Mitig")) ? accent
                : "#FFC93C";

            return new VulnRow(
                v.Id, v.Cve, v.Cvss.ToString("0.0"), scoreCor,
                lang == "pt" ? v.TituloPt : v.TituloEn, $"{v.Cwe} · {v.Componente}", v.AssetNome,
                exposicao, isKev ? "#FF3B5C" : "#6E8098", isKev ? "#2A0D14" : "#0F1720",
                v.PrioridadeIa, v.PrioridadeIa > 80 ? "#FF3B5C" : v.PrioridadeIa > 50 ? "#FF8A3D" : "#FFC93C",
                status, statusCor,
                v.FonteScan, lang == "pt" ? v.RiscoPt : v.RiscoEn, lang == "pt" ? v.RecomendacaoPt : v.RecomendacaoEn, lang == "pt" ? v.InstrucoesPt : v.InstrucoesEn);
        }).ToList();

        var scanPct = new[] { 72, 38, 91, 14, 56, 100 };
        (string Ferramenta, string Alvo)[] scanDefs =
        [
            ("Nuclei", "api.grupovector.com"), ("OWASP ZAP", "portal.hsanta.br"), ("Nmap", "10.20.0.0/16"),
            ("Trivy", "registry/prod:latest"), ("Subfinder", "grupovector.com"), ("Nikto", "wp.lojaativa.com.br"),
        ];
        Scans = scanDefs.Select((s, i) =>
        {
            var pct = scanPct[i];
            var done = pct >= 100;
            return new ScanView(s.Ferramenta, s.Alvo, pct, done ? (lang == "pt" ? "CONCLUÍDO" : "DONE") : $"{pct}%", done ? accent : "#4D9BFF");
        }).ToList();

        ScanTypes = ["Portas", "DNS", "Subdomínios", "SSL/TLS", "Headers", "Cookies", "CORS", "CSRF", "SQLi", "XSS",
            "XXE", "RCE", "LFI/RFI", "Open Redirect", "Path Traversal", "Clickjacking", "Dir Listing", "Uploads", "JWT", "OAuth", "GraphQL", "SMTP", "SSH", "RDP"];

        Feeds =
        [
            ("NVD / NIST", "4 min"), ("CISA KEV", "11 min"), ("MITRE ATT&CK", "1 h"),
            ("EPSS", "6 h"), ("OWASP Top 10", "v2025"), ("AbuseIPDB", "2 min"),
        ];
    }

    public async Task<IActionResult> OnPostReverificarAsync(int id, int? empresa)
    {
        var empresaAtual = await ResolverEmpresaAsync(empresa);
        var vuln = await _db.Vulnerabilities.FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == empresaAtual!.Id && v.FonteScan);
        if (vuln is null || vuln.CategoriaScan is null)
        {
            return RedirectToPage(new { tab = "v", empresa });
        }

        var achadosAtuais = await _scanner.ExecutarCategoriaAsync(vuln.AssetNome, vuln.CategoriaScan);
        var aindaPresente = achadosAtuais.Any(a => a.TituloPt == vuln.TituloPt);

        if (!aindaPresente)
        {
            _db.Vulnerabilities.Remove(vuln);
            await _db.SaveChangesAsync();
            await AssetScoreCalculator.RecalcularAsync(_db, vuln.CompanyId, vuln.AssetNome);
            ReverificarResultado = "corrigido";
        }
        else
        {
            ReverificarResultado = "ainda-presente";
        }

        return RedirectToPage(new { tab = "v", empresa });
    }

    public async Task<IActionResult> OnGetPdfAsync(int? empresa)
    {
        var empresaAtual = await ResolverEmpresaAsync(empresa);
        var achados = await _db.Vulnerabilities
            .Where(v => v.CompanyId == empresaAtual!.Id && v.FonteScan)
            .ToListAsync();
        var ativosReais = await _db.Assets
            .Where(a => a.CompanyId == empresaAtual!.Id && a.Real)
            .Select(a => new { a.Nome, a.UltimoScanEm })
            .ToListAsync();

        var pdf = _relatorioPdf.Gerar(
            empresaAtual?.Nome ?? "—",
            achados,
            ativosReais.Select(a => (a.Nome, a.UltimoScanEm)).ToList(),
            _i18n.Lang);

        var nomeArquivo = $"relatorio-seguranca-{(empresaAtual?.Nome ?? "empresa").Replace(" ", "-").ToLowerInvariant()}.pdf";
        return File(pdf, "application/pdf", nomeArquivo);
    }

    public async Task<IActionResult> OnGetPdfClienteAsync(int? empresa)
    {
        var empresaAtual = await ResolverEmpresaAsync(empresa);
        var achados = await _db.Vulnerabilities
            .Where(v => v.CompanyId == empresaAtual!.Id && v.FonteScan)
            .ToListAsync();
        var ativosReais = await _db.Assets
            .Where(a => a.CompanyId == empresaAtual!.Id && a.Real)
            .Select(a => new { a.Nome, a.UltimoScanEm })
            .ToListAsync();

        var pdf = _relatorioPdf.Gerar(
            empresaAtual?.Nome ?? "—",
            achados,
            ativosReais.Select(a => (a.Nome, a.UltimoScanEm)).ToList(),
            _i18n.Lang,
            paraCliente: true);

        var nomeArquivo = $"relatorio-cliente-{(empresaAtual?.Nome ?? "empresa").Replace(" ", "-").ToLowerInvariant()}.pdf";
        return File(pdf, "application/pdf", nomeArquivo);
    }

    public async Task<IActionResult> OnGetPropostaAsync(int? empresa)
    {
        var empresaAtual = await ResolverEmpresaAsync(empresa);
        if (empresaAtual is null)
        {
            return RedirectToPage(new { tab = "v", empresa });
        }

        var achadosReais = await _db.Vulnerabilities
            .Where(v => v.CompanyId == empresaAtual.Id && v.FonteScan)
            .ToListAsync();
        var ativos = await _db.Assets.Where(a => a.CompanyId == empresaAtual.Id).ToListAsync();
        var ativosReais = ativos.Where(a => a.Real).ToList();
        var ultimaVarredura = ativosReais.Where(a => a.UltimoScanEm.HasValue).Select(a => a.UltimoScanEm!.Value)
            .OrderByDescending(d => d).FirstOrDefault();

        var pdf = _propostaPdf.Gerar(empresaAtual, achadosReais, ativosReais.Count, ativos.Count,
            ultimaVarredura == default ? null : ultimaVarredura);

        var nomeArquivo = $"proposta-comercial-okta-ia-{empresaAtual.Nome.Replace(" ", "-").ToLowerInvariant()}.pdf";
        return File(pdf, "application/pdf", nomeArquivo);
    }

    // Empresa explicitamente escolhida no filtro da página tem prioridade; senão cai no tenant
    // global do cabeçalho (mesma resolução usada em Dashboard/Ativos/etc).
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
}
