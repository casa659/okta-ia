using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

public record CopilotEvidence(string Chave, string Valor, string Cor);
public record CopilotPrompt(string Pergunta, string Resposta, CopilotEvidence[] Evidencias);

// Perguntas sugeridas do Copiloto, respondidas a partir do BANCO da empresa em contexto.
//
// Substitui o antigo CopilotPrompts, que servia 5 respostas inventadas ("3.418 tentativas de
// autenticação de 214 IPs em ASNs russos contra o vpn-sp01", certificados de domínios que não
// existem). Numa plataforma de segurança esse tipo de conteúdo é passivo, não recurso: basta o
// cliente pedir "me mostra esses 214 IPs" pra derrubar a confiança na ferramenta inteira.
//
// Ainda não há LLM: as perguntas são fixas e as respostas são calculadas por consulta. A regra é
// simples — todo número exibido aqui tem que sair de uma linha do banco. Quando não há dado
// (empresa sem ativo real, nunca escaneada), a resposta diz exatamente isso em vez de preencher
// com exemplo.
public class CopilotService
{
    private readonly ApplicationDbContext _db;

    private const string CorOk = "#00E0A4";
    private const string CorAtencao = "#FFC93C";
    private const string CorAlta = "#FF8A3D";
    private const string CorCritica = "#FF3B5C";
    private const string CorNeutra = "#4D9BFF";

    public CopilotService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<CopilotPrompt>> GerarAsync(int? companyId, string lang)
    {
        var pt = lang != "en";
        if (companyId is null)
        {
            return [];
        }

        var ativosReais = await _db.Assets
            .Where(a => a.CompanyId == companyId && a.Real)
            .ToListAsync();
        var achados = await _db.Vulnerabilities
            .Where(v => v.CompanyId == companyId && v.FonteScan)
            .ToListAsync();

        return
        [
            Score(ativosReais, achados, pt),
            Achados(ativosReais, achados, pt),
            Certificados(ativosReais, achados, pt),
            UltimaVarredura(ativosReais, pt),
            Portas(ativosReais, achados, pt),
        ];
    }

    // "Briefing do turno" do Dashboard. Antes era uma frase fixa no Translations ("Pico de força
    // bruta contra vpn-sp01 vindo de 3 ASNs russos nas últimas 40 min") — idêntica pra toda
    // empresa e sem nenhuma relação com o banco. Agora resume o estado real do tenant.
    public async Task<string> GerarBriefingAsync(int? companyId, string lang)
    {
        var pt = lang != "en";
        var ativos = await _db.Assets.Where(a => a.CompanyId == companyId && a.Real).ToListAsync();

        if (ativos.Count == 0)
        {
            return pt
                ? "Nenhum ativo real está sendo monitorado nesta empresa. Cadastre um domínio autorizado em Ativos para que o briefing passe a refletir o ambiente."
                : "No real asset is being monitored for this company. Register an authorized domain under Assets so this briefing reflects the environment.";
        }

        var achados = await _db.Vulnerabilities
            .Where(v => v.CompanyId == companyId && v.FonteScan)
            .ToListAsync();

        var portasAbertas = achados.Count(a => a.CategoriaScan == SecurityScanService.CategoriaPortas);
        var r = CompanySecurityScoreCalculator.Calcular(achados, ativos.Count, portasAbertas);
        var nunca = ativos.Count(a => a.UltimoScanEm is null);

        if (achados.Count == 0)
        {
            var baseTexto = pt
                ? $"Score {r.Score}/100 ({r.Classificacao}) em {ativos.Count} ativo(s) monitorado(s), sem achados em aberto no último scan."
                : $"Score {r.Score}/100 ({r.Classificacao}) across {ativos.Count} monitored asset(s), with no open findings in the last scan.";
            return nunca == 0
                ? baseTexto
                : baseTexto + (pt
                    ? $" Atenção: {nunca} ativo(s) ainda nunca foi(ram) escaneado(s)."
                    : $" Note: {nunca} asset(s) have never been scanned.");
        }

        var prioridade = CompanySecurityScoreCalculator.PrioridadeDaSemana(achados);
        var criticas = achados.Count(a => a.Severidade == Severidade.Critica);
        var altas = achados.Count(a => a.Severidade == Severidade.Alta);

        var texto = pt
            ? $"Score {r.Score}/100 ({r.Classificacao}) em {ativos.Count} ativo(s) monitorado(s): {criticas} achado(s) crítico(s) e {altas} alto(s) em aberto."
            : $"Score {r.Score}/100 ({r.Classificacao}) across {ativos.Count} monitored asset(s): {criticas} critical and {altas} high finding(s) open.";

        if (prioridade is not null)
        {
            var titulo = pt ? prioridade.TituloPt : prioridade.TituloEn;
            var minutos = CompanySecurityScoreCalculator.TempoEstimadoMinutos(prioridade);
            texto += pt
                ? $" Prioridade agora: \"{titulo}\" em {prioridade.AssetNome} — correção estimada em {minutos} minutos."
                : $" Top priority now: \"{titulo}\" on {prioridade.AssetNome} — estimated fix time {minutos} minutes.";
        }

        return texto;
    }

