using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Diagnostico;

/// <summary>
/// Os rótulos e cores dos enums do diagnóstico, num lugar só.
///
/// Nasceram dentro do PageModel da tela de resultado, o que funcionou enquanto só a tela precisava
/// deles. Quando o PDF passou a precisar dos mesmos textos, manter ali obrigaria um serviço a
/// depender de uma página — e a alternativa (copiar) faria a tela e o documento discordarem na
/// primeira alteração, com o cliente vendo "NÃO TEM" na reunião e outra palavra no papel.
///
/// ⚠️ "declarado pelo cliente" não é enfeite. A distinção entre declarado, evidenciado e validado é
/// a espinha do módulo inteiro: se estes textos forem suavizados, o documento passa a afirmar como
/// medido aquilo que foi apenas conversado.
/// </summary>
public static class RotulosDoDiagnostico
{
    public static string Origem(OrigemDaInformacao o) => o switch
    {
        OrigemDaInformacao.Declarado => "declarado pelo cliente",
        OrigemDaInformacao.Evidenciado => "com evidência anexada",
        OrigemDaInformacao.Validado => "validado tecnicamente",
        OrigemDaInformacao.NaoAplicavel => "não se aplica",
        _ => "não avaliado",
    };

    public static string Situacao(SituacaoDoControle s) => s switch
    {
        SituacaoDoControle.Tem => "TEM",
        SituacaoDoControle.Parcial => "PARCIAL",
        SituacaoDoControle.NaoTem => "NÃO TEM",
        SituacaoDoControle.NaoAplicavel => "N/A",
        _ => "—",
    };

    public static string CorDaSituacao(SituacaoDoControle s) => s switch
    {
        SituacaoDoControle.Tem => "#00E0A4",
        SituacaoDoControle.Parcial => "#F5D547",
        SituacaoDoControle.NaoTem => "#FF3B5C",
        _ => "#5A7191",
    };

    public static string CorDaGravidade(GravidadeRisco g) => g switch
    {
        GravidadeRisco.Critico => "#FF3B5C",
        GravidadeRisco.Alto => "#FF8A3D",
        GravidadeRisco.Medio => "#F5D547",
        _ => "#7A8FAB",
    };

    /// <summary>
    /// Versão escurecida para papel branco. As cores da interface foram escolhidas para brilhar em
    /// fundo escuro; o amarelo, em especial, some numa folha impressa.
    /// </summary>
    public static string ParaImpressao(string cor) => cor switch
    {
        "#F5D547" => "#8A6D00",
        "#00E0A4" => "#00755A",
        "#FF8A3D" => "#B54708",
        "#7A8FAB" => "#5A7191",
        _ => cor,
    };
}
