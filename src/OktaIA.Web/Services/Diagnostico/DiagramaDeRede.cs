using System.Globalization;
using System.Text;

namespace OktaIA.Web.Services.Diagnostico;

/// <summary>Paleta do desenho. A tela é escura; o PDF é branco.</summary>
public enum TemaDoDiagrama { Escuro, Claro }

/// <summary>
/// Desenha o ambiente do cliente como um diagrama de rede — caixas com ícone e linhas ortogonais —
/// em vez de uma régua de percentuais.
///
/// Serve a uma conversa específica: quem decide orçamento entende um desenho da própria empresa com
/// duas caixas vermelhas muito mais rápido do que entende "cobertura 63%". O número diz o tamanho
/// do problema; o desenho diz onde ele está.
///
/// Sai como SVG e não como imagem pronta por um motivo prático: o mesmo texto serve à tela (SVG
/// embutido) e ao PDF (QuestPDF renderiza SVG). Dois desenhos gerados por caminhos diferentes
/// divergiriam na primeira alteração.
///
/// ⚠️ Os ícones são geometria pura — retângulo, círculo, caminho. Nada de emoji: eles dependem de
/// fonte instalada, e o que aparece bonito no navegador vira quadrado vazio no PDF.
/// </summary>
public static class DiagramaDeRede
{
    /// <summary>Posição da caixa de cada camada. Topo-esquerda, em coordenadas do viewBox.</summary>
    private static readonly Dictionary<string, (int X, int Y)> Posicoes = new()
    {
        ["internet"] = (10, 176),
        ["firewall"] = (180, 176),
        ["cloud"] = (350, 40),
        ["rede"] = (350, 176),
        ["servidores"] = (560, 60),
        ["endpoints"] = (560, 150),
        ["aplicacoes"] = (560, 240),
        ["identidades"] = (560, 330),
        ["backup"] = (760, 60),
        ["monitoramento"] = (760, 330),
    };

    private const int L = 130;   // largura da caixa
    private const int A = 58;    // altura da caixa

