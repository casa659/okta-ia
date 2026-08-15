using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Diagnostico;

/// <summary>
/// Lê um diagnóstico preenchido e escreve a interpretação. Contrato separado da implementação pelo
/// mesmo motivo do envio de e-mail no LinkEscola: sem chave configurada o serviço **recusa operar**
/// em vez de fingir que analisou.
/// </summary>
public interface IAnalisadorDeDiagnostico
{
    /// <summary>Há chave de API configurada. A tela usa isto para avisar antes de o botão falhar.</summary>
    bool Configurado { get; }

    Task<DiagnosticoAnalise> AnalisarAsync(
        Models.Diagnostico diagnostico,
        ResultadoDoDiagnostico resultado,
        string solicitadoPor,
        CancellationToken ct = default);
}

/// <summary>
/// Análise por modelo de linguagem (Claude).
///
/// Três decisões que valem a leitura:
///
/// 1. **A recusa é caminho esperado, não exceção.** O modelo roda classificadores de segurança e
///    pode declinar uma requisição — devolvendo HTTP 200 com `stop_reason: "refusal"`, não erro.
///    Este produto vive inteiro no domínio que esses classificadores vigiam, então ler
///    `Content[0]` direto quebraria em produção, na frente do cliente. Quando acontece, tentamos
///    uma vez num modelo de fallback e, se ele também recusar, gravamos a recusa com a categoria —
///    em vez de mostrar erro genérico.
///
/// 2. **O prefixo é estável de propósito.** O catálogo de domínios é idêntico entre clientes e
///    ocupa a maior parte da entrada; fica no bloco de sistema com marca de cache e passa a custar
///    cerca de um décimo nas chamadas seguintes. Nada volátil (data, id, nome de empresa) pode
///    entrar antes dele: cache é casamento de prefixo, e um byte diferente invalida tudo à frente.
///
/// 3. **Saída estruturada em vez de pedir JSON no texto.** É o que faz a resposta chegar em campos
///    tratáveis em vez de prosa que ninguém consegue diagramar no PDF.
/// </summary>
public class AnalisadorDeDiagnostico : IAnalisadorDeDiagnostico
{
    /// <summary>Modelo principal. Trocar aqui muda o custo e invalida o cache de prefixo.</summary>
    private const string ModeloPrincipal = "claude-opus-5";

    /// <summary>
    /// Para onde a recusa é reencaminhada. Recusa de categoria "cyber" é justamente o caso que
    /// este modelo atende melhor.
    /// </summary>
    private const string ModeloDeFallback = "claude-opus-4-8";

    private readonly string? _apiKey;
    private readonly ILogger<AnalisadorDeDiagnostico> _log;

