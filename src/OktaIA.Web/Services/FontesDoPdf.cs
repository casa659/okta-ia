using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace OktaIA.Web.Services;

/// <summary>
/// As fontes dos PDFs, embutidas no próprio assembly.
///
/// 🔴 POR QUE ISTO EXISTE (12/10/2026). Os PDFs pediam <c>Fonts.Calibri</c> e
/// <c>Fonts.CourierNew</c>. Essas fontes são da Microsoft e vêm instaladas no App Service
/// **Windows** — funcionava por acidente de plataforma, não por decisão. Num contêiner Linux
/// elas não existem, e o QuestPDF cai numa fonte qualquer (layout deslocado, quebra de linha
/// diferente, tabela desalinhada) ou lança exceção por não achar a família. Descoberto ao
/// avaliar a migração dos planos Windows para Linux, que custam ~4,6x menos.
///
/// ⚠️ EMBUTIR, E NÃO INSTALAR NA IMAGEM. Depender de fonte instalada no sistema devolve o mesmo
/// problema por outra porta: o PDF passa a mudar conforme a imagem base, e a diferença só
/// aparece no documento que já foi enviado para alguém.
///
/// ⚠️ Fontes LIVRES, de propósito. Redistribuir Calibri ou Consolas dentro de um contêiner é
/// questão de licenciamento da Microsoft, não só técnica. Lato (OFL) já vem dentro do QuestPDF;
/// JetBrains Mono (OFL-1.1) está em <c>Fontes/</c>, com a licença ao lado.
/// </summary>
public static class FontesDoPdf
{
    /// <summary>Texto corrido. Vem embarcada no QuestPDF — não precisa registrar.</summary>
    public const string Texto = "Lato";

    /// <summary>
    /// Serifada. Hoje aponta para a mesma do texto: não há serifada livre embarcada, e inventar
    /// uma dependência nova para um único trecho não se paga. Trocar aqui vale para o projeto todo.
    /// </summary>
    public const string Serifada = "Lato";

    /// <summary>
    /// Monoespaçada — códigos, hashes e números de documento.
    ///
    /// ⚠️ JetBrains Mono foi escolhida por distinguir <c>0</c> de <c>O</c> e <c>1</c> de <c>l</c>:
    /// esses trechos existem para alguém conferir caractere a caractere, às vezes em 6,5pt.
    /// </summary>
    public const string Mono = "JetBrains Mono";

    /// <summary>
    /// Fonte de RESERVA, para o glifo que a fonte principal nao tem.
    ///
    /// 🔴 POR QUE (12/10/2026). Os documentos usam simbolos de desenho — seta, marca de
    /// confirmacao, triangulo de alerta, circulo de pendente — e o Lato nao cobre nenhum deles.
    /// Quando falta o glifo, o motor sai procurando fonte NO SISTEMA: no App Service Windows
    /// achava alguma, no conteiner Linux achou DejaVu, e numa imagem enxuta nao acharia nada.
    /// Medido: o PDF de diagnostico saiu do Linux com DejaVuSerif embutida sem ninguem pedir.
    ///
    /// ⚠️ A ALTERNATIVA ERA TROCAR OS SIMBOLOS por ASCII, e seria pior: sao 15 ocorrencias nos
    /// tres sistemas, todas deliberadas. Trocar ✓ por "v" e ⚠ por "!" degrada documento que vai
    /// para titular e para paciente.
    /// </summary>
    public const string Reserva = "DejaVu Sans";

    private static bool _registradas;
    private static readonly Lock _trava = new();

    /// <summary>
    /// Registra as fontes embutidas. Chamar uma vez no start da aplicação.
    ///
    /// ⚠️ Idempotente e com trava: o QuestPDF guarda as fontes num estado global, e registrar o
    /// mesmo arquivo duas vezes em paralelo já deu briga em outras bases.
    /// </summary>
    public static void Registrar()
    {
        if (_registradas) { return; }

        lock (_trava)
        {
            if (_registradas) { return; }

            var assembly = typeof(FontesDoPdf).Assembly;

            foreach (var recurso in assembly.GetManifestResourceNames()
                         .Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
            {
                using var fluxo = assembly.GetManifestResourceStream(recurso);
                if (fluxo is null) { continue; }


                FontManager.RegisterFont(fluxo);
            }

            // 🔴 FECHA A PORTA DO SISTEMA (12/10/2026). Sem isto, glifo ausente faz o motor
            // procurar fonte instalada na maquina — e o documento passa a depender da imagem onde
            // roda, mudando sozinho entre Windows e Linux. Com `false`, so as fontes registradas
            // acima existem, e o PDF fica identico em qualquer ambiente.
            QuestPDF.Settings.UseEnvironmentFonts = false;

            _registradas = true;
        }
    }

    /// <summary>
    /// Liga a fonte de reserva neste estilo de texto. Usar no DefaultTextStyle de cada pagina;
    /// os estilos aninhados herdam.
    ///
    /// 🔴 A CHAMADA OBSOLETA MORA AQUI, DE PROPOSITO (12/10/2026). O QuestPDF marca
    /// <c>Fallback</c> como obsoleto e manda usar <c>FontFamilyFallback</c> ou "o novo
    /// mecanismo automatico". MEDIDO na versao 2026.7.2, com PDF gerado e fontes conferidas
    /// dentro do arquivo:
    ///   • <c>FontFamilyFallback</c> NAO EXISTE (erro CS1061);
    ///   • <c>FontFamily("Lato", "DejaVu Sans")</c> compila e NAO tem efeito;
    ///   • o mecanismo automatico pega ⚠ e ▲, mas NAO pega ✓ — e o ✓ some do documento,
    ///     sem erro nenhum, num relatorio que diz "verificado".
    /// So <c>Fallback</c> funciona. Centralizado num metodo so para a supressao do aviso ficar
    /// em UM lugar, com o motivo junto, em vez de 30 pragmas espalhados.
    /// </summary>
    public static TextStyle ComReserva(this TextStyle estilo)
    {
    #pragma warning disable CS0618 // ver o porque acima
        return estilo.Fallback(r => r.FontFamily(Reserva));
    #pragma warning restore CS0618
    }
}
