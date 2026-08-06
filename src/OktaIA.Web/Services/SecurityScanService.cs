using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

// Checagens reais de segurança externa contra um domínio autorizado — tudo em C# puro
// (HttpClient/TcpClient/SslStream), sem binário externo, porque o App Service (okta-ia) roda em
// Windows e ferramentas tipo Nuclei/nmap não são portáveis pra lá sem esforço extra.
public class SecurityScanService
{
    private readonly HttpClient _http;

    private static readonly int[] PortasComuns = [21, 22, 23, 25, 445, 1433, 3306, 3389, 5432, 6379];

    // Chaves estáveis gravadas em Vulnerability.CategoriaScan — permitem "Reverificar" rodar só
    // a checagem específica daquele achado, em vez do scan inteiro de novo.
    public const string CategoriaTls = "tls";
    public const string CategoriaHeaders = "headers";
    public const string CategoriaDns = "dns";
    public const string CategoriaPortas = "portas";

    public SecurityScanService(HttpClient http)
    {
        _http = http;
    }

    // RiscoPt/En = o impacto/dano ao negócio se não for corrigido (pra quem decide, não só pra
    // quem executa). RecomendacaoPt/En = o quê fazer, em 1-2 frases. InstrucoesPt/En = o passo a
    // passo real por plataforma comum (Nginx/Apache/IIS/Cloudflare/Azure/painel de DNS) — não
    // sabemos a stack de cada cliente, então cobrimos as mais comuns em vez de adivinhar uma só.
    public record ScanFinding(string TituloPt, string TituloEn, Severidade Severidade, string Categoria,
        string DescricaoPt, string DescricaoEn, string RiscoPt, string RiscoEn, string RecomendacaoPt, string RecomendacaoEn,
        string InstrucoesPt, string InstrucoesEn);

    public async Task<List<ScanFinding>> ExecutarAsync(string dominio)
    {
        var achados = new List<ScanFinding>();

        achados.AddRange(await VerificarTlsAsync(dominio));
        achados.AddRange(await VerificarCabecalhosAsync(dominio));
        achados.AddRange(await VerificarDnsEmailAsync(dominio));
        achados.AddRange(await VerificarPortasAsync(dominio));

        return achados;
    }

    // Reroda só uma categoria — usado pelo botão "Reverificar" de um achado específico, sem
    // precisar escanear o ativo inteiro de novo.
    public async Task<List<ScanFinding>> ExecutarCategoriaAsync(string dominio, string categoria) => categoria switch
    {
        CategoriaTls => await VerificarTlsAsync(dominio),
        CategoriaHeaders => await VerificarCabecalhosAsync(dominio),
        CategoriaDns => await VerificarDnsEmailAsync(dominio),
        CategoriaPortas => await VerificarPortasAsync(dominio),
        _ => [],
    };

