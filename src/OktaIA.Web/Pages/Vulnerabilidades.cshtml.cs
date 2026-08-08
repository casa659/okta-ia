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
    private readonly IConfiguration _config;

    public VulnerabilidadesModel(ApplicationDbContext db, I18nService i18n, SecurityScanService scanner,
        RelatorioPdfService relatorioPdf, PropostaComercialPdfService propostaPdf, IConfiguration config)
    {
        _db = db;
        _i18n = i18n;
        _scanner = scanner;
        _relatorioPdf = relatorioPdf;
        _propostaPdf = propostaPdf;
        _config = config;
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

    // Aba "Scanner": estado REAL do monitoramento desta empresa. Antes era um mockup com
    // ferramentas (Nuclei/ZAP/Nmap), percentuais fixos e "feeds de inteligência" inventados —
    // removido de propósito: dado falso numa tela de segurança é passivo, não recurso (basta o
    // cliente abrir duas vezes e ver o mesmo "72%" parado pra perder a confiança na plataforma
    // inteira). Só aparece aqui o que o SecurityScanService de fato executa.
    public record AtivoMonitoradoView(int Id, string Nome, string Ip, string UltimoScan, bool NuncaEscaneado,
        int Criticas, int Altas, int Medias, int Baixas, int Saude, string SaudeCor,
        bool MonitoramentoContinuo, string ProximoScan);
    public record ChecagemView(string Nome, string Descricao, int Achados);
    public record MudancaView(string Quando, string AssetNome, string Titulo, bool Novo, string Cor, bool Automatico);

    public List<AtivoMonitoradoView> AtivosMonitorados { get; private set; } = [];
    public List<ChecagemView> Checagens { get; private set; } = [];
    public int[] PortasVerificadas { get; private set; } = [];
    public List<MudancaView> Mudancas { get; private set; } = [];
    public string IntervaloMonitoramento { get; private set; } = "";

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
        // Antes havia um KPI "CISA KEV" (contava ExposicaoPt=="KEV", valor que só existe no seed de
        // demonstração) — o rótulo anunciava uma correlação com o catálogo KEV da CISA que a
        // plataforma não faz; para cliente real ele exibiria 0 eternamente, sugerindo "nenhuma vuln
        // explorada ativamente" quando na verdade nunca se consultou o catálogo. Trocado por portas
        // expostas, que é sinal real produzido pelo scanner.
        var portasExpostas = vulns.Count(v => v.FonteScan && v.CategoriaScan == SecurityScanService.CategoriaPortas);
        // "Corrigidos 30d" não filtrava por data nenhuma (contava todo "Corrigido" do histórico) e
        // não há campo de data de correção pra filtrar — rótulo ajustado ao que de fato é contado.
        Kpis =
        [
            new(lang == "pt" ? "Total aberto" : "Total open", totalAberto.ToString(), "#D4DDEA"),
            new(lang == "pt" ? "Crítico" : "Critical", criticas.ToString(), "#FF3B5C"),
            new(lang == "pt" ? "Alto" : "High", altas.ToString(), "#FF8A3D"),
            new(lang == "pt" ? "Portas expostas" : "Exposed ports", portasExpostas.ToString(), portasExpostas > 0 ? "#FF3B5C" : accent),
            new(lang == "pt" ? "Corrigidos" : "Fixed", vulns.Count(v => v.StatusPt == "Corrigido").ToString(), accent),
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

        await MontarAbaScannerAsync(ativosReais, vulns, empresaAtual?.Id, lang, accent);
    }

    // Estado real do monitoramento: quais ativos são escaneáveis, quando cada um foi visto pela
    // última vez e quantos achados cada checagem produziu. Sem ativo real cadastrado, a aba fica
    // legitimamente vazia (a view mostra o aviso) em vez de exibir atividade fictícia.
    private async Task MontarAbaScannerAsync(List<Asset> ativosReais, List<Vulnerability> vulns, int? companyId, string lang, string accent)
    {
        var intervaloHoras = _config.GetValue("Scanner:IntervaloHoras", 24d);
        IntervaloMonitoramento = intervaloHoras >= 24 && intervaloHoras % 24 == 0
            ? $"{intervaloHoras / 24:0}{(lang == "pt" ? "d" : "d")}"
            : $"{intervaloHoras:0}h";

        AtivosMonitorados = ativosReais
            .OrderBy(a => a.Nome)
            .Select(a => new AtivoMonitoradoView(
                a.Id,
                a.Nome,
                string.IsNullOrWhiteSpace(a.Ip) ? "—" : a.Ip,
                a.UltimoScanEm?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? _i18n.T("neverScanned"),
                a.UltimoScanEm is null,
                a.VulnsCriticas, a.VulnsAltas, a.VulnsMedias, a.VulnsBaixas,
                a.Saude,
                a.Saude > 85 ? accent : a.Saude > 60 ? "#FFC93C" : "#FF3B5C",
                a.MonitoramentoContinuo,
                ProximoScanTexto(a, intervaloHoras, lang)))
            .ToList();

        // Histórico de mudanças: é o que prova que o monitoramento está rodando sozinho, e a
        // pergunta que o cliente realmente faz ("o que mudou desde ontem?").
        var alertas = await _db.ScanAlertas
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.DetectadoEm)
            .Take(12)
            .ToListAsync();

        Mudancas = alertas.Select(s => new MudancaView(
            s.DetectadoEm.ToLocalTime().ToString("dd/MM HH:mm"),
            s.AssetNome,
            lang == "pt" ? s.TituloPt : s.TituloEn,
            s.Tipo == TipoMudancaScan.Novo,
            s.Tipo == TipoMudancaScan.Novo
                ? s.Severidade switch
                {
                    Severidade.Critica => "#FF3B5C",
                    Severidade.Alta => "#FF8A3D",
                    Severidade.Media => "#FFC93C",
                    _ => "#4D9BFF",
                }
                : accent,
            s.Automatico)).ToList();

        // Contagem por categoria vem dos achados reais (FonteScan) — as 4 categorias abaixo são
        // exatamente as que SecurityScanService.ExecutarCategoriaAsync sabe executar.
        var achadosReais = vulns.Where(v => v.FonteScan).ToList();
        int PorCategoria(string categoria) => achadosReais.Count(v => v.CategoriaScan == categoria);

        Checagens = lang == "pt"
            ?
            [
                new("TLS / Certificado", "Validade do certificado, expiração próxima e versão do protocolo negociado.", PorCategoria(SecurityScanService.CategoriaTls)),
                new("Cabeçalhos HTTP", "HSTS, CSP, X-Content-Type-Options e X-Frame-Options.", PorCategoria(SecurityScanService.CategoriaHeaders)),
                new("DNS de e-mail", "Registros SPF e DMARC publicados no domínio.", PorCategoria(SecurityScanService.CategoriaDns)),
                new("Portas expostas", "Serviços administrativos e de banco de dados acessíveis pela internet.", PorCategoria(SecurityScanService.CategoriaPortas)),
            ]
            :
            [
                new("TLS / Certificate", "Certificate validity, upcoming expiration and negotiated protocol version.", PorCategoria(SecurityScanService.CategoriaTls)),
                new("HTTP headers", "HSTS, CSP, X-Content-Type-Options and X-Frame-Options.", PorCategoria(SecurityScanService.CategoriaHeaders)),
                new("Email DNS", "SPF and DMARC records published on the domain.", PorCategoria(SecurityScanService.CategoriaDns)),
                new("Exposed ports", "Administrative and database services reachable from the internet.", PorCategoria(SecurityScanService.CategoriaPortas)),
            ];

        PortasVerificadas = SecurityScanService.PortasComuns;
    }

    // Quando o agendador deve pegar este ativo. O texto reflete a fila real: sem monitoramento
    // contínuo ele nunca entra, e um ativo já vencido aparece como "na próxima verificação" em vez
    // de mostrar uma data no passado.
    private string ProximoScanTexto(Asset a, double intervaloHoras, string lang)
    {
        if (!a.MonitoramentoContinuo)
        {
            return lang == "pt" ? "monitoramento desligado" : "monitoring off";
        }

        if (a.UltimoScanEm is null)
        {
            return lang == "pt" ? "na próxima verificação" : "on next check";
        }

        var proximo = a.UltimoScanEm.Value.AddHours(intervaloHoras);
        return proximo <= DateTimeOffset.UtcNow
            ? (lang == "pt" ? "na próxima verificação" : "on next check")
            : proximo.ToLocalTime().ToString("dd/MM HH:mm");
    }

    // Liga/desliga a revarredura automática do ativo. Não mexe em AutorizadoParaScan: a
    // autorização do cliente continua registrada, só a frequência é pausada.
    public async Task<IActionResult> OnPostMonitoramentoAsync(int id, int? empresa)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id && a.Real && a.AutorizadoParaScan);
        if (asset is not null)
        {
            asset.MonitoramentoContinuo = !asset.MonitoramentoContinuo;
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { tab = "s", empresa });
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
            .Select(a => new { a.Nome, a.Ip, a.UltimoScanEm })
            .ToListAsync();

        var pdf = _relatorioPdf.Gerar(
            empresaAtual?.Nome ?? "—",
            achados,
            ativosReais.Select(a => (a.Nome, a.Ip, a.UltimoScanEm)).ToList(),
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
            .Select(a => new { a.Nome, a.Ip, a.UltimoScanEm })
            .ToListAsync();

        var pdf = _relatorioPdf.Gerar(
            empresaAtual?.Nome ?? "—",
            achados,
            ativosReais.Select(a => (a.Nome, a.Ip, a.UltimoScanEm)).ToList(),
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

        var nomeArquivo = $"proposta-comercial-lokta-ia-{empresaAtual.Nome.Replace(" ", "-").ToLowerInvariant()}.pdf";
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
