using System.Net.Http.Headers;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OktaIA.Web.Services;

/// <summary>
/// Configuração do envio de e-mail. Vem de App Settings (`Email__*`) — nunca de arquivo versionado.
///
/// Dois caminhos (12/09/2026):
/// - Microsoft Graph (credencial de aplicativo do mesmo Microsoft 365 da Lekker/LinkEscola): é o
///   caminho atual, com a caixa loktaia@iagrow.com.br (pedido do dono). Precisa de TenantId,
///   ClientId, ClientSecret e Remetente.
/// - SMTP autenticado (a antiga caixa info@loktaia.com na GoDaddy): fica como alternativa se os
///   campos do Graph não existirem. Precisa de Host, Usuario, Senha e Remetente.
/// </summary>
public class OpcoesEmail
{
    // ── Microsoft Graph ──
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    // ── SMTP ──
    /// <summary>Servidor SMTP. GoDaddy: `smtpout.secureserver.net`.</summary>
    public string? Host { get; set; }
    /// <summary>465 = TLS implícito (padrão da GoDaddy); 587 = STARTTLS.</summary>
    public int Porta { get; set; } = 465;
    public string? Usuario { get; set; }
    public string? Senha { get; set; }

    /// <summary>Endereço que aparece como remetente. No Graph, uma caixa do locatário; no SMTP, a própria caixa autenticada.</summary>
    public string? Remetente { get; set; }
    public string? NomeRemetente { get; set; } = "L'okta IA";

    /// <summary>Endereço que recebe cópia OCULTA (Cco) de todo e-mail enviado. Vazio = sem cópia.</summary>
    public string? CopiaPara { get; set; }

    public bool GraphCompleto =>
        !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(Remetente);

    public bool SmtpCompleto =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Senha) && !string.IsNullOrWhiteSpace(Remetente);

    public bool Completo => GraphCompleto || SmtpCompleto;
}

public interface IEnviadorEmail
{
    /// <summary>
    /// Devolve TAMBÉM o motivo da recusa. O chamador não mostra isso a quem está na tela (traz nome
    /// de servidor e de caixa), mas registra no log: "535 autenticação" e "550 destinatário" pedem
    /// providências opostas, e um "falhou" genérico manda procurar no lugar errado.
    /// </summary>
    /// <param name="copiaOculta">Cco; nulo = Email:CopiaPara.</param>
    Task<(bool Ok, string? Erro)> EnviarAsync(string para, string assunto, string corpoHtml, string? copiaOculta = null);

    bool Configurado { get; }
}

/// <summary>
/// Sem credencial: registra no log e devolve false.
///
/// Devolver false é deliberado. Um "enviado" mentiroso faz a pessoa esperar para sempre um e-mail
/// que nunca sai — e ninguém descobre, porque a tela disse que deu certo.
/// </summary>
public class EnviadorEmailNaoConfigurado(ILogger<EnviadorEmailNaoConfigurado> log) : IEnviadorEmail
{
    public bool Configurado => false;

    public Task<(bool Ok, string? Erro)> EnviarAsync(string para, string assunto, string corpoHtml, string? copiaOculta = null)
    {
        log.LogWarning("E-mail NÃO enviado (sem credencial de envio). Para: {Para} - Assunto: {Assunto}", para, assunto);
        return Task.FromResult<(bool, string?)>((false, "Nenhum provedor de e-mail configurado neste ambiente."));
    }
}

/// <summary>
/// Guarda o token do Graph entre os envios. Singleton porque o token vale ~1h e é da APLICAÇÃO,
/// não de quem está na tela — pedir um novo a cada e-mail é uma ida extra ao login da Microsoft
/// em toda mensagem, e eles limitam quem faz isso.
/// </summary>
public class TokenDoGraph(IHttpClientFactory http, OpcoesEmail op)
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private string? _token;
    private DateTimeOffset _expira = DateTimeOffset.MinValue;

    public async Task<string> ObterAsync(CancellationToken ct = default)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expira.AddMinutes(-5)) { return _token; }

        await _trava.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expira.AddMinutes(-5)) { return _token; }

            var cliente = http.CreateClient("graph");
            var resposta = await cliente.PostAsync(
                $"https://login.microsoftonline.com/{op.TenantId}/oauth2/v2.0/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = op.ClientId!,
                    ["client_secret"] = op.ClientSecret!,
                    ["scope"] = "https://graph.microsoft.com/.default",
                    ["grant_type"] = "client_credentials",
                }), ct);

            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            if (!resposta.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(ResumirErro(corpo, (int)resposta.StatusCode));
            }

            using var json = JsonDocument.Parse(corpo);
            _token = json.RootElement.GetProperty("access_token").GetString();
            var segundos = json.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            _expira = DateTimeOffset.UtcNow.AddSeconds(segundos);
            return _token!;
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>Tira do JSON da Microsoft a parte que serve para agir, sem despejar a resposta inteira.</summary>
    internal static string ResumirErro(string corpo, int status)
    {
        try
        {
            using var json = JsonDocument.Parse(corpo);
            var raiz = json.RootElement;
            if (raiz.TryGetProperty("error", out var erro))
            {
                if (erro.ValueKind == JsonValueKind.Object)
                {
                    var codigo = erro.TryGetProperty("code", out var c) ? c.GetString() : null;
                    var msg = erro.TryGetProperty("message", out var m) ? m.GetString() : null;
                    return string.Join(" - ", new[] { codigo, msg }.Where(x => !string.IsNullOrWhiteSpace(x)));
                }

                var desc = raiz.TryGetProperty("error_description", out var d) ? d.GetString() : null;
                var primeira = desc?.Split('\n', '\r').FirstOrDefault(l => l.Trim().Length > 0)?.Trim();
                return string.Join(" - ", new[] { erro.GetString(), primeira }.Where(x => !string.IsNullOrWhiteSpace(x)));
            }
        }
        catch (JsonException) { /* não era JSON: cai no texto cru abaixo */ }

        var cru = corpo.Length > 300 ? corpo[..300] : corpo;
        return $"HTTP {status}: {cru}";
    }
}

