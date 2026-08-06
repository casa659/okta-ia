using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

// Cores/rótulos de Severidade — usado por Dashboard (fluxo de eventos) e Incidentes (fila/
// detalhe), extraído aqui pra não duplicar o mesmo switch em cada PageModel.
public static class SeverityStyle
{
    public static string Label(Severidade s, string lang) => (s, lang) switch
    {
        (Severidade.Critica, "pt") => "CRÍTICO",
        (Severidade.Critica, _) => "CRITICAL",
        (Severidade.Alta, "pt") => "ALTO",
        (Severidade.Alta, _) => "HIGH",
        (Severidade.Media, "pt") => "MÉDIO",
        (Severidade.Media, _) => "MEDIUM",
        (_, "pt") => "BAIXO",
        _ => "LOW",
    };

    public static string Cor(Severidade s) => s switch
    {
        Severidade.Critica => "#FF3B5C",
        Severidade.Alta => "#FF8A3D",
        Severidade.Media => "#FFC93C",
        _ => "#4D9BFF",
    };

    public static string Fundo(Severidade s) => s switch
    {
        Severidade.Critica => "#2A0D14",
        Severidade.Alta => "#2A1608",
        Severidade.Media => "#2A2208",
        _ => "#0C1B2E",
    };
}