    private static async Task<List<ScanFinding>> VerificarTlsAsync(string dominio)
    {
        var achados = new List<ScanFinding>();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(dominio, 443, cts.Token);
            using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(dominio);

            if (ssl.RemoteCertificate is not null)
            {
                using var cert = new X509Certificate2(ssl.RemoteCertificate);
                var diasRestantes = (cert.NotAfter - DateTime.Now).Days;
                if (diasRestantes < 0)
                {
                    achados.Add(new ScanFinding("Certificado TLS expirado", "TLS certificate expired", Severidade.Critica, CategoriaTls,
                        $"O certificado TLS de {dominio} expirou em {cert.NotAfter:dd/MM/yyyy}.",
                        $"The TLS certificate for {dominio} expired on {cert.NotAfter:yyyy-MM-dd}.",
                        "Navegadores exibem um alerta de \"site inseguro\" e bloqueiam o acesso por padrão — isso derruba a confiança do visitante e pode afastar clientes; integrações automatizadas (apps, APIs, webhooks) também param de conectar.",
                        "Browsers show an \"unsafe site\" warning and block access by default — this destroys visitor trust and can drive customers away; automated integrations (apps, APIs, webhooks) also stop connecting.",
                        "Renove o certificado TLS imediatamente junto ao seu provedor e confirme que a renovação automática está ativa.",
                        "Renew the TLS certificate immediately with your provider and confirm auto-renewal is enabled.",
                        InstrucoesTlsPt, InstrucoesTlsEn));
                }
                else if (diasRestantes < 30)
                {
                    achados.Add(new ScanFinding("Certificado TLS expirando em breve", "TLS certificate expiring soon", Severidade.Alta, CategoriaTls,
                        $"O certificado TLS de {dominio} expira em {diasRestantes} dia(s) ({cert.NotAfter:dd/MM/yyyy}).",
                        $"The TLS certificate for {dominio} expires in {diasRestantes} day(s) ({cert.NotAfter:yyyy-MM-dd}).",
                        "Se expirar sem renovação, o site fica inacessível com alerta de segurança no navegador — queda de tráfego, confiança e possível parada de integrações automatizadas.",
                        "If it expires without renewal, the site becomes inaccessible with a browser security warning — drop in traffic, trust, and potential outage of automated integrations.",
                        "Renove o certificado TLS antes da data de expiração e, se possível, ative renovação automática pra evitar isso no futuro.",
                        "Renew the TLS certificate before it expires, and enable auto-renewal if possible to avoid this in the future.",
                        InstrucoesTlsPt, InstrucoesTlsEn));
                }
            }

#pragma warning disable SYSLIB0039 // checar protocolos obsoletos é o objetivo desta verificação
            if (ssl.SslProtocol is SslProtocols.Tls or SslProtocols.Tls11)
#pragma warning restore SYSLIB0039
            {
                achados.Add(new ScanFinding("Protocolo TLS desatualizado", "Outdated TLS protocol", Severidade.Alta, CategoriaTls,
                    $"{dominio} negociou {ssl.SslProtocol}, considerado obsoleto e inseguro. Recomenda-se exigir TLS 1.2+.",
                    $"{dominio} negotiated {ssl.SslProtocol}, considered outdated and insecure. TLS 1.2+ should be enforced.",
                    "Conexões nesse protocolo são vulneráveis a ataques conhecidos (ex. BEAST, POODLE) que podem expor dados em trânsito, incluindo credenciais e dados de clientes trafegando pelo site.",
                    "Connections on this protocol are vulnerable to known attacks (e.g. BEAST, POODLE) that can expose data in transit, including credentials and customer data flowing through the site.",
                    "Desative TLS 1.0 e TLS 1.1 na configuração do servidor web/balanceador de carga, deixando só TLS 1.2 e 1.3 habilitados.",
                    "Disable TLS 1.0 and TLS 1.1 in the web server/load balancer configuration, leaving only TLS 1.2 and 1.3 enabled.",
                    InstrucoesProtocoloTlsPt, InstrucoesProtocoloTlsEn));
            }
        }
        catch (Exception)
        {
            achados.Add(new ScanFinding("Falha ao verificar TLS", "TLS check failed", Severidade.Media, CategoriaTls,
                $"Não foi possível estabelecer conexão TLS com {dominio} na porta 443 pra checar o certificado.",
                $"Could not establish a TLS connection to {dominio} on port 443 to check the certificate.",
                "Sem conseguir validar o TLS, não é possível confirmar se o tráfego dos seus visitantes está protegido — o risco real pode estar sendo mascarado por uma falha de conectividade que também afeta clientes reais.",
                "Without validating TLS, it's not possible to confirm whether your visitors' traffic is protected — the real risk may be masked by a connectivity failure that also affects real customers.",
                "Confirme que a porta 443 está acessível publicamente e que o servidor responde a handshakes TLS.",
                "Confirm port 443 is publicly reachable and the server responds to TLS handshakes.",
                "", ""));
        }

