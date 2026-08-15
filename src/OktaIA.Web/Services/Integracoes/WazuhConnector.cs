using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Integracoes;

/// <summary>
/// Adaptador do Wazuh — o primeiro conector real da plataforma.
///
/// De onde vem o dado: alerta do Wazuh NÃO está na API do manager (porta 55000, que serve agentes,
/// FIM, rootcheck e vulnerabilidades). Alerta fica no **Wazuh Indexer**, que é um OpenSearch, nos
/// índices `wazuh-alerts-*`. Por isso este adaptador fala OpenSearch, com Basic auth, e não a API
/// do manager. Se um dia precisarmos de inventário de agentes, aí sim entra a API do manager, como
/// um escopo separado.
///
/// Estratégia de cursor: ordena por `@timestamp` crescente e guarda o timestamp do último alerta
/// lido. A página seguinte pede `@timestamp > cursor`. Simples, legível no banco e retomável.
/// Alertas com timestamp idêntico na virada de página são resolvidos pela chave de idempotência
/// (ConectorId, IdExterno) — o `_id` do OpenSearch —, então repetição não vira duplicata.
/// </summary>
public class WazuhConnector : IConnector
{
    private readonly HttpClient _http;
    private readonly ILogger<WazuhConnector> _log;

    private const int TamanhoPagina = 200;

    public WazuhConnector(HttpClient http, ILogger<WazuhConnector> log)
    {
        _http = http;
        _log = log;
    }

    public CapacidadesConector Capacidades { get; } = new(
        Slug: "wazuh",
        Nome: "Wazuh",
        Categoria: "HIDS / SIEM",
        Fabricante: "Wazuh Inc.",
        TipoAuth: TipoAuthConector.ApiKey,
        Escopos: [EscopoSync.Alertas],
        ExigeUrlBase: true,
        CamposCredencial:
        [
            new CampoCredencial("usuario", "Usuário do Indexer", Segredo: false),
            new CampoCredencial("senha", "Senha", Segredo: true),
        ]);