    // Pergunta do topo da tela de SIEM. Os eventos vêm da tabela SecurityEvents da própria
    // empresa — mesma fonte que alimenta os gráficos daquela página.
    public async Task<CopilotPrompt> GerarEventosAsync(int? companyId, string lang)
    {
        var pt = lang != "en";
        var pergunta = pt ? "O que aconteceu nas últimas 24 horas?" : "What happened in the last 24 hours?";
        var desde = DateTime.UtcNow.AddHours(-24);

        var eventos = await _db.SecurityEvents
            .Where(e => e.CompanyId == companyId && e.CriadoEm >= desde)
            .ToListAsync();

        if (eventos.Count == 0)
        {
            return new CopilotPrompt(pergunta,
                pt ? "Nenhum evento de segurança registrado para esta empresa nas últimas 24 horas."
                   : "No security events recorded for this company in the last 24 hours.",
                [new CopilotEvidence(pt ? "eventos / 24h" : "events / 24h", "0", CorOk)]);
        }

        var bloqueados = eventos.Count(e => e.Bloqueado);
        var criticos = eventos.Count(e => e.Severidade == Severidade.Critica);

        return new CopilotPrompt(pergunta,
            pt
                ? $"Foram registrados {eventos.Count} evento(s) nas últimas 24 horas, dos quais {bloqueados} bloqueado(s) automaticamente e {criticos} classificado(s) como crítico(s)."
                : $"{eventos.Count} event(s) were recorded in the last 24 hours, {bloqueados} automatically blocked and {criticos} classified as critical.",
            [
                new CopilotEvidence(pt ? "eventos / 24h" : "events / 24h", eventos.Count.ToString(), CorNeutra),
                new CopilotEvidence(pt ? "bloqueados" : "blocked", bloqueados.ToString(), CorOk),
                new CopilotEvidence(pt ? "críticos" : "critical", criticos.ToString(), criticos > 0 ? CorCritica : CorOk),
            ]);
    }

    private static CopilotPrompt Score(List<Asset> ativos, List<Vulnerability> achados, bool pt)
    {
        var pergunta = pt ? "Qual é o nosso score de segurança?" : "What is our security score?";
        if (ativos.Count == 0)
        {
            return SemAtivo(pergunta, pt);
        }

        var portasAbertas = achados.Count(a => a.CategoriaScan == SecurityScanService.CategoriaPortas);
        var r = CompanySecurityScoreCalculator.Calcular(achados, ativos.Count, portasAbertas);

        return new CopilotPrompt(pergunta,
            pt
                ? $"O score atual é {r.Score}/100 (classificação {r.Classificacao}), calculado sobre {ativos.Count} ativo(s) monitorado(s) e {achados.Count} achado(s) em aberto. O risco de invasão externa está classificado como {r.RiscoLabelPt.ToLowerInvariant()}."
                : $"The current score is {r.Score}/100 (rating {r.Classificacao}), based on {ativos.Count} monitored asset(s) and {achados.Count} open finding(s). External breach risk is rated {r.RiscoLabelEn.ToLowerInvariant()}.",
            [
                new CopilotEvidence("score", $"{r.Score}/100", r.RiscoCor),
                new CopilotEvidence(pt ? "classificação" : "rating", r.Classificacao, r.RiscoCor),
                new CopilotEvidence(pt ? "ativos monitorados" : "monitored assets", ativos.Count.ToString(), CorNeutra),
            ]);
    }

