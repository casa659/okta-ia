using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OktaIA.Web.Services;

/// <summary>
/// Configuração do envio de e-mail. Vem de App Settings (`Email__*`) — nunca de arquivo versionado.
///
/// A caixa remetente (`info@okta-ia.com`) mora no e-mail da GoDaddy (MX `secureserver.net`), não no
/// Microsoft 365 dos outros produtos. Por isso aqui é SMTP com usuário e senha da caixa, e não a API
/// do Graph — o Graph só alcança caixas do tenant da Microsoft.
/// </summary>
public class OpcoesEmail
{
    /// <summary>Servidor SMTP. GoDaddy: `smtpout.secureserver.net`.</summary>
    public string? Host { get; set; }

    /// <summary>465 = TLS implícito (padrão da GoDaddy); 587 = STARTTLS.</summary>
    public int Porta { get; set; } = 465;

    public string? Usuario { get; set; }
    public string? Senha { get; set; }

    /// <summary>Endereço que aparece como remetente. Na GoDaddy tem de ser a própria caixa autenticada.</summary>
    public string? Remetente { get; set; }

    public string? NomeRemetente { get; set; } = "L'okta IA";

    public bool Completo =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Senha) && !string.IsNullOrWhiteSpace(Remetente);
}

public interface IEnviadorEmail
{
    /// <summary>
    /// Devolve TAMBÉM o motivo da recusa. O chamador não mostra isso a quem está na tela (traz nome
    /// de servidor e de caixa), mas registra no log: "535 autenticação" e "550 destinatário" pedem
    /// providências opostas, e um "falhou" genérico manda procurar no lugar errado.
    /// </summary>
    Task<(bool Ok, string? Erro)> EnviarAsync(string para, string assunto, string corpoHtml);

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

    public Task<(bool Ok, string? Erro)> EnviarAsync(string para, string assunto, string corpoHtml)
    {
        log.LogWarning("E-mail NÃO enviado (sem credencial de envio). Para: {Para} - Assunto: {Assunto}", para, assunto);
        return Task.FromResult<(bool, string?)>((false, "Nenhum provedor de e-mail configurado neste ambiente."));
    }
}

/// <summary>Envio por SMTP autenticado (MailKit). Uma conexão por mensagem — o volume aqui é de senha esquecida, não de campanha.</summary>
public class EnviadorEmailSmtp(OpcoesEmail op, ILogger<EnviadorEmailSmtp> log) : IEnviadorEmail
{
    public bool Configurado => op.Completo;

    public async Task<(bool Ok, string? Erro)> EnviarAsync(string para, string assunto, string corpoHtml)
    {
        if (!op.Completo)
        {
            log.LogWarning("E-mail NÃO enviado: configuração SMTP incompleta.");
            return (false, "Faltam campos na configuração (Host, Usuario, Senha ou Remetente).");
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(op.NomeRemetente ?? "L'okta IA", op.Remetente));
            msg.To.Add(MailboxAddress.Parse(para));
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
