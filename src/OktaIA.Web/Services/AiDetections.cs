namespace OktaIA.Web.Services;

public record AiDetection(string Tipo, string Titulo, string Confianca, string Corpo, string[] Tags);

// Detecções comportamentais de exemplo do módulo IA — narrativa específica do design original,
// mantida como conteúdo de referência (não é histórico de negócio simulável por seed genérico).
public static class AiDetections
{
    public static readonly AiDetection[] Pt =
    [
        new("UEBA", "Acesso fora do padrão horário", "87%",
            "O usuário m.torres acessou 214 registros de pacientes às 03:12, sem histórico de plantão noturno em 18 meses. Volume 12× acima da média pessoal.",
            ["T1078", "hsanta.br", "LGPD"]),
        new("MALWARE", "Webshell identificado por comportamento", "88%",
            "Arquivo PHP criado por processo web e imediatamente acessado por IP externo único. Sem correspondência de assinatura — detecção puramente comportamental.",
            ["T1505.003", "lojaativa"]),
        new("DDOS", "Padrão de botnet em formação", "73%",
            "Crescimento coordenado de requisições de 1.842 IPs residenciais com mesmo user-agent malformado. Ainda abaixo do limiar de mitigação automática.",
            ["T1498", "checkout.pagou.io"]),
        new("PHISHING", "Domínio typosquatting recém-registrado", "96%",
            "Domínio com distância de edição 1 do portal oficial, registrado há 4 dias, com certificado emitido 2 horas antes da campanha.",
            ["T1566.002", "prefdigital"]),
        new("INSIDER", "Download em massa antes de desligamento", "64%",
            "Colaborador com desligamento agendado baixou 3,2 GB do repositório comercial em 40 minutos. Contexto de RH correlacionado automaticamente.",
            ["T1530", "vector"]),
    ];

    public static readonly AiDetection[] En =
    [
        new("UEBA", "Off-hours access anomaly", "87%",
            "User m.torres accessed 214 patient records at 03:12, with no night-shift history in 18 months. Volume 12× above personal average.",
            ["T1078", "hsanta.br", "LGPD"]),
        new("MALWARE", "Webshell identified by behavior", "88%",
            "PHP file created by a web process and immediately accessed by a single external IP. No signature match — purely behavioral detection.",
            ["T1505.003", "lojaativa"]),
        new("DDOS", "Botnet pattern forming", "73%",
            "Coordinated request growth from 1,842 residential IPs sharing the same malformed user-agent. Still below the automatic mitigation threshold.",
            ["T1498", "checkout.pagou.io"]),
        new("PHISHING", "Newly registered typosquatting domain", "96%",
            "Domain with edit distance 1 from the official portal, registered 4 days ago, certificate issued 2 hours before the campaign.",
            ["T1566.002", "prefdigital"]),
        new("INSIDER", "Bulk download before offboarding", "64%",
            "Employee with scheduled offboarding downloaded 3.2 GB from the sales repository in 40 minutes. HR context correlated automatically.",
            ["T1530", "vector"]),
    ];

    public static AiDetection[] For(string lang) => lang == "en" ? En : Pt;
}