    public static string Gerar(List<CamadaDaArquitetura> mapa, TemaDoDiagrama tema = TemaDoDiagrama.Escuro)
    {
        var escuro = tema == TemaDoDiagrama.Escuro;
        var corTexto = escuro ? "#EAF0F8" : "#1C2836";
        var corLinha = escuro ? "#3E5273" : "#C2CDDC";
        var corFundoCaixa = escuro ? "#0B1220" : "#FFFFFF";
        var corLegenda = escuro ? "#7A8FAB" : "#6B7C93";

        var porCodigo = mapa.ToDictionary(c => c.Codigo);
        var sb = new StringBuilder();

        sb.Append("<svg viewBox=\"0 0 900 405\" xmlns=\"http://www.w3.org/2000/svg\" ")
          .Append("style=\"width:100%;height:auto;\" font-family=\"Segoe UI, Helvetica, Arial, sans-serif\">");

        // ── Ligações, desenhadas antes para ficarem atrás das caixas ──
        // Ortogonais de propósito: é o traço que faz o desenho ser lido como topologia de rede, e
        // não como um fluxograma qualquer.
        sb.Append($"<g fill=\"none\" stroke=\"{corLinha}\" stroke-width=\"1.6\">");
        sb.Append(Linha("M75,205 H245"));                          // internet → firewall
        sb.Append(Linha("M245,205 H415"));                         // firewall → rede
        sb.Append(Linha("M415,205 V98"));                          // rede → nuvem
        sb.Append(Linha("M415,205 H500 V89 H560"));                // rede → servidores
        sb.Append(Linha("M415,205 H500 V179 H560"));               // rede → endpoints
        sb.Append(Linha("M415,205 H500 V269 H560"));               // rede → aplicações
        sb.Append(Linha("M415,205 H500 V359 H560"));               // rede → identidades
        sb.Append(Linha("M690,89 H760"));                          // servidores → backup
        // O SIEM não fica "depois" de nada: ele observa. Linha tracejada, saindo por baixo da rede.
        sb.Append("<path d=\"M415,234 V392 H825 V388\" stroke-dasharray=\"4 4\" />");
        sb.Append("</g>");

        // Rótulo da linha tracejada, para ninguém ler como caminho de tráfego. Fica na folga entre
        // a caixa de Identidades (termina em x=690) e a de SIEM (começa em x=760) — em x=600 ele
        // caía ATRÁS da caixa de Identidades.
        sb.Append($"<text x=\"695\" y=\"387\" font-size=\"8\" fill=\"{corLegenda}\">coleta de logs</text>");

        // ── Caixas ──
        foreach (var (codigo, pos) in Posicoes)
        {
            if (!porCodigo.TryGetValue(codigo, out var camada)) { continue; }

            var cor = MapaDaArquitetura.Cor(camada.Estado);
            var indefinida = camada.Estado is EstadoDaCamada.NaoAvaliado or EstadoDaCamada.NaoSeAplica;

            sb.Append($"<g transform=\"translate({pos.X},{pos.Y})\">");

            // Camada sem avaliação fica tracejada: a borda diz "não sabemos" antes de qualquer texto.
            sb.Append($"<rect width=\"{L}\" height=\"{A}\" rx=\"9\" fill=\"{corFundoCaixa}\" ")
              .Append($"stroke=\"{cor}\" stroke-width=\"{(indefinida ? "1.2" : "1.8")}\"")
              .Append(indefinida ? " stroke-dasharray=\"5 4\"" : "")
              .Append(" />");

            // Faixa de estado no topo da caixa, como um cabeçalho colorido.
            if (!indefinida)
            {
                sb.Append($"<path d=\"M9,0 H{L - 9} A9,9 0 0 1 {L},9 V12 H0 V9 A9,9 0 0 1 9,0 Z\" fill=\"{cor}\" />");
            }

            sb.Append($"<g transform=\"translate(13,24)\" fill=\"none\" stroke=\"{cor}\" ")
              .Append("stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\">")
              .Append(Icone(codigo))
              .Append("</g>");

            sb.Append($"<text x=\"42\" y=\"32\" font-size=\"12.5\" font-weight=\"600\" fill=\"{corTexto}\">")
              .Append(Escapar(camada.Nome)).Append("</text>");

            // Sobre fundo branco a cor viva do estado fica ilegível (amarelo em papel some).
            // A borda e a faixa mantêm a cor forte; o texto ganha uma versão escurecida.
            var corRotulo = escuro ? cor : CorLegivelNoBranco(camada.Estado);

            sb.Append($"<text x=\"42\" y=\"46\" font-size=\"7.5\" letter-spacing=\"0.6\" fill=\"{corRotulo}\">")
              .Append(MapaDaArquitetura.Rotulo(camada.Estado));

            if (!indefinida)
            {
                sb.Append("  ").Append(camada.Cobertura.ToString(CultureInfo.InvariantCulture)).Append('%');
            }
            sb.Append("</text>");

            sb.Append("</g>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string Linha(string d) => $"<path d=\"{d}\" />";

    /// <summary>
    /// Versão escurecida da cor do estado, para texto sobre fundo branco. As cores da interface
    /// foram escolhidas para brilhar num fundo escuro — no papel, o amarelo simplesmente some.
    /// </summary>
    private static string CorLegivelNoBranco(EstadoDaCamada estado) => estado switch
    {
        EstadoDaCamada.Protegido => "#00875F",
        EstadoDaCamada.Parcial => "#8A6D00",
        EstadoDaCamada.Descoberto => "#D01B3C",
        _ => "#6B7C93",
    };

    /// <summary>
    /// Ícone de cada camada, em geometria simples dentro de uma caixa de 20×20 com a origem no
    /// canto superior esquerdo do glifo.
    /// </summary>
    private static string Icone(string codigo) => codigo switch
    {
        // Globo
        "internet" => "<circle cx=\"9\" cy=\"0\" r=\"8\" /><path d=\"M1,0 H17 M9,-8 C5,-4 5,4 9,8 M9,-8 C13,-4 13,4 9,8\" />",

        // Escudo
        "firewall" => "<path d=\"M9,-8 L16,-5 V1 C16,5 13,8 9,9 C5,8 2,5 2,1 V-5 Z\" /><path d=\"M2,-1 H16\" />",

        // Nuvem
        "cloud" => "<path d=\"M4,4 A4,4 0 0 1 5,-3 A5,5 0 0 1 14,-2 A3.5,3.5 0 0 1 14,4 Z\" />",

        // Switch: caixa com portas
        "rede" => "<rect x=\"1\" y=\"-5\" width=\"16\" height=\"9\" rx=\"1.5\" /><path d=\"M4,4 V7 M9,4 V7 M14,4 V7\" />",

        // Rack: três lâminas empilhadas
        "servidores" => "<rect x=\"2\" y=\"-8\" width=\"14\" height=\"5\" rx=\"1\" /><rect x=\"2\" y=\"-1.5\" width=\"14\" height=\"5\" rx=\"1\" /><path d=\"M5,-5.5 H5.01 M5,1 H5.01\" />",

        // Notebook
        "endpoints" => "<rect x=\"3\" y=\"-6\" width=\"13\" height=\"9\" rx=\"1\" /><path d=\"M1,6 H18\" />",

        // Janela de aplicação
        "aplicacoes" => "<rect x=\"2\" y=\"-6\" width=\"15\" height=\"12\" rx=\"1.5\" /><path d=\"M2,-2 H17 M5,-4 H5.01 M8,-4 H8.01\" />",

        // Pessoa
        "identidades" => "<circle cx=\"9\" cy=\"-3\" r=\"4\" /><path d=\"M2,7 C2,2 5.5,0 9,0 C12.5,0 16,2 16,7\" />",

        // Cilindro de dados
        "backup" => "<ellipse cx=\"9\" cy=\"-6\" rx=\"7\" ry=\"2.6\" /><path d=\"M2,-6 V4 C2,5.4 5.1,6.6 9,6.6 C12.9,6.6 16,5.4 16,4 V-6\" /><path d=\"M2,-1 C2,0.4 5.1,1.6 9,1.6 C12.9,1.6 16,0.4 16,-1\" />",

        // Barras de monitoramento
        "monitoramento" => "<path d=\"M2,6 V-1 M7,6 V-6 M12,6 V-3 M17,6 V-8\" stroke-width=\"1.8\" />",

        _ => "<circle cx=\"9\" cy=\"0\" r=\"7\" />",
    };

    private static string Escapar(string texto) =>
        texto.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