/// <summary>
/// Envio pela API do Microsoft Graph, com credencial de APLICATIVO (users/{remetente}/sendMail).
/// O nome que aparece como remetente é o NOME DE EXIBIÇÃO DA CAIXA no Microsoft 365 — o Exchange
/// ignora o `from.name` enviado aqui.
/// </summary>
public class EnviadorEmailGraph(IHttpClientFactory http, OpcoesEmail op, TokenDoGraph token, ILogger<EnviadorEmailGraph> log) : IEnviadorEmail
{
    public bool Configurado => op.GraphCompleto;

    public async Task<(bool Ok, string? Erro)> EnviarAsync(string para, string assunto, string corpoHtml, string? copiaOculta = null)
    {
        if (!op.GraphCompleto)
        {
            log.LogWarning("E-mail NÃO enviado: credenciais do Graph incompletas.");
            return (false, "Faltam campos na configuração (TenantId, ClientId, ClientSecret ou Remetente).");
        }
        copiaOculta = string.IsNullOrWhiteSpace(copiaOculta) ? op.CopiaPara : copiaOculta.Trim();
        var bcc = string.IsNullOrWhiteSpace(copiaOculta) || string.Equals(copiaOculta, para, StringComparison.OrdinalIgnoreCase)
            ? Array.Empty<object>()
            : new object[] { new { emailAddress = new { address = copiaOculta } } };

        try
        {
            var cliente = http.CreateClient("graph");
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await token.ObterAsync());

            var resposta = await cliente.PostAsJsonAsync(
                $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(op.Remetente!)}/sendMail",
                new
                {
                    message = new
                    {
                        subject = assunto,
                        body = new { contentType = "HTML", content = corpoHtml },
                        toRecipients = new[] { new { emailAddress = new { address = para } } },
                        bccRecipients = bcc,
                        from = new { emailAddress = new { address = op.Remetente!, name = op.NomeRemetente ?? "L'okta IA" } },
                    },
                    saveToSentItems = true,
                });

            if (resposta.IsSuccessStatusCode)
            {
                log.LogInformation("E-mail enviado pelo Graph de {De} para {Para}{Cco}", op.Remetente, para, bcc.Length > 0 ? " (cco " + copiaOculta + ")" : "");
                return (true, null);
            }

            var motivo = TokenDoGraph.ResumirErro(await resposta.Content.ReadAsStringAsync(), (int)resposta.StatusCode);
            log.LogError("Graph recusou o envio para {Para}: {Motivo}", para, motivo);
            return (false, motivo);
        }
        catch (Exception e)
        {
            log.LogError(e, "Falha ao enviar pelo Graph para {Para}: {Motivo}", para, e.Message);
            return (false, e.Message);
        }
    }
}

/// <summary>Envio por SMTP autenticado (MailKit). Uma conexão por mensagem — o volume aqui é de senha esquecida, não de campanha.</summary>
public class EnviadorEmailSmtp(OpcoesEmail op, ILogger<EnviadorEmailSmtp> log) : IEnviadorEmail
{
    public bool Configurado => op.SmtpCompleto;

    public async Task<(bool Ok, string? Erro)> EnviarAsync(string para, string assunto, string corpoHtml, string? copiaOculta = null)
    {
        if (!op.SmtpCompleto)
        {
            log.LogWarning("E-mail NÃO enviado: configuração SMTP incompleta.");
            return (false, "Faltam campos na configuração (Host, Usuario, Senha ou Remetente).");
        }
        copiaOculta = string.IsNullOrWhiteSpace(copiaOculta) ? op.CopiaPara : copiaOculta.Trim();

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(op.NomeRemetente ?? "L'okta IA", op.Remetente));
            msg.To.Add(MailboxAddress.Parse(para));
            if (!string.IsNullOrWhiteSpace(copiaOculta) && !string.Equals(copiaOculta, para, StringComparison.OrdinalIgnoreCase))
                msg.Bcc.Add(MailboxAddress.Parse(copiaOculta));
            msg.Subject = assunto;
            msg.Body = new BodyBuilder { HtmlBody = corpoHtml }.ToMessageBody();

            using var smtp = new SmtpClient { Timeout = 20_000 };
            var seguranca = op.Porta == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await smtp.ConnectAsync(op.Host, op.Porta, seguranca);
            await smtp.AuthenticateAsync(op.Usuario, op.Senha);
            await smtp.SendAsync(msg);
            await smtp.DisconnectAsync(true);

            log.LogInformation("E-mail enviado por SMTP para {Para}", para);
            return (true, null);
        }
        catch (Exception e)
        {
            // A mensagem do servidor vai inteira para o log: é ela que diz se foi senha, destinatário
            // ou bloqueio do provedor. Ver feedback_nao_descartar_motivo_do_servidor.
            log.LogError(e, "SMTP recusou o envio para {Para}: {Motivo}", para, e.Message);
            return (false, e.Message);
        }
    }
}
