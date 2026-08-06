using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

// Agrega os achados reais (FonteScan=true) de uma empresa num placar único — mesma fórmula de
// peso por severidade já usada por asset em AssetScoreCalculator, só que somada na empresa
// inteira, pra não ter duas contas diferentes pro que é "gravidade" de um achado.
public static class CompanySecurityScoreCalculator
{
    public record Resultado(
        int Score, string Classificacao,
        string RiscoLabelPt, string RiscoLabelEn, string RiscoCor, string RiscoEmoji,
        string SuperficiePt, string SuperficieEn,
        string PhishingPt, string PhishingEn);

    public static Resultado Calcular(List<Vulnerability> achadosReais, int ativosReaisCount, int portasAbertasCount)
    {
        var criticas = achadosReais.Count(a => a.Severidade == Severidade.Critica);
        var altas = achadosReais.Count(a => a.Severidade == Severidade.Alta);
        var medias = achadosReais.Count(a => a.Severidade == Severidade.Media);
        var baixas = achadosReais.Count(a => a.Severidade == Severidade.Baixa);

        var score = Math.Clamp(100 - (criticas * 25 + altas * 12 + medias * 5 + baixas * 2), 0, 100);

        var classificacao = score switch
        {
            >= 90 => "A",
            >= 80 => "B+",
            >= 70 => "B",
            >= 55 => "C+",
            >= 40 => "C",
            _ => "D",
        };

        var (riscoPt, riscoEn, riscoCor, riscoEmoji) = score switch
        {
            >= 80 => ("Baixo", "Low", "#00E0A4", "🟢"),
            >= 50 => ("Médio", "Medium", "#FFC93C", "🟡"),
            _ => ("Alto", "High", "#FF3B5C", "🔴"),
        };

        // Superfície de ataque: número de pontos de entrada expostos (ativos reais + portas
        // abertas encontradas), não o número de achados — um ativo com 1 porta aberta tem menos
        // superfície que 5 ativos mesmo que ambos tenham a mesma quantidade de achados.
        var pontosExpostos = ativosReaisCount + portasAbertasCount;
        var (superficiePt, superficieEn) = pontosExpostos switch
        {
            <= 1 => ("Baixa", "Low"),
            <= 4 => ("Média", "Medium"),
            _ => ("Alta", "High"),
        };

        // Risco de phishing: só olha os dois achados de e-mail que realmente checamos
        // (SPF/DMARC ausentes) — nunca infere risco de phishing de nada além disso.
        var faltaSpf = achadosReais.Any(a => a.TituloPt == "Registro SPF ausente");
        var faltaDmarc = achadosReais.Any(a => a.TituloPt == "Registro DMARC ausente");
        var (phishingPt, phishingEn) = (faltaSpf, faltaDmarc) switch
        {
            (true, true) => ("Alto", "High"),
            (true, false) or (false, true) => ("Médio", "Medium"),
            _ => ("Baixo", "Low"),
        };

        return new Resultado(score, classificacao, riscoPt, riscoEn, riscoCor, riscoEmoji, superficiePt, superficieEn, phishingPt, phishingEn);
    }

    // Estimativa grosseira de esforço — é config de DNS/servidor, não desenvolvimento; serve só
    // pra dar uma noção de prioridade "rápido de resolver" na narrativa de IA, nunca aparece como
    // compromisso de SLA.
    public static int TempoEstimadoMinutos(Vulnerability achado) => achado.CategoriaScan switch
    {
        SecurityScanService.CategoriaDns => 20,
        SecurityScanService.CategoriaHeaders => 15,
        SecurityScanService.CategoriaTls => achado.TituloPt.Contains("expirad") ? 30 : 15,
        SecurityScanService.CategoriaPortas => 30,
        _ => 30,
    };

    // Prioridade da semana: maior severidade primeiro; empatado, o mais rápido de resolver vence
    // (mais fácil de fechar logo, mantém o time motivado em vez de travar num item difícil).
    public static Vulnerability? PrioridadeDaSemana(List<Vulnerability> achadosReais) => achadosReais
        .OrderByDescending(a => a.Severidade)
        .ThenBy(TempoEstimadoMinutos)
        .FirstOrDefault();
}
