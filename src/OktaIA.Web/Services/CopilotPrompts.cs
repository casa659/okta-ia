namespace OktaIA.Web.Services;

public record CopilotEvidence(string Chave, string Valor, string Cor);
public record CopilotPrompt(string Pergunta, string Resposta, CopilotEvidence[] Evidencias);

// Perguntas sugeridas + respostas scriptadas do Copiloto de segurança — extraídas literalmente
// do mockup (objeto PROMPTS). Sem LLM real por enquanto (fora de escopo desta fase); é o mesmo
// comportamento "encenado" do design original, só que servido do backend em vez de JS embutido.
public static class CopilotPrompts
{
    public static readonly CopilotPrompt[] Pt =
    [
        new("O servidor vpn-sp01 está sob ataque?",
            "Sim. 3.418 tentativas de autenticação falhas nos últimos 41 minutos, originadas de 214 IPs em 3 ASNs russos. Nenhuma sessão foi estabelecida. Duas contas de serviço sem MFA foram alvo repetido — recomendo bloquear os ASNs e forçar MFA agora.",
            [new("tentativas / 41min", "3.418", "#FF3B5C"), new("IPs distintos", "214", "#FF8A3D"), new("sessões estabelecidas", "0", "#00E0A4")]),
        new("Existe ransomware em algum ativo?",
            "Nenhum indicador de ransomware ativo. Detectei 1 comportamento suspeito de criptografia em massa em WKS-ADM-14, mas a análise apontou backup legítimo do usuário. Assinaturas de 214 famílias conhecidas foram verificadas nas últimas 6 horas.",
            [new("famílias verificadas", "214", "#00E0A4"), new("detecções positivas", "0", "#00E0A4"), new("comportamentos suspeitos", "1", "#FFC93C")]),
        new("Quais certificados vencem em 30 dias?",
            "Quatro certificados vencem nos próximos 30 dias. O mais crítico é checkout.pagou.io, que processa pagamentos e vence em 9 dias. A renovação automática via ACME falhou 2 vezes por erro de validação DNS.",
            [new("checkout.pagou.io", "9 dias", "#FF3B5C"), new("portal.hsanta.br", "17 dias", "#FF8A3D"), new("api.grupovector.com", "28 dias", "#FFC93C")]),
        new("Qual servidor está mais lento agora?",
            "srv-db-prod-02 apresenta latência de consulta 3,4× acima da linha de base desde 13:40. A causa provável é um plano de execução degradado após a última carga em lote — não há correlação com atividade maliciosa.",
            [new("latência p95", "842ms", "#FF8A3D"), new("linha de base", "247ms", "#4D9BFF"), new("correlação com ataque", "nenhuma", "#00E0A4")]),
        new("Houve vazamento de dados este mês?",
            "Não identifiquei exfiltração confirmada. O maior volume de saída anômalo foi 96 mil requisições ao endpoint público /v2/catalog, que não expõe dados sensíveis. Monitoramento DLP não registrou correspondências em 30 dias.",
            [new("exfiltrações confirmadas", "0", "#00E0A4"), new("alertas DLP", "0", "#00E0A4"), new("anomalias de saída", "1", "#FFC93C")]),
    ];

    public static readonly CopilotPrompt[] En =
    [
        new("Is server vpn-sp01 under attack?",
            "Yes. 3,418 failed authentication attempts in the last 41 minutes, from 214 IPs across 3 Russian ASNs. No session was established. Two service accounts without MFA were repeatedly targeted — I recommend blocking the ASNs and enforcing MFA now.",
            [new("attempts / 41min", "3,418", "#FF3B5C"), new("distinct IPs", "214", "#FF8A3D"), new("sessions established", "0", "#00E0A4")]),
        new("Is there ransomware on any asset?",
            "No active ransomware indicators. I detected 1 suspicious mass-encryption behavior on WKS-ADM-14, but analysis pointed to a legitimate user backup. Signatures for 214 known families were checked in the last 6 hours.",
            [new("families checked", "214", "#00E0A4"), new("positive detections", "0", "#00E0A4"), new("suspicious behaviors", "1", "#FFC93C")]),
        new("Which certificates expire in 30 days?",
            "Four certificates expire within 30 days. The most critical is checkout.pagou.io, which processes payments and expires in 9 days. Automatic ACME renewal failed twice due to a DNS validation error.",
            [new("checkout.pagou.io", "9 days", "#FF3B5C"), new("portal.hsanta.br", "17 days", "#FF8A3D"), new("api.grupovector.com", "28 days", "#FFC93C")]),
        new("Which server is slowest right now?",
            "srv-db-prod-02 shows query latency 3.4× above baseline since 13:40. The likely cause is a degraded execution plan after the last batch load — there is no correlation with malicious activity.",
            [new("p95 latency", "842ms", "#FF8A3D"), new("baseline", "247ms", "#4D9BFF"), new("attack correlation", "none", "#00E0A4")]),
        new("Was there any data breach this month?",
            "No confirmed exfiltration. The largest anomalous outbound volume was 96k requests to the public /v2/catalog endpoint, which exposes no sensitive data. DLP monitoring recorded no matches in 30 days.",
            [new("confirmed exfiltrations", "0", "#00E0A4"), new("DLP alerts", "0", "#00E0A4"), new("outbound anomalies", "1", "#FFC93C")]),
    ];

    public static CopilotPrompt[] For(string lang) => lang == "en" ? En : Pt;
}