    public AnalisadorDeDiagnostico(IConfiguration config, ILogger<AnalisadorDeDiagnostico> log)
    {
        // Mesma origem das outras credenciais do projeto: App Setting no Azure, e o Key Vault por
        // cima quando disponível. Nunca em arquivo versionado.
        _apiKey = config["Anthropic:ApiKey"];
        _log = log;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<DiagnosticoAnalise> AnalisarAsync(
        Models.Diagnostico diagnostico,
        ResultadoDoDiagnostico resultado,
        string solicitadoPor,
        CancellationToken ct = default)
    {
        var analise = new DiagnosticoAnalise
        {
            DiagnosticoId = diagnostico.Id,
            GeradaPor = solicitadoPor,
        };

        if (!Configurado)
        {
            analise.Resultado = ResultadoAnalise.Falhou;
            analise.Erro = "Chave da API não configurada neste servidor.";
            return analise;
        }

        var relogio = Stopwatch.StartNew();
        var client = new AnthropicClient { ApiKey = _apiKey };

        try
        {
            var resposta = await ChamarAsync(client, ModeloPrincipal, diagnostico, resultado, ct);

            // ⚠️ Conferir o motivo da parada ANTES de ler o conteúdo. Numa recusa o conteúdo vem
            // vazio (ou parcial), e indexar direto seria a exceção que derruba a tela.
            if (EhRecusa(resposta))
            {
                var categoria = resposta.StopDetails?.Category?.ToString();
                _log.LogWarning("Análise recusada pelo modelo {Modelo}, categoria {Categoria}. Tentando fallback.",
                    ModeloPrincipal, categoria ?? "não informada");

                resposta = await ChamarAsync(client, ModeloDeFallback, diagnostico, resultado, ct);

                if (EhRecusa(resposta))
                {
                    analise.Resultado = ResultadoAnalise.Recusado;
                    analise.Modelo = ModeloDeFallback;
                    analise.MotivoRecusa = resposta.StopDetails?.Category?.ToString() ?? categoria;
                    analise.DuracaoMs = (int)relogio.ElapsedMilliseconds;
                    return analise;
                }
                analise.Modelo = ModeloDeFallback;
            }
            else
            {
                analise.Modelo = ModeloPrincipal;
            }

            var texto = PrimeiroTexto(resposta);
            if (string.IsNullOrWhiteSpace(texto))
            {
                analise.Resultado = ResultadoAnalise.Falhou;
                analise.Erro = "O modelo respondeu sem conteúdo.";
            }
            else
            {
                PreencherDoJson(analise, texto);
                analise.Resultado = ResultadoAnalise.Sucesso;
            }

            analise.TokensEntrada = (int?)resposta.Usage?.InputTokens;
            analise.TokensSaida = (int?)resposta.Usage?.OutputTokens;
            analise.TokensCacheLidos = (int?)resposta.Usage?.CacheReadInputTokens;
        }
        catch (Exception e)
        {
            // Falha de rede, cota ou credencial. Distinta de recusa: esta se resolve tentando de
            // novo, e a mensagem crua nunca vai para a tela do cliente.
            _log.LogError(e, "Falha ao analisar o diagnóstico {Id}", diagnostico.Id);
            analise.Resultado = ResultadoAnalise.Falhou;
            analise.Erro = e.Message;
        }

        analise.DuracaoMs = (int)relogio.ElapsedMilliseconds;
        return analise;
    }

    private static bool EhRecusa(Message resposta) =>
        string.Equals(resposta.StopReason?.ToString(), "refusal", StringComparison.OrdinalIgnoreCase);

    private static string? PrimeiroTexto(Message resposta)
    {
        foreach (var bloco in resposta.Content)
        {
            if (bloco.TryPickText(out TextBlock? texto)) { return texto.Text; }
        }
        return null;
    }

    private static async Task<Message> ChamarAsync(
        AnthropicClient client,
        string modelo,
        Models.Diagnostico diagnostico,
        ResultadoDoDiagnostico resultado,
        CancellationToken ct)
    {
        var parametros = new MessageCreateParams
        {
            Model = modelo,
            MaxTokens = 16000,
            // Prefixo estável primeiro, marcado para cache; o dado do cliente vai na mensagem.
            // `System` é uma união (texto simples OU lista de blocos) e a conversão implícita só
            // aceita o `List<TextBlockParam>` concreto — expressão de coleção não compila aqui.
            System = new List<TextBlockParam>
            {
                new() { Text = Instrucoes },
                new()
                {
                    Text = DescricaoDoCatalogo.Value,
                    CacheControl = new CacheControlEphemeral(),
                },
            },
            OutputConfig = new OutputConfig
            {
                Effort = Effort.High,
                Format = new JsonOutputFormat { Schema = Esquema.Value },
            },
            Messages =
            [
                new MessageParam { Role = Role.User, Content = MontarEntrada(diagnostico, resultado) },
            ],
        };

        return await client.Messages.Create(parametros, cancellationToken: ct);
    }

    // ── Prompt ───────────────────────────────────────────────────────────────

    /// <summary>
    /// As regras. Escritas como restrição do que NÃO se pode afirmar, porque o risco deste módulo
    /// não é o modelo escrever pouco — é ele escrever com uma confiança que o dado não sustenta.
    /// </summary>
    private const string Instrucoes = """
        Você analisa diagnósticos de segurança da informação de empresas brasileiras de pequeno e
        médio porte, para uma consultoria que opera segurança gerenciada.

        REGRA PRINCIPAL: quase tudo que você vai ler foi DECLARADO pelo cliente numa reunião, sem
        prova técnica. Trate assim. Nunca escreva como se tivesse medido. Quando uma informação vier
        marcada como evidenciada ou validada, você pode ser mais assertivo sobre ela — e só sobre ela.

        Nunca faça:
        - inventar percentual, estatística, comparação de mercado ou nome de norma que não esteja no
          material;
        - afirmar que a empresa está "em conformidade" com qualquer norma — o que existe aqui é
          relação com controles, não conformidade;
        - citar produto, fabricante ou preço que não apareça nas respostas;
        - transformar "não sei" em "não tem". Não saber é um achado próprio: significa que ninguém
          acompanha o item.

        Sempre faça:
        - escrever em português do Brasil, direto, sem jargão no resumo executivo;
        - preferir a linguagem de risco e probabilidade ("aumenta a chance de", "deixa a empresa
          exposta a") à de certeza ("vai ser invadida");
        - reconhecer o que a empresa já tem antes de falar do que falta. O objetivo comercial é
          aproveitar o investimento existente, não empurrar substituição;
        - apontar contradições entre respostas quando existirem — elas são o achado mais valioso de
          um assessment declarado.
        """;

    /// <summary>
    /// O catálogo, em texto, para o modelo saber o que cada código de pergunta significa. Montado
    /// uma vez por processo: é o bloco que vai para o cache e precisa ser byte a byte idêntico
    /// entre chamadas — recalcular por requisição arriscaria variar a ordem e perder o cache.
    /// </summary>
    private static readonly Lazy<string> DescricaoDoCatalogo = new(() =>
    {
        var sb = new StringBuilder();
        sb.AppendLine("CATÁLOGO DE DOMÍNIOS E CONTROLES AVALIADOS");
        sb.AppendLine();
        foreach (var dominio in CatalogoDeDominios.Todos)
        {
            sb.AppendLine($"## {dominio.Nome} [{dominio.Codigo}] — {dominio.Resumo}");
            foreach (var p in dominio.Perguntas)
            {
                sb.Append("- ").Append(p.Codigo).Append(": ").Append(p.Texto);
                if (p.RespostaBoaEhNao) { sb.Append(" (atenção: aqui a resposta desejável é NÃO)"); }
                if (p.SeNaoTratar is { } risco) { sb.Append(" | risco se ausente: ").Append(risco); }
                sb.AppendLine();
            }
            sb.AppendLine();
        }
        return sb.ToString();
    });

    private static string MontarEntrada(Models.Diagnostico diagnostico, ResultadoDoDiagnostico resultado)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"EMPRESA: {diagnostico.Company?.Nome ?? "não informada"}");
        sb.AppendLine($"Respondente: {diagnostico.Respondente ?? "não informado"} ({diagnostico.RespondenteCargo ?? "cargo não informado"})");
        sb.AppendLine();
        sb.AppendLine("NÚMEROS CALCULADOS (não recalcule, use estes):");
        sb.AppendLine($"- Cobertura: {resultado.Cobertura}% dos controles esperados existem");
        sb.AppendLine($"- Maturidade: {(resultado.Maturidade is { } m ? $"{m} de 5" : "não avaliável — não há controle implantado o suficiente para julgar gestão")}");
        sb.AppendLine($"- Uso do investimento: {(resultado.UsoDoInvestimento is { } u ? $"{u}%" : "sem inventário de ferramentas")}");
        sb.AppendLine($"- Integração ao L'Okta IA: {(resultado.Integracao is { } i ? $"{i}%" : "sem inventário")}");
        sb.AppendLine($"- Completude do levantamento: {resultado.Completude}% das perguntas aplicáveis foram respondidas");
        sb.AppendLine($"- Controles ausentes: {resultado.ControlesAusentes} | parciais: {resultado.ControlesParciais}");
        sb.AppendLine();

