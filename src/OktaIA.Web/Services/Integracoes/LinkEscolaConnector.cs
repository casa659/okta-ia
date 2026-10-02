using System.Text.Json;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Integracoes;

/// <summary>
/// Lê a POSTURA DA TRILHA do LinkEscola (`GET /postura/trilha`) e transforma o que ela revela em
/// alertas da plataforma.
///
/// 🔴 POR QUE ELE EXISTE, e por que é diferente de todos os outros adaptadores: o modelo da L'okta é
/// Wazuh no ambiente do cliente, e Wazuh depende de AGENTE instalado em máquina. O LinkEscola roda
/// em Azure App Service com Postgres gerenciado — não há máquina onde instalar nada. Não era "ligar
/// o conector": era um caminho de coleta que a plataforma não tinha.
///
/// 🔴 ELE NÃO RECEBE DADO PESSOAL, e isto é a decisão central. O endpoint do LinkEscola devolve
/// AGREGADOS — contagens por tipo de ação, meses com e sem registro, horas desde o último evento.
/// Nunca uma linha da trilha. Quem sabe "fulano abriu a ficha do titular X às 14h32" é o LinkEscola,
/// e continua sendo só ele. A L'okta mede a INTEGRIDADE da trilha, não o conteúdo dela.
///
/// Isso também responde ao limite que não é técnico: as duas empresas são do mesmo dono, e a L'okta
/// medindo o LinkEscola é AUTOAVALIAÇÃO. O relatório que sai daqui é de POSTURA — nunca certificado,
/// nunca auditoria independente, e nunca pentest, que continua não existindo neste produto.
///
/// ⚠️ OS ALERTAS SÃO FATOS, NÃO LIMIARES INVENTADOS. Cada um dos três abaixo é verificável e não
/// depende de eu escolher um número:
///
/// 1. **Trilha parada** — a própria trilha diz há quantas horas não recebe registro.
/// 2. **Mês sem registro** — o endpoint lista os meses vazios do período.
/// 3. **Ação não classificada** — o LinkEscola nomeia o que não entra em nenhum indicador.
///
/// Não há alerta de "muitos downloads" nem de "leituras acima da média": eu teria de inventar o
/// corte, e um número inventado vira alarme que a equipe aprende a ignorar. Quando houver base
/// histórica medida, o corte vem dela.
/// </summary>
public class LinkEscolaConnector : IConnector
{
    private readonly HttpClient _http;
    private readonly ILogger<LinkEscolaConnector> _log;

    /// <summary>
    /// A partir de quantas horas sem registro a trilha é considerada parada.
    ///
    /// ⚠️ 48 h e não 24: o LinkEscola de uma escola pequena passa um fim de semana inteiro sem
    /// ninguém abrir ficha nenhuma, e isso é operação normal, não falha de coleta. Abaixo disso o
    /// alerta dispararia toda segunda-feira — e alarme que toca sem motivo é alarme desligado.
    /// </summary>
    private const double HorasParaTrilhaParada = 48;

    public LinkEscolaConnector(HttpClient http, ILogger<LinkEscolaConnector> log)
    {
        _http = http;
        _log = log;
    }

    public CapacidadesConector Capacidades { get; } = new(
        Slug: "linkescola",
        Nome: "LinkEscola — trilha LGPD",
        Categoria: "Conformidade / LGPD",
        Fabricante: "LinkEscola",
        TipoAuth: TipoAuthConector.ApiKey,
        Escopos: [EscopoSync.Alertas],
        ExigeUrlBase: true,
        CamposCredencial:
        [
            // ⚠️ O controlador NÃO é segredo: é o número da instituição dentro do LinkEscola, e
            // precisa ficar visível para quem configurou conferir que ligou na escola certa.
            new CampoCredencial("controlador", "Id do controlador (instituição)", Segredo: false),
            new CampoCredencial("chave", "Chave da postura (X-Postura-Chave)", Segredo: true),
        ]);

