using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages;

/// <summary>
/// Catálogo de relatórios. Hoje só UM existe de verdade — o do scanner, que é gerado na tela de
/// Vulnerabilidades. Os demais são planejados.
///
/// Esta tela já exibiu quatro indicadores escritos à mão ("3,84 M eventos bloqueados", "7 incidentes
/// contidos", "168 de dívida de vulns", "risco residual Médio") e datas de "última geração" de
/// relatórios que nunca foram gerados. Numa conversa sobre conformidade — que é exatamente o assunto
/// desta tela — o cliente pergunta de onde vem cada número, e número inventado destrói a confiança em
/// tudo o mais. Agora os indicadores vêm de consulta, e o que não é medível não aparece.
/// </summary>
[Authorize]
public class RelatoriosModel : PageModel
{
    private readonly I18nService _i18n;
    private readonly ApplicationDbContext _db;

    public RelatoriosModel(I18nService i18n, ApplicationDbContext db)
    {
        _i18n = i18n;
        _db = db;
    }

    public record ReportView(string Nome, string Formato, string CorFormato, string FundoFormato,
        string Frequencia, string Descricao, string Ultimo, bool Disponivel);
    public record ExecStatView(string Chave, string Valor, string Cor);

    public List<ReportView> Reports { get; private set; } = [];
    public List<ExecStatView> ExecStats { get; private set; } = [];
    public string? EmpresaNome { get; private set; }
    public bool SemDadosReais { get; private set; }

    // Resumo do período. Antes era um parágrafo fixo em Translations, com selo de "IA", afirmando
    // "postura melhorou 12%", "3,84 milhões de eventos bloqueados" e CVEs do catálogo CISA que nunca
    // foram medidos. Agora é frase montada a partir do que está no banco — e quando não há dado,
    // diz isso, em vez de preencher o espaço.
    public string PeriodoRotulo { get; private set; } = "";
    public string ResumoTitulo { get; private set; } = "";
    public string ResumoTexto { get; private set; } = "";

    public async Task OnGetAsync()
    {
        var lang = _i18n.Lang;
        var pt = lang == "pt";
        const string accent = "#00E0A4";

        var empresa = await TenantResolver.ResolverAtualAsync(HttpContext, _db);
        EmpresaNome = empresa?.Nome;

        var agora = DateTimeOffset.UtcNow.ToLocalTime();
        PeriodoRotulo = (pt ? "PERÍODO · " : "PERIOD · ") +
            agora.ToString("MMMM yyyy", new System.Globalization.CultureInfo(pt ? "pt-BR" : "en-US")).ToUpperInvariant();

        Reports =
        [
            // O único que existe: gerado pelo RelatorioPdfService a partir dos achados do scanner.
            new(pt ? "Achados do scanner de superfície" : "Surface scanner findings", "PDF", accent, "#0F2E26",
                pt ? "SOB DEMANDA" : "ON DEMAND",
                pt ? "Achados reais por ativo, com risco de negócio e passo a passo de correção. Gerado na tela de Vulnerabilidades."
                   : "Real findings per asset, with business risk and remediation steps. Generated on the Vulnerabilities screen.",
                pt ? "Disponível" : "Available", true),

            new(pt ? "Evidências técnicas ISO 27001 / LGPD" : "ISO 27001 / GDPR technical evidence", "PDF", "#4D9BFF", "#12283F",
                pt ? "TRIMESTRAL" : "QUARTERLY",
                pt ? "Controles que conseguimos evidenciar tecnicamente, o que não avaliamos e as lacunas. Não substitui auditoria."
                   : "Controls we can technically evidence, what we did not assess and the gaps. Does not replace an audit.",
                pt ? "Planejado" : "Planned", false),

            new(pt ? "Relatório executivo mensal" : "Monthly executive report", "PDF", "#FF8A3D", "#3A2410",
                pt ? "MENSAL" : "MONTHLY",
                pt ? "Postura de segurança, tendências e risco residual em linguagem de negócio."
                   : "Security posture, trends and residual risk in business language.",
                pt ? "Planejado" : "Planned", false),

            new(pt ? "Inventário de achados" : "Findings inventory", "XLSX", "#FFC93C", "#2A2208",
                pt ? "SEMANAL" : "WEEKLY",
                pt ? "Todos os achados abertos com severidade, ativo afetado e recomendação."
                   : "All open findings with severity, affected asset and recommendation.",
                pt ? "Planejado" : "Planned", false),
        ];

        if (empresa is null)
        {
            SemDadosReais = true;
            ResumoTitulo = pt ? "Sem empresa selecionada" : "No company selected";
            ResumoTexto = pt
                ? "Escolha uma organização no cabeçalho para ver o resumo do período."
                : "Pick an organization in the header to see the period summary.";
            return;
        }

        // Só conta o que é medido: achado de scanner (FonteScan) e ativo real monitorado.
        var achadosAbertos = await _db.Vulnerabilities
            .CountAsync(v => v.CompanyId == empresa.Id && v.FonteScan);
        var ativos = await _db.Assets.Where(a => a.CompanyId == empresa.Id && a.Real).ToListAsync();
        var monitorados = ativos.Count(a => a.MonitoramentoContinuo);
        var ultimaVarredura = ativos.Where(a => a.UltimoScanEm.HasValue)
            .OrderByDescending(a => a.UltimoScanEm).FirstOrDefault()?.UltimoScanEm;
        var alertas = await _db.AlertasUnificados.CountAsync(a => a.CompanyId == empresa.Id);

        SemDadosReais = ativos.Count == 0;

        ExecStats =
        [
            new(pt ? "Ativos monitorados" : "Monitored assets",
                ativos.Count == 0 ? "—" : $"{monitorados}/{ativos.Count}",
                monitorados > 0 ? accent : "#4A5D78"),

            new(pt ? "Achados abertos" : "Open findings",
                ativos.Count == 0 ? "—" : achadosAbertos.ToString("N0"),
                achadosAbertos > 0 ? "#FF8A3D" : accent),

            new(pt ? "Alertas ingeridos" : "Ingested alerts",
                alertas.ToString("N0"), alertas > 0 ? "#4D9BFF" : "#4A5D78"),

            new(pt ? "Última varredura" : "Last scan",
                ultimaVarredura?.ToLocalTime().ToString("dd/MM HH:mm") ?? (pt ? "nunca" : "never"),
                ultimaVarredura is null ? "#4A5D78" : accent),
        ];

        MontarResumo(pt, empresa.Nome, ativos.Count, monitorados, achadosAbertos, alertas, ultimaVarredura);
    }