        sb.AppendLine("ORIGEM DAS RESPOSTAS (o quanto disso é palavra do cliente):");
        foreach (var (origem, qtd) in resultado.PorOrigem.OrderByDescending(x => x.Value))
        {
            sb.AppendLine($"- {origem}: {qtd}");
        }
        sb.AppendLine();

        sb.AppendLine("RESULTADO POR DOMÍNIO:");
        foreach (var d in resultado.Dominios)
        {
            sb.AppendLine($"- {d.Dominio.Nome}: cobertura {d.Cobertura}%, maturidade "
                + $"{(d.Maturidade is { } dm ? dm.ToString() : "não avaliável")}, "
                + $"{d.PerguntasRespondidas}/{d.PerguntasVisiveis} respondidas");
        }
        sb.AppendLine();

        sb.AppendLine("RESPOSTAS:");
        foreach (var r in diagnostico.Respostas.OrderBy(r => r.PerguntaCodigo))
        {
            var valor = r.Opcao ?? r.Texto ?? r.Numero?.ToString() ?? "—";
            sb.AppendLine($"- {r.PerguntaCodigo} = {valor} [{r.Origem}]");
        }
        sb.AppendLine();

        if (diagnostico.Ferramentas.Count > 0)
        {
            sb.AppendLine("FERRAMENTAS QUE A EMPRESA JÁ POSSUI:");
            foreach (var f in diagnostico.Ferramentas)
            {
                sb.AppendLine($"- {f.Categoria}: {f.Fabricante} {f.Produto} | licenciada: {Sn(f.Licenciado)}, "
                    + $"atualizada: {Sn(f.Atualizado)}, monitorada: {Sn(f.Monitorado)}, "
                    + $"alertas tratados: {Sn(f.AlertasTratados)}, integrada ao L'Okta IA: {Sn(f.IntegradaAoLokta)}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(diagnostico.Observacoes))
        {
            sb.AppendLine($"OBSERVAÇÕES DO CONSULTOR: {diagnostico.Observacoes}");
        }

        return sb.ToString();
    }

    private static string Sn(bool v) => v ? "sim" : "não";

    /// <summary>
    /// Esquema da resposta. Campos separados porque cada um vai para um lugar diferente do
    /// relatório — pedir "um texto" devolveria prosa impossível de diagramar.
    /// </summary>
    private static readonly Lazy<Dictionary<string, JsonElement>> Esquema = new(() =>
    {
        static JsonElement E(object o) => JsonSerializer.SerializeToElement(o);
        static object Txt(string desc) => new { type = "string", description = desc };
        static object Lista(string desc) => new { type = "array", items = new { type = "string" }, description = desc };

        return new Dictionary<string, JsonElement>
        {
            ["type"] = E("object"),
            ["additionalProperties"] = E(false),
            ["required"] = E(new[] { "resumo_executivo", "resumo_tecnico", "inconsistencias", "leitura_do_investimento", "perguntas_adicionais" }),
            ["properties"] = E(new
            {
                resumo_executivo = Txt("2 a 4 parágrafos para a diretoria. Sem jargão, sem nome de produto. Começa pelo que a empresa já tem."),
                resumo_tecnico = Txt("Para quem opera. Pode citar tecnologia e configuração. Objetivo."),
                inconsistencias = Lista("Contradições entre respostas. Lista vazia se não houver — não invente."),
                leitura_do_investimento = Txt("O que a empresa já pagou e não usa por inteiro, e o que dá para ganhar sem comprar nada."),
                perguntas_adicionais = Lista("O que o consultor deveria ter perguntado e não perguntou."),
            }),
        };
    });

    private static void PreencherDoJson(DiagnosticoAnalise analise, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;

        analise.ResumoExecutivo = Texto(raiz, "resumo_executivo");
        analise.ResumoTecnico = Texto(raiz, "resumo_tecnico");
        analise.LeituraDoInvestimento = Texto(raiz, "leitura_do_investimento");
        analise.Inconsistencias = Juntar(raiz, "inconsistencias");
        analise.PerguntasAdicionais = Juntar(raiz, "perguntas_adicionais");

        static string? Texto(JsonElement raiz, string campo) =>
            raiz.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        static string? Juntar(JsonElement raiz, string campo)
        {
            if (!raiz.TryGetProperty(campo, out var v) || v.ValueKind != JsonValueKind.Array) { return null; }
            var itens = v.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var texto = string.Join("\n", itens);
            return string.IsNullOrWhiteSpace(texto) ? null : texto;
        }
    }
}