    private static CopilotPrompt Achados(List<Asset> ativos, List<Vulnerability> achados, bool pt)
    {
        var pergunta = pt ? "Quais achados estão abertos agora?" : "Which findings are open right now?";
        if (ativos.Count == 0)
        {
            return SemAtivo(pergunta, pt);
        }

        var criticas = achados.Count(a => a.Severidade == Severidade.Critica);
        var altas = achados.Count(a => a.Severidade == Severidade.Alta);
        var medias = achados.Count(a => a.Severidade == Severidade.Media);
        var baixas = achados.Count(a => a.Severidade == Severidade.Baixa);

        var resposta = achados.Count == 0
            ? (pt ? "Nenhum achado em aberto nos ativos monitorados. Todas as checagens do último scan passaram."
                  : "No open findings on the monitored assets. All checks passed in the last scan.")
            : (pt
                ? $"Há {achados.Count} achado(s) em aberto: {criticas} crítico(s), {altas} alto(s), {medias} médio(s) e {baixas} baixo(s). O detalhe de cada um, com risco e instruções de correção, está na aba Vulnerabilidades."
                : $"There are {achados.Count} open finding(s): {criticas} critical, {altas} high, {medias} medium and {baixas} low. Details for each, with risk and remediation steps, are on the Vulnerabilities tab.");

        return new CopilotPrompt(pergunta, resposta,
            [
                new CopilotEvidence(pt ? "críticos" : "critical", criticas.ToString(), criticas > 0 ? CorCritica : CorOk),
                new CopilotEvidence(pt ? "altos" : "high", altas.ToString(), altas > 0 ? CorAlta : CorOk),
                new CopilotEvidence(pt ? "médios" : "medium", medias.ToString(), medias > 0 ? CorAtencao : CorOk),
            ]);
    }

    private static CopilotPrompt Certificados(List<Asset> ativos, List<Vulnerability> achados, bool pt)
    {
        var pergunta = pt ? "Algum certificado está vencendo?" : "Is any certificate expiring?";
        if (ativos.Count == 0)
        {
            return SemAtivo(pergunta, pt);
        }

        // O scanner grava esses dois títulos exatos na categoria "tls" (ver SecurityScanService).
        var certificados = achados
            .Where(a => a.CategoriaScan == SecurityScanService.CategoriaTls
                        && (a.TituloPt == "Certificado TLS expirado" || a.TituloPt == "Certificado TLS expirando em breve"))
            .ToList();

        if (certificados.Count == 0)
        {
            var comTls = ativos.Count(a => a.TlsStatus != AssetTlsStatus.NaoAplicavel);
            return new CopilotPrompt(pergunta,
                pt
                    ? $"Nenhum certificado vencido ou próximo do vencimento nos ativos monitorados. A checagem de TLS alerta a partir de 30 dias para o vencimento."
                    : "No expired or soon-to-expire certificates on the monitored assets. The TLS check alerts starting 30 days before expiration.",
                [
                    new CopilotEvidence(pt ? "certificados com alerta" : "certificates flagged", "0", CorOk),
                    new CopilotEvidence(pt ? "ativos com TLS verificado" : "assets with TLS checked", comTls.ToString(), CorNeutra),
                ]);
        }

        var expirados = certificados.Count(c => c.TituloPt == "Certificado TLS expirado");
        return new CopilotPrompt(pergunta,
            pt
                ? $"Sim — {certificados.Count} ativo(s) com problema de certificado, sendo {expirados} já expirado(s). Afeta: {string.Join(", ", certificados.Select(c => c.AssetNome).Distinct())}."
                : $"Yes — {certificados.Count} asset(s) with certificate issues, {expirados} already expired. Affected: {string.Join(", ", certificados.Select(c => c.AssetNome).Distinct())}.",
            certificados.Take(3)
                .Select(c => new CopilotEvidence(c.AssetNome,
                    c.TituloPt == "Certificado TLS expirado" ? (pt ? "expirado" : "expired") : (pt ? "expirando" : "expiring"),
                    c.TituloPt == "Certificado TLS expirado" ? CorCritica : CorAlta))
                .ToArray());
    }