    /// <summary>
    /// Monta o resumo com o que foi medido. Cada frase só entra se houver dado que a sustente —
    /// é o mesmo princípio do assistente da tela de IA: preferir dizer "não avaliado" a preencher.
    /// </summary>
    private void MontarResumo(bool pt, string empresa, int ativos, int monitorados,
        int achados, int alertas, DateTimeOffset? ultimaVarredura)
    {
        if (ativos == 0)
        {
            ResumoTitulo = pt ? "Nenhum ativo cadastrado" : "No assets registered";
            ResumoTexto = pt
                ? $"{empresa} ainda não tem ativo real cadastrado, então não há postura de segurança medida. Cadastre um domínio em Ativos para a primeira varredura."
                : $"{empresa} has no real asset registered yet, so there is no measured security posture. Register a domain under Assets for the first scan.";
            return;
        }

        var partes = new List<string>();

        partes.Add(pt
            ? $"{empresa} tem {ativos} ativo(s) real(is) cadastrado(s), {monitorados} com varredura recorrente."
            : $"{empresa} has {ativos} real asset(s) registered, {monitorados} under recurring scanning.");

        partes.Add(achados == 0
            ? (pt ? "Nenhum achado aberto do scanner de superfície externa."
                  : "No open findings from the external surface scanner.")
            : (pt ? $"São {achados} achado(s) aberto(s) do scanner de superfície externa."
                  : $"There are {achados} open finding(s) from the external surface scanner."));

        if (alertas > 0)
        {
            partes.Add(pt
                ? $"As ferramentas integradas do cliente trouxeram {alertas} alerta(s)."
                : $"The client's integrated tools brought in {alertas} alert(s).");
        }
        else
        {
            partes.Add(pt
                ? "Nenhum conector instalado, então não há alerta vindo das ferramentas do cliente."
                : "No connector installed, so there are no alerts from the client's own tools.");
        }

        if (ultimaVarredura is not null)
        {
            partes.Add(pt
                ? $"A última varredura foi em {ultimaVarredura.Value.ToLocalTime():dd/MM/yyyy 'às' HH:mm}."
                : $"The last scan ran on {ultimaVarredura.Value.ToLocalTime():yyyy-MM-dd 'at' HH:mm}.");
        }

        ResumoTitulo = monitorados == ativos && ativos > 0
            ? (pt ? "Todos os ativos sob varredura recorrente" : "All assets under recurring scanning")
            : (pt ? "Parte dos ativos ainda sem varredura recorrente" : "Some assets still without recurring scanning");

        ResumoTexto = string.Join(" ", partes);
    }
}