        return achados;
    }

    private async Task<List<ScanFinding>> VerificarCabecalhosAsync(string dominio)
    {
        var achados = new List<ScanFinding>();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var resposta = await _http.GetAsync($"https://{dominio}/", cts.Token);

            void Checar(string cabecalho, string tituloPt, string tituloEn, string riscoPt, string riscoEn, string recPt, string recEn, string instrPt, string instrEn)
            {
                if (!resposta.Headers.Contains(cabecalho) && !(resposta.Content?.Headers.Contains(cabecalho) ?? false))
                {
                    achados.Add(new ScanFinding(tituloPt, tituloEn, Severidade.Media, CategoriaHeaders,
                        $"O cabeçalho de resposta \"{cabecalho}\" não foi encontrado em {dominio}.",
                        $"The response header \"{cabecalho}\" was not found on {dominio}.",
                        riscoPt, riscoEn, recPt, recEn, instrPt, instrEn));
                }
            }

            Checar("Strict-Transport-Security", "Cabeçalho HSTS ausente", "Missing HSTS header",
                "Um atacante na mesma rede (ex. Wi-Fi público) pode forçar o navegador a usar conexão não criptografada e interceptar dados trafegados (ataque de downgrade).",
                "An attacker on the same network (e.g. public Wi-Fi) can force the browser into an unencrypted connection and intercept data in transit (downgrade attack).",
                "Configure o cabeçalho HTTP \"Strict-Transport-Security: max-age=31536000; includeSubDomains\" no servidor web.",
                "Configure the HTTP header \"Strict-Transport-Security: max-age=31536000; includeSubDomains\" on the web server.",
                InstrucoesHstsPt, InstrucoesHstsEn);
            Checar("Content-Security-Policy", "Cabeçalho CSP ausente", "Missing CSP header",
                "O site fica mais vulnerável a ataques de Cross-Site Scripting (XSS), que podem roubar sessões de usuários, credenciais ou injetar conteúdo malicioso na página.",
                "The site becomes more vulnerable to Cross-Site Scripting (XSS) attacks, which can hijack user sessions, steal credentials, or inject malicious content into the page.",
                "Configure um cabeçalho Content-Security-Policy apropriado ao site, restringindo de onde scripts/estilos podem ser carregados.",
                "Configure an appropriate Content-Security-Policy header, restricting where scripts/styles can be loaded from.",
                InstrucoesCspPt, InstrucoesCspEn);
            Checar("X-Content-Type-Options", "Cabeçalho X-Content-Type-Options ausente", "Missing X-Content-Type-Options header",
                "O navegador pode interpretar arquivos de forma incorreta (MIME sniffing), abrindo brecha pra execução de scripts maliciosos disfarçados de outro tipo de arquivo (ex. upload de usuário).",
                "The browser may misinterpret file types (MIME sniffing), opening a path for malicious scripts disguised as another file type (e.g. user uploads) to execute.",
                "Configure o cabeçalho \"X-Content-Type-Options: nosniff\" no servidor web.",
                "Configure the \"X-Content-Type-Options: nosniff\" header on the web server.",
                InstrucoesXctoPt, InstrucoesXctoEn);
            Checar("X-Frame-Options", "Cabeçalho X-Frame-Options ausente", "Missing X-Frame-Options header",
                "O site pode ser embutido em um iframe malicioso por outro site (clickjacking), enganando usuários pra clicar em ações que não pretendiam — ex. transferências ou alterações de conta.",
                "The site can be embedded in a malicious iframe by another site (clickjacking), tricking users into clicking actions they didn't intend — e.g. transfers or account changes.",
                "Configure o cabeçalho \"X-Frame-Options: SAMEORIGIN\" (ou DENY) pra evitar clickjacking.",
                "Configure the \"X-Frame-Options: SAMEORIGIN\" (or DENY) header to prevent clickjacking.",
                InstrucoesXfoPt, InstrucoesXfoEn);
        }
        catch (Exception)
        {
            achados.Add(new ScanFinding("Falha ao verificar cabeçalhos HTTP", "HTTP header check failed", Severidade.Media, CategoriaHeaders,
                $"Não foi possível conectar em https://{dominio}/ pra checar cabeçalhos de segurança.",
                $"Could not connect to https://{dominio}/ to check security headers.",
                "Sem conseguir verificar os cabeçalhos, não é possível confirmar se o site está protegido contra clickjacking, XSS e downgrade de conexão — o mesmo problema de conectividade também afeta visitantes reais.",
                "Without checking the headers, it's not possible to confirm whether the site is protected against clickjacking, XSS and connection downgrade — the same connectivity issue also affects real visitors.",
                "Confirme que o site responde normalmente em https:// antes de reverificar.",
                "Confirm the site responds normally over https:// before re-checking.",
                "", ""));
        }

        return achados;
    }

    private async Task<List<ScanFinding>> VerificarDnsEmailAsync(string dominio)
    {
        var achados = new List<ScanFinding>();

        var temSpf = await TemRegistroTxtAsync(dominio, "v=spf1");
        if (!temSpf)
        {
            achados.Add(new ScanFinding("Registro SPF ausente", "Missing SPF record", Severidade.Alta, CategoriaDns,
                $"{dominio} não tem um registro SPF configurado — facilita falsificação de e-mails (phishing) em nome do domínio.",
                $"{dominio} has no SPF record configured — makes it easier to spoof emails (phishing) impersonating the domain.",
                "Qualquer pessoa pode enviar e-mails falsificando o seu domínio — um vetor comum de phishing e fraude contra seus próprios clientes, fornecedores e parceiros, com risco direto à reputação da marca.",
                "Anyone can send emails spoofing your domain — a common phishing and fraud vector against your own customers, suppliers and partners, with direct risk to brand reputation.",
                $"Adicione um registro TXT em {dominio} listando os servidores autorizados a enviar e-mail em nome do domínio.",
                $"Add a TXT record on {dominio} listing the servers authorized to send email for the domain.",
                string.Format(InstrucoesSpfPt, dominio), string.Format(InstrucoesSpfEn, dominio)));
        }

        var temDmarc = await TemRegistroTxtAsync($"_dmarc.{dominio}", "v=dmarc1");
        if (!temDmarc)
        {
            achados.Add(new ScanFinding("Registro DMARC ausente", "Missing DMARC record", Severidade.Alta, CategoriaDns,
                $"{dominio} não tem um registro DMARC configurado — sem política de autenticação de e-mail contra spoofing.",
                $"{dominio} has no DMARC record configured — no email authentication policy against spoofing.",
                "Mesmo com SPF configurado, sem DMARC não há política dizendo aos provedores de e-mail o que fazer com mensagens falsificadas — phishing em nome do seu domínio pode continuar chegando às caixas de entrada de clientes e parceiros.",
                "Even with SPF configured, without DMARC there's no policy telling email providers what to do with spoofed messages — phishing impersonating your domain can keep reaching customers' and partners' inboxes.",
                $"Adicione um registro TXT em _dmarc.{dominio} com a política de autenticação de e-mail.",
                $"Add a TXT record on _dmarc.{dominio} with the email authentication policy.",
                string.Format(InstrucoesDmarcPt, dominio), string.Format(InstrucoesDmarcEn, dominio)));
        }

        return achados;
    }

    private async Task<bool> TemRegistroTxtAsync(string nome, string prefixoEsperado)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(nome)}&type=TXT");
            req.Headers.Add("Accept", "application/dns-json");
            using var resposta = await _http.SendAsync(req, cts.Token);
            if (!resposta.IsSuccessStatusCode)
            {
                return false;
            }

            using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(cts.Token));
            if (!json.RootElement.TryGetProperty("Answer", out var respostas))
            {
                return false;
            }

            foreach (var registro in respostas.EnumerateArray())
            {
                var dado = registro.GetProperty("data").GetString() ?? "";
                if (dado.Trim('"').StartsWith(prefixoEsperado, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // DNS indisponível não deve travar o scan inteiro — só não confirma o registro.
        }

        return false;
    }

    private static async Task<List<ScanFinding>> VerificarPortasAsync(string dominio)
    {
        var tarefas = PortasComuns.Select(async porta =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(dominio, porta, cts.Token);
                return (Porta: porta, Aberta: true);
            }
            catch (Exception)
            {
                return (Porta: porta, Aberta: false);
            }
        });

        var resultados = await Task.WhenAll(tarefas);
        return resultados.Where(r => r.Aberta).Select(r => new ScanFinding(
            $"Porta {r.Porta} possivelmente exposta publicamente", $"Port {r.Porta} possibly publicly exposed", Severidade.Alta, CategoriaPortas,
            $"A porta {r.Porta} respondeu a uma conexão TCP externa em {dominio} — confirme se esse serviço deveria estar acessível pela internet.",
            $"Port {r.Porta} responded to an external TCP connection on {dominio} — confirm whether this service should be internet-facing.",
            "Um serviço exposto diretamente à internet é alvo comum de varreduras automatizadas e tentativas de força bruta; se não estiver atualizado ou com autenticação forte, pode virar a porta de entrada para um incidente maior (ransomware, roubo de dados).",
            "A service exposed directly to the internet is a common target for automated scans and brute-force attempts; if it isn't up to date or lacks strong authentication, it can become the entry point for a larger incident (ransomware, data theft).",
            $"Revise se o serviço na porta {r.Porta} realmente precisa estar acessível pela internet; se não precisar, bloqueie a porta no firewall/security group.",
            $"Review whether the service on port {r.Porta} really needs to be internet-facing; if not, block the port at the firewall/security group.",
            string.Format(InstrucoesPortaPt, r.Porta), string.Format(InstrucoesPortaEn, r.Porta))).ToList();
    }

    // ---------- Passo a passo detalhado por plataforma ----------
    // Não sabemos a stack de cada cliente (Nginx? Apache? IIS? WordPress em hospedagem
    // compartilhada? Cloudflare na frente? Azure/AWS?) — cobrimos as combinações mais comuns em
    // vez de assumir uma só, já que o público-alvo (PME) raramente vai saber qual usa.

    private const string InstrucoesTlsPt =
        "• Certificado gerenciado automaticamente (Let's Encrypt, Cloudflare, Azure App Service, cPanel/AutoSSL): confira se a renovação automática está ativada e se não há erro de validação (DNS ou HTTP) travando o processo — geralmente aparece um aviso no painel.\n" +
        "• Certificado manual (comprado de uma CA): gere um novo certificado com o provedor (GoDaddy, DigiCert, Registro.br, etc.) e instale-o no servidor ou CDN antes da expiração.\n" +
        "• cPanel/hospedagem compartilhada: procure \"SSL/TLS Status\" no painel e clique em \"Run AutoSSL\" pra reemitir gratuitamente.\n" +
        "• Cloudflare: SSL/TLS > Edge Certificates — confirme que o certificado Universal SSL está ativo e válido.";
    private const string InstrucoesTlsEn =
        "• Auto-managed certificate (Let's Encrypt, Cloudflare, Azure App Service, cPanel/AutoSSL): check that auto-renewal is enabled and there's no validation error (DNS or HTTP) blocking it — usually shown as a warning in the panel.\n" +
        "• Manual certificate (purchased from a CA): generate a new certificate with your provider (GoDaddy, DigiCert, etc.) and install it on the server or CDN before it expires.\n" +
        "• cPanel/shared hosting: look for \"SSL/TLS Status\" in the panel and click \"Run AutoSSL\" to reissue for free.\n" +
        "• Cloudflare: SSL/TLS > Edge Certificates — confirm Universal SSL is active and valid.";

    private const string InstrucoesProtocoloTlsPt =
        "• Nginx: no bloco server, defina \"ssl_protocols TLSv1.2 TLSv1.3;\" e rode \"nginx -t && systemctl reload nginx\".\n" +
        "• Apache: no VirtualHost SSL, defina \"SSLProtocol -all +TLSv1.2 +TLSv1.3\" e reinicie o Apache.\n" +
        "• IIS/Windows Server: use a ferramenta gratuita \"IIS Crypto\" (Nartac Software) pra desabilitar TLS 1.0/1.1 sem editar o registro na mão.\n" +
        "• Cloudflare: SSL/TLS > Edge Certificates > \"Minimum TLS Version\" = TLS 1.2.\n" +
        "• Azure App Service: em Configuração > Configurações gerais > \"TLS mínimo\" = 1.2.";
    private const string InstrucoesProtocoloTlsEn =
        "• Nginx: in the server block, set \"ssl_protocols TLSv1.2 TLSv1.3;\" and run \"nginx -t && systemctl reload nginx\".\n" +
        "• Apache: in the SSL VirtualHost, set \"SSLProtocol -all +TLSv1.2 +TLSv1.3\" and restart Apache.\n" +
        "• IIS/Windows Server: use the free \"IIS Crypto\" tool (Nartac Software) to disable TLS 1.0/1.1 without manually editing the registry.\n" +
        "• Cloudflare: SSL/TLS > Edge Certificates > \"Minimum TLS Version\" = TLS 1.2.\n" +
        "• Azure App Service: under Configuration > General settings > \"Minimum TLS Version\" = 1.2.";

    private const string InstrucoesHstsPt =
        "• Nginx: adicione \"add_header Strict-Transport-Security \\\"max-age=31536000; includeSubDomains\\\" always;\" no bloco server e recarregue.\n" +
        "• Apache: adicione \"Header always set Strict-Transport-Security \\\"max-age=31536000; includeSubDomains\\\"\" no VirtualHost e reinicie.\n" +
        "• IIS: no web.config, dentro de <system.webServer><httpProtocol><customHeaders>, adicione o cabeçalho Strict-Transport-Security.\n" +
        "• Cloudflare: SSL/TLS > Edge Certificates > ative \"HSTS\" (leia o aviso antes — é difícil reverter rápido).\n" +
        "• WordPress/hospedagem compartilhada sem acesso ao servidor: peça ao suporte da hospedagem ou use um plugin de segurança (ex. Really Simple SSL).";
    private const string InstrucoesHstsEn =
        "• Nginx: add \"add_header Strict-Transport-Security \\\"max-age=31536000; includeSubDomains\\\" always;\" in the server block and reload.\n" +
        "• Apache: add \"Header always set Strict-Transport-Security \\\"max-age=31536000; includeSubDomains\\\"\" in the VirtualHost and restart.\n" +
        "• IIS: in web.config, inside <system.webServer><httpProtocol><customHeaders>, add the Strict-Transport-Security header.\n" +
        "• Cloudflare: SSL/TLS > Edge Certificates > enable \"HSTS\" (read the warning first — it's hard to reverse quickly).\n" +
        "• WordPress/shared hosting without server access: ask your host's support or use a security plugin (e.g. Really Simple SSL).";

    private const string InstrucoesCspPt =
        "• É a checagem mais trabalhosa: primeiro liste todos os domínios de onde o site carrega scripts, estilos, imagens e fontes (ex. Google Fonts, CDN, Google Analytics).\n" +
        "• Comece em modo \"somente relatório\" (Content-Security-Policy-Report-Only) pra ver o que quebraria, sem bloquear nada ainda.\n" +
        "• Nginx/Apache/IIS: mesmo mecanismo de cabeçalho do HSTS acima, com o valor do CSP montado a partir da lista de domínios.\n" +
        "• Use o CSP Evaluator do Google (ferramenta gratuita) pra validar a política antes de ativar em produção.";
    private const string InstrucoesCspEn =
        "• This is the most involved check: first list every domain the site loads scripts, styles, images and fonts from (e.g. Google Fonts, CDN, Google Analytics).\n" +
        "• Start in report-only mode (Content-Security-Policy-Report-Only) to see what would break, without blocking anything yet.\n" +
        "• Nginx/Apache/IIS: same header mechanism as HSTS above, with the CSP value built from your domain list.\n" +
        "• Use Google's CSP Evaluator (free tool) to validate the policy before enabling it in production.";

    private const string InstrucoesXctoPt =
        "• Nginx: \"add_header X-Content-Type-Options \\\"nosniff\\\" always;\" no bloco server.\n" +
        "• Apache: \"Header set X-Content-Type-Options \\\"nosniff\\\"\" no VirtualHost.\n" +
        "• IIS: mesmo local do web.config usado pro HSTS, adicionando esse cabeçalho.\n" +
        "• É o cabeçalho mais simples de ativar — sem risco de quebrar o site.";
    private const string InstrucoesXctoEn =
        "• Nginx: \"add_header X-Content-Type-Options \\\"nosniff\\\" always;\" in the server block.\n" +
        "• Apache: \"Header set X-Content-Type-Options \\\"nosniff\\\"\" in the VirtualHost.\n" +
        "• IIS: same web.config location used for HSTS, adding this header.\n" +
        "• This is the simplest header to enable — no risk of breaking the site.";

    private const string InstrucoesXfoPt =
        "• Nginx: \"add_header X-Frame-Options \\\"SAMEORIGIN\\\" always;\" no bloco server.\n" +
        "• Apache: \"Header always set X-Frame-Options \\\"SAMEORIGIN\\\"\" no VirtualHost.\n" +
        "• IIS: mesmo local do web.config usado pro HSTS.\n" +
        "• Use SAMEORIGIN se o site precisa ser incorporado em iframe por páginas do mesmo domínio; use DENY se nunca precisa.";
    private const string InstrucoesXfoEn =
        "• Nginx: \"add_header X-Frame-Options \\\"SAMEORIGIN\\\" always;\" in the server block.\n" +
        "• Apache: \"Header always set X-Frame-Options \\\"SAMEORIGIN\\\"\" in the VirtualHost.\n" +
        "• IIS: same web.config location used for HSTS.\n" +
        "• Use SAMEORIGIN if the site needs to be embedded in an iframe by pages on the same domain; use DENY if it never does.";

    private const string InstrucoesSpfPt =
        "• Acesse o painel de DNS do domínio (Registro.br, GoDaddy, Cloudflare, etc.) e crie um registro TXT na raiz ('@' ou '{0}').\n" +
        "• Google Workspace: \"v=spf1 include:_spf.google.com ~all\"\n" +
        "• Microsoft 365/Outlook: \"v=spf1 include:spf.protection.outlook.com -all\"\n" +
        "• Se usa mais de um provedor de e-mail (ex. Google + SendGrid/Mailgun pra e-mail transacional), combine todos os includes num ÚNICO registro SPF — nunca crie dois registros SPF separados, isso quebra a validação.";
    private const string InstrucoesSpfEn =
        "• Go to your domain's DNS panel (registrar or Cloudflare) and create a TXT record on the root ('@' or '{0}').\n" +
        "• Google Workspace: \"v=spf1 include:_spf.google.com ~all\"\n" +
        "• Microsoft 365/Outlook: \"v=spf1 include:spf.protection.outlook.com -all\"\n" +
        "• If you use more than one email provider (e.g. Google + SendGrid/Mailgun for transactional email), combine all includes into a SINGLE SPF record — never create two separate SPF records, it breaks validation.";

    private const string InstrucoesDmarcPt =
        "• No mesmo painel de DNS, crie um registro TXT no host \"_dmarc.{0}\".\n" +
        "• Valor inicial recomendado: \"v=DMARC1; p=quarantine; rua=mailto:dmarc@{0}\"\n" +
        "• Comece com p=quarantine (ou até p=none, só monitorando) por 2-4 semanas observando os relatórios (enviados pro e-mail em \"rua=\") antes de evoluir pra p=reject, que é mais rígido e pode bloquear e-mail legítimo se SPF/DKIM não estiverem certos.\n" +
        "• Certifique-se de que o SPF (acima) já está correto antes de ativar o DMARC — ele depende do SPF/DKIM pra funcionar.";
    private const string InstrucoesDmarcEn =
        "• In the same DNS panel, create a TXT record on the host \"_dmarc.{0}\".\n" +
        "• Recommended starting value: \"v=DMARC1; p=quarantine; rua=mailto:dmarc@{0}\"\n" +
        "• Start with p=quarantine (or even p=none, monitoring only) for 2-4 weeks watching the reports (sent to the email in \"rua=\") before moving to p=reject, which is stricter and can block legitimate email if SPF/DKIM aren't correct.\n" +
        "• Make sure SPF (above) is already correct before enabling DMARC — it depends on SPF/DKIM to work.";

    private const string InstrucoesPortaPt =
        "• Se o serviço na porta {0} não precisa ser acessado da internet pública, bloqueie-a no firewall (ex. \"ufw deny {0}\" no Linux) ou no Security Group/Network Security Group da nuvem (AWS/Azure).\n" +
        "• Se precisar de acesso remoto administrativo, prefira uma VPN ou liberar só por IP específico (whitelist), nunca deixar aberto pro mundo todo.\n" +
        "• Se o serviço realmente precisa ser público (ex. servidor de e-mail na porta 25), garanta que esteja atualizado e com autenticação forte.";
    private const string InstrucoesPortaEn =
        "• If the service on port {0} doesn't need to be reachable from the public internet, block it at the firewall (e.g. \"ufw deny {0}\" on Linux) or the cloud Security Group/Network Security Group (AWS/Azure).\n" +
        "• If you need remote admin access, prefer a VPN or allow only a specific IP (whitelist) instead of leaving it open to everyone.\n" +
        "• If the service genuinely needs to be public (e.g. an email server on port 25), make sure it's up to date and has strong authentication.";
}