    private static CopilotPrompt UltimaVarredura(List<Asset> ativos, bool pt)
    {
        var pergunta = pt ? "Quando foi a última varredura?" : "When was the last scan?";
        if (ativos.Count == 0)
        {
            return SemAtivo(pergunta, pt);
        }

        var escaneados = ativos.Where(a => a.UltimoScanEm.HasValue).ToList();
        var nunca = ativos.Count - escaneados.Count;

        if (escaneados.Count == 0)
        {
            return new CopilotPrompt(pergunta,
                pt
                    ? $"Nenhum dos {ativos.Count} ativo(s) cadastrado(s) foi escaneado ainda. Use \"Escanear agora\" na tela de Ativos para gerar o primeiro resultado."
                    : $"None of the {ativos.Count} registered asset(s) has been scanned yet. Use \"Scan now\" on the Assets page to produce the first result.",
                [new CopilotEvidence(pt ? "nunca escaneados" : "never scanned", ativos.Count.ToString(), CorAlta)]);
        }

        var ultima = escaneados.Max(a => a.UltimoScanEm!.Value).ToLocalTime();
        return new CopilotPrompt(pergunta,
            pt
                ? $"A varredura mais recente foi em {ultima:dd/MM/yyyy 'às' HH:mm}, cobrindo {escaneados.Count} de {ativos.Count} ativo(s) monitorado(s)."
                  + (nunca > 0 ? $" {nunca} ativo(s) ainda nunca foi(ram) escaneado(s)." : "")
                : $"The most recent scan ran on {ultima:yyyy-MM-dd 'at' HH:mm}, covering {escaneados.Count} of {ativos.Count} monitored asset(s)."
                  + (nunca > 0 ? $" {nunca} asset(s) have never been scanned." : ""),
            [
                new CopilotEvidence(pt ? "última varredura" : "last scan", ultima.ToString("dd/MM HH:mm"), CorNeutra),
                new CopilotEvidence(pt ? "ativos cobertos" : "assets covered", $"{escaneados.Count}/{ativos.Count}", nunca > 0 ? CorAtencao : CorOk),
            ]);
    }

    private static CopilotPrompt Portas(List<Asset> ativos, List<Vulnerability> achados, bool pt)
    {
        var pergunta = pt ? "Temos alguma porta exposta na internet?" : "Do we have any port exposed to the internet?";
        if (ativos.Count == 0)
        {
            return SemAtivo(pergunta, pt);
        }

        var portas = achados.Where(a => a.CategoriaScan == SecurityScanService.CategoriaPortas).ToList();
        var verificadas = SecurityScanService.PortasComuns.Length;

        if (portas.Count == 0)
        {
            return new CopilotPrompt(pergunta,
                pt
                    ? $"Nenhuma das {verificadas} portas administrativas e de banco de dados verificadas respondeu nos ativos monitorados."
                    : $"None of the {verificadas} administrative and database ports checked responded on the monitored assets.",
                [
                    new CopilotEvidence(pt ? "portas abertas" : "open ports", "0", CorOk),
                    new CopilotEvidence(pt ? "portas verificadas" : "ports checked", verificadas.ToString(), CorNeutra),
                ]);
        }

        return new CopilotPrompt(pergunta,
            pt
                ? $"Sim — {portas.Count} porta(s) sensível(is) respondendo pela internet em: {string.Join(", ", portas.Select(p => p.AssetNome).Distinct())}. Serviço administrativo ou de banco exposto é caminho direto para ataque de força bruta."
                : $"Yes — {portas.Count} sensitive port(s) responding from the internet on: {string.Join(", ", portas.Select(p => p.AssetNome).Distinct())}. An exposed admin or database service is a direct path for brute-force attacks.",
            portas.Take(3).Select(p => new CopilotEvidence(p.AssetNome, p.TituloPt, CorCritica)).ToArray());
    }

    private static CopilotPrompt SemAtivo(string pergunta, bool pt) => new(pergunta,
        pt
            ? "Esta empresa ainda não tem nenhum ativo real cadastrado, então não há dado coletado para responder. Cadastre um domínio autorizado na tela de Ativos para começar o monitoramento."
            : "This company has no real asset registered yet, so there is no collected data to answer from. Register an authorized domain on the Assets page to start monitoring.",
        [new CopilotEvidence(pt ? "ativos monitorados" : "monitored assets", "0", CorAtencao)]);
}