    public async Task<ResultadoTeste> TestarConexaoAsync(ContextoConector ctx, CancellationToken ct)
    {
        var inicio = DateTimeOffset.UtcNow;
        try
        {
            // Pergunta barata que prova as três coisas de uma vez: alcance de rede, credencial
            // válida e existência dos índices de alerta. Não grava nada.
            using var req = Requisicao(ctx, HttpMethod.Get, "_cat/indices/wazuh-alerts-*?format=json");
            using var resp = await _http.SendAsync(req, ct);

            var latencia = (int)(DateTimeOffset.UtcNow - inicio).TotalMilliseconds;

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Dizer QUAL usuário foi recusado: a senha nunca aparece, mas o usuário sim — e é ele
                // que denuncia o erro mais comum, o navegador ter preenchido o campo com o login da
                // própria plataforma em vez da credencial do cliente. Sem o nome na mensagem, o
                // sintoma joga a suspeita no cliente e só o banco revela o que foi gravado.
                var usuario = ctx.Credencial.GetValueOrDefault("usuario");
                var quem = string.IsNullOrWhiteSpace(usuario) ? "" : $" (usuário \"{usuario}\")";
                return new ResultadoTeste(false,
                    $"Usuário ou senha recusados pelo Wazuh Indexer{quem}.", latencia);
            }

            if (!resp.IsSuccessStatusCode)
            {
                return new ResultadoTeste(false, $"Indexer respondeu {(int)resp.StatusCode}.", latencia);
            }

            var corpo = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(corpo);
            var indices = doc.RootElement.GetArrayLength();

            return indices == 0
                // Conectou, mas não há o que ler. Distinguir isso de "falhou" importa: o gestor
                // precisa saber que o problema é o Wazuh dele não ter alerta, não a integração.
                ? new ResultadoTeste(true, "Conectado, mas nenhum índice wazuh-alerts-* foi encontrado ainda.",
                    latencia, ctx.Credencial.GetValueOrDefault("usuario"))
                : new ResultadoTeste(true, $"Conectado. {indices} índice(s) de alerta visíveis.",
                    latencia, ctx.Credencial.GetValueOrDefault("usuario"));
        }
        catch (Exception ex)
        {
            var latencia = (int)(DateTimeOffset.UtcNow - inicio).TotalMilliseconds;
            return new ResultadoTeste(false, $"Falha ao alcançar o Indexer: {ex.Message}", latencia);
        }
    }

    public async Task<ResultadoSaude> VerificarSaudeAsync(ContextoConector ctx, CancellationToken ct)
    {
        var inicio = DateTimeOffset.UtcNow;
        try
        {
            using var req = Requisicao(ctx, HttpMethod.Get, "_cluster/health");
            using var resp = await _http.SendAsync(req, ct);
            var latencia = (int)(DateTimeOffset.UtcNow - inicio).TotalMilliseconds;

            if (!resp.IsSuccessStatusCode)
            {
                return new ResultadoSaude(false, latencia, $"HTTP {(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;

            // "yellow" é normal em instalação de nó único (réplicas não alocadas) — tratar como
            // saudável evita alarme falso permanente no laboratório e em cliente pequeno.
            return new ResultadoSaude(status is "green" or "yellow", latencia, $"cluster {status}");
        }
        catch (Exception ex)
        {
            return new ResultadoSaude(false, null, ex.Message);
        }
    }

    public async Task<ResultadoSync> SincronizarAsync(ContextoConector ctx, EscopoSync escopo, string? cursor, CancellationToken ct)
    {
        if (escopo != EscopoSync.Alertas)
        {
            throw new NotSupportedException($"O conector Wazuh só sincroniza {nameof(EscopoSync.Alertas)}.");
        }

        // Primeira carga: 7 dias pra trás. Puxar o histórico inteiro de um SIEM na instalação
        // encheria o banco e demoraria demais — o valor está no que é recente e no que vem depois.
        var desde = cursor ?? DateTimeOffset.UtcNow.AddDays(-7).ToString("o");

        var consulta = new
        {
            size = TamanhoPagina,
            sort = new object[] { new { @timestamp = new { order = "asc" } } },
            query = new
            {
                range = new Dictionary<string, object>
                {
                    ["@timestamp"] = new { gt = desde },
                },
            },
        };

        using var req = Requisicao(ctx, HttpMethod.Post, "wazuh-alerts-*/_search");
        req.Content = new StringContent(JsonSerializer.Serialize(consulta), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var erro = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Wazuh Indexer respondeu {(int)resp.StatusCode}: {Truncar(erro, 400)}");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var hits = doc.RootElement.GetProperty("hits").GetProperty("hits");

        var alertas = new List<AlertaUnificado>();
        string? ultimoTimestamp = null;

        foreach (var hit in hits.EnumerateArray())
        {
            var alerta = Mapear(hit, ctx, out var timestampBruto);
            if (alerta is null)
            {
                continue;
            }

            alertas.Add(alerta);
            ultimoTimestamp = timestampBruto ?? ultimoTimestamp;
        }

        var quantidade = hits.GetArrayLength();
        _log.LogInformation("Wazuh conector {ConectorId}: {Lidos} alerta(s) lidos desde {Desde}.",
            ctx.ConectorId, quantidade, desde);

        return new ResultadoSync(
            alertas,
            // Só avança o cursor se algo veio; senão mantém o anterior pra não pular janela.
            ultimoTimestamp ?? cursor,
            TemMais: quantidade == TamanhoPagina);
    }

    private AlertaUnificado? Mapear(JsonElement hit, ContextoConector ctx, out string? timestampBruto)
    {
        timestampBruto = null;

        if (!hit.TryGetProperty("_source", out var src))
        {
            return null;
        }

        var idExterno = hit.TryGetProperty("_id", out var id) ? id.GetString() : null;
        if (string.IsNullOrEmpty(idExterno))
        {
            return null;
        }

        timestampBruto = src.TryGetProperty("@timestamp", out var ts) ? ts.GetString() : null;
        var ocorridoEm = DateTimeOffset.TryParse(timestampBruto, out var parsed) ? parsed : DateTimeOffset.UtcNow;

        var rule = src.TryGetProperty("rule", out var r) ? r : default;
        var agent = src.TryGetProperty("agent", out var a) ? a : default;

        var nivel = rule.ValueKind == JsonValueKind.Object && rule.TryGetProperty("level", out var lvl)
            ? lvl.GetInt32()
            : 0;

        var descricao = rule.ValueKind == JsonValueKind.Object && rule.TryGetProperty("description", out var d)
            ? d.GetString()
            : null;

        return new AlertaUnificado
        {
            CompanyId = ctx.CompanyId,
            ConectorId = ctx.ConectorId,
            IdExterno = idExterno,
            Titulo = descricao ?? "Alerta do Wazuh",
            // `full_log` só existe em alerta originado de log. Medido numa instância real 4.14.7:
            // 1 de 186 alertas tinha o campo — o resto era SCA, que não carrega log bruto. Sem o
            // fallback, a descrição ficaria vazia na esmagadora maioria dos casos.
            Descricao = Truncar(
                (src.TryGetProperty("full_log", out var fl) ? fl.GetString() : null)
                ?? (src.TryGetProperty("location", out var loc) ? loc.GetString() : null),
                2000),
            Severidade = MapearSeveridade(nivel),
            Categoria = PrimeiroGrupo(rule),
            AtivoNome = agent.ValueKind == JsonValueKind.Object && agent.TryGetProperty("name", out var an)
                ? an.GetString() : null,
            AtivoIp = agent.ValueKind == JsonValueKind.Object && agent.TryGetProperty("ip", out var ai)
                ? ai.GetString() : null,
            OcorridoEm = ocorridoEm,
            StatusOrigem = $"level {nivel}",
            DadosBrutosJson = Truncar(src.GetRawText(), 8000),
        };
    }

    /// <summary>
    /// Traduz o nível de regra do Wazuh (0-15) pra nossa escala de 4 níveis.
    ///
    /// Faixas escolhidas por nós, não impostas pelo produto: o Wazuh publica 16 níveis com
    /// descrições, e integrações diferentes agrupam de formas diferentes. Deixar explícito aqui
    /// evita que a escolha vire folclore — se um cliente achar que 12 deveria ser Alta e não
    /// Crítica, o ajuste é nesta função e em nenhum outro lugar.
    /// </summary>
    private static Severidade MapearSeveridade(int nivel) => nivel switch
    {
        >= 12 => Severidade.Critica,
        >= 8 => Severidade.Alta,
        >= 4 => Severidade.Media,
        _ => Severidade.Baixa,
    };

    private static string? PrimeiroGrupo(JsonElement rule)
    {
        if (rule.ValueKind != JsonValueKind.Object || !rule.TryGetProperty("groups", out var g)
            || g.ValueKind != JsonValueKind.Array || g.GetArrayLength() == 0)
        {
            return null;
        }

        return g[0].GetString();
    }

    private static HttpRequestMessage Requisicao(ContextoConector ctx, HttpMethod metodo, string caminho)
    {
        var baseUrl = (ctx.UrlBase ?? "").TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException("O conector Wazuh exige a URL do Indexer (ex.: https://10.0.0.5:9200).");
        }

        var req = new HttpRequestMessage(metodo, $"{baseUrl}/{caminho}");

        var usuario = ctx.Credencial.GetValueOrDefault("usuario") ?? "";
        var senha = ctx.Credencial.GetValueOrDefault("senha") ?? "";
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuario}:{senha}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        return req;
    }

    private static string? Truncar(string? texto, int max) =>
        texto is null || texto.Length <= max ? texto : texto[..max];
}