    public async Task<ResultadoTeste> TestarConexaoAsync(ContextoConector ctx, CancellationToken ct)
    {
        var inicio = DateTimeOffset.UtcNow;
        try
        {
            using var resp = await PedirAsync(ctx, ct);
            var latencia = (int)(DateTimeOffset.UtcNow - inicio).TotalMilliseconds;

            // ⚠️ 404 É RESPOSTA ESPERADA, e significa coisa específica: o endpoint só existe quando
            // a postura está LIGADA no LinkEscola. Tratar como "fora do ar" mandaria quem configura
            // procurar problema de rede onde falta um App Setting.
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new ResultadoTeste(false,
                    "O endereço respondeu, mas a postura está desligada no LinkEscola "
                    + "(`Postura:Ligado`). Ligue lá antes de instalar o conector.", latencia);
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new ResultadoTeste(false, "A chave da postura não confere.", latencia);
            }

            if (!resp.IsSuccessStatusCode)
            {
                return new ResultadoTeste(false, $"O LinkEscola respondeu {(int)resp.StatusCode}.", latencia);
            }

            var raiz = await LerAsync(resp, ct);

            // ⚠️ Confere que veio a MEDIÇÃO, não só um 200. Um proxy ou uma página de erro podem
            // devolver 200 com HTML, e "conectado" seria mentira.
            if (!raiz.TryGetProperty("medido", out var medido) || !medido.GetBoolean())
            {
                return new ResultadoTeste(false,
                    "A resposta não é a medição da trilha — confira a URL base.", latencia);
            }

            var registros = raiz.GetProperty("trilha").GetProperty("registros").GetInt32();

            return new ResultadoTeste(true,
                $"Conectado. A trilha deste controlador tem {registros:N0} registro(s).",
                latencia, Referencia: raiz.GetProperty("gerado_em").GetString());
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Teste de conexão do LinkEscola falhou (conector {Id})", ctx.ConectorId);
            return new ResultadoTeste(false, "Não foi possível falar com o LinkEscola: " + e.Message);
        }
    }

    public async Task<ResultadoSaude> VerificarSaudeAsync(ContextoConector ctx, CancellationToken ct)
    {
        var inicio = DateTimeOffset.UtcNow;
        try
        {
            using var resp = await PedirAsync(ctx, ct);
            var latencia = (int)(DateTimeOffset.UtcNow - inicio).TotalMilliseconds;
            return new ResultadoSaude(resp.IsSuccessStatusCode, latencia,
                resp.IsSuccessStatusCode ? null : $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception e)
        {
            return new ResultadoSaude(false, null, e.Message);
        }
    }

    /// <summary>
    /// Uma leitura, uma página. Não há paginação a fazer: o endpoint é uma FOTOGRAFIA do estado da
    /// trilha, não um fluxo de eventos.
    ///
    /// ⚠️ A IDEMPOTÊNCIA VEM DO `IdExterno` COM A DATA. Rodando três vezes no mesmo dia, o índice
    /// único (ConectorId, IdExterno) deixa um alerta só — e amanhã, se o problema continuar, nasce
    /// outro, que é o comportamento certo: "a trilha segue parada no dia seguinte" é informação
    /// nova, não repetição.
    /// </summary>
    public async Task<ResultadoSync> SincronizarAsync(
        ContextoConector ctx, EscopoSync escopo, string? cursor, CancellationToken ct)
    {
        if (escopo != EscopoSync.Alertas)
        {
            return new ResultadoSync([], cursor, false);
        }

        using var resp = await PedirAsync(ctx, ct);
        resp.EnsureSuccessStatusCode();

        var raiz = await LerAsync(resp, ct);
        var geradoEm = raiz.GetProperty("gerado_em").GetDateTimeOffset();
        var dia = geradoEm.ToString("yyyy-MM-dd");

        var trilha = raiz.GetProperty("trilha");
        var alertas = new List<AlertaUnificado>();

        AlertaUnificado Novo(string id, string titulo, string descricao, Severidade sev) => new()
        {
            CompanyId = ctx.CompanyId,
            ConectorId = ctx.ConectorId,
            IdExterno = id,
            Titulo = titulo,
            Descricao = descricao,
            Severidade = sev,
            Categoria = "conformidade",
            AtivoNome = "LinkEscola",
            OcorridoEm = geradoEm,
            StatusOrigem = "Medido",
        };

        // ── 1. A trilha parou de receber ────────────────────────────────────────────────────
        var horas = trilha.GetProperty("horas_desde_o_ultimo").GetDouble();
        if (horas >= HorasParaTrilhaParada)
        {
            alertas.Add(Novo(
                $"trilha-parada:{dia}",
                "Trilha de auditoria sem registros novos",
                $"O último registro da trilha do LinkEscola tem {horas:N0} horas. "
                + "Ou ninguém operou o portal nesse período, ou o registro parou de ser gravado — "
                + "e a segunda hipótese só aparece quando alguém procura. "
                + "Evidência de tratamento não se cria retroativamente.",
                horas >= 24 * 7 ? Severidade.Alta : Severidade.Media));
        }

        // ── 2. Meses sem registro nenhum ────────────────────────────────────────────────────
        //
        // ⚠️ UM ALERTA POR MÊS, e não um só listando todos. Cada mês vazio é uma lacuna própria na
        // evidência: tratados juntos, resolver um faria o alerta inteiro parecer pendente, e
        // fechá-lo daria por resolvidos os outros.
        foreach (var mes in trilha.GetProperty("meses_sem_registro").EnumerateArray())
        {
            var m = mes.GetString();
            if (string.IsNullOrWhiteSpace(m)) { continue; }

            alertas.Add(Novo(
                $"lacuna-mensal:{m}",
                $"Mês sem nenhum registro na trilha ({m})",
                $"Não há um único registro de auditoria em {m}. Para o art. 46 isso é um buraco na "
                + "evidência: não se consegue demonstrar o que foi feito — nem que nada foi feito.",
                Severidade.Media));
        }

        // ── 3. Ações que nenhum indicador classifica ────────────────────────────────────────
        //
        // 🔴 É O ALARME DO PRÓPRIO INDICADOR. As listas de classificação do LinkEscola são
        // explícitas de propósito, e lista explícita ENVELHECE: um ponto novo de registro nasce
        // fora dela e o indicador passa a mentir PARA MENOS, em silêncio. Já aconteceu lá —
        // "Anexo baixado" contava 1 quando eram 111.
        var ultimos = raiz.GetProperty("ultimos_30_dias");
        foreach (var item in ultimos.GetProperty("nao_classificadas").EnumerateArray())
        {
            var acao = item.GetProperty("acao").GetString();
            var quantas = item.GetProperty("quantas").GetInt32();
            if (string.IsNullOrWhiteSpace(acao)) { continue; }

            alertas.Add(Novo(
                $"nao-classificada:{acao}:{geradoEm:yyyy-MM}",
                $"Ação sem classificação no indicador: \"{acao}\"",
                $"{quantas} registro(s) de \"{acao}\" nos últimos 30 dias não entram em nenhum "
                + "indicador de postura. Enquanto isso durar, os números de leitura de dado "
                + "pessoal, saída de documento e entrega ao titular estão MENORES que a realidade.",
                Severidade.Baixa));
        }

        // ⚠️ O cursor é a data da medição, e serve só para o histórico: não há o que retomar numa
        // fotografia. Guardá-lo mantém visível, na tela do conector, quando foi a última leitura.
        return new ResultadoSync(alertas, geradoEm.ToString("O"), TemMais: false);
    }

    // ── Apoio ────────────────────────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> PedirAsync(ContextoConector ctx, CancellationToken ct)
    {
        var baseUrl = (ctx.UrlBase ?? "").TrimEnd('/');
        var controlador = ctx.Credencial.TryGetValue("controlador", out var c) ? c.Trim() : "";
        var chave = ctx.Credencial.TryGetValue("chave", out var k) ? k : "";

        var req = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl}/postura/trilha?controlador={Uri.EscapeDataString(controlador)}");

        // ⚠️ A chave vai no CABEÇALHO, nunca na query: query string entra no log de acesso do
        // servidor, no histórico do navegador e em qualquer proxy no caminho.
        req.Headers.Add("X-Postura-Chave", chave);

        return _http.SendAsync(req, ct);
    }

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var fluxo = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(fluxo, cancellationToken: ct);
        return doc.RootElement.Clone();
    }
}
