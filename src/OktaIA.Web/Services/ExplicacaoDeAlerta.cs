using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

/// <summary>
/// Traduz um alerta técnico em três respostas: o que é, por que importa e o que fazer.
///
/// POR QUE EXISTE: a tela mostrava "CIS Ubuntu Benchmark: Ensure ufw is uninstalled or disabled
/// with iptables: Status changed from passed to failed" e mais nada. Isso é uma frase para quem
/// já sabe — e quem abre a tela do SOC de um cliente, na frente dele, precisa saber responder
/// "e daí?" na hora. Alerta que ninguém entende é alerta que ninguém trata.
///
/// ⚠️ POR CATÁLOGO, E NÃO POR IA. Explicação gerada na hora custa dinheiro por clique, demora, e
/// — o que decide — pode variar entre duas leituras do MESMO alerta. Num assunto em que a
/// resposta vira ação numa máquina de cliente, texto que muda sozinho é pior que texto curto.
/// O catálogo é conferível, instantâneo e de graça.
///
/// ⚠️ E TEM FALLBACK HONESTO. O que não está no catálogo devolve o que se sabe — a origem e a
/// descrição crua — em vez de inventar uma explicação plausível. Chutar aqui seria dizer a alguém
/// para mexer numa máquina de produção com base num palpite.
/// </summary>
public static class ExplicacaoDeAlerta
{
    /// <param name="OQueE">O fato, sem jargão.</param>
    /// <param name="PorQue">A consequência — e, quando cabe, o ângulo da LGPD.</param>
    /// <param name="OQueFazer">A ação, em passos que cabem numa frase cada.</param>
    /// <param name="DoCatalogo">Falso = não sabíamos, e a tela diz isso.</param>
    public record Explicacao(string OQueE, string PorQue, string OQueFazer, bool DoCatalogo = true);

    public static Explicacao De(string titulo, string? descricao, string? categoria)
    {
        var t = (titulo ?? "").ToLowerInvariant();

        // ── Conta e grupo novos ─────────────────────────────────────────────────────────────
        if (t.Contains("new user added") || t.Contains("new group added"))
        {
            var conta = t.Contains("group") ? "grupo" : "usuário";
            return new(
                $"Um {conta} novo foi criado no sistema operacional desta máquina.",
                "Criar conta é o passo que um invasor dá para continuar entrando depois que a porta "
                + "de origem for fechada — e é também o que um funcionário faz ao instalar um "
                + "programa. Os dois casos parecem iguais no log; só quem administra a máquina sabe "
                + "qual é. Se for acesso não autorizado a um servidor que trata dados pessoais, "
                + "vira incidente de segurança para o art. 48 da LGPD.",
                "1. Pergunte a quem administra a máquina se a criação foi planejada.\n"
                + "2. Se ninguém reconhecer, desative a conta antes de investigar — desativar é "
                + "reversível, apagar destrói a prova.\n"
                + "3. Confira o que essa conta acessou desde que foi criada.\n"
                + "4. Reconhecida, marque como falso positivo aqui para não voltar a chamar atenção.");
        }

        // ── Resumo de conformidade com nota baixa ───────────────────────────────────────────
        if (t.Contains("sca summary") || t.Contains("score less than"))
        {
            return new(
                "A auditoria de configuração comparou esta máquina com o benchmark do CIS — o "
                + "padrão público de configuração segura para o sistema dela — e a nota ficou baixa.",
                "Não é vírus nem invasão: é a máquina configurada de fábrica, com as opções "
                + "permissivas que o fabricante deixa ligadas para não dar trabalho ao usuário. "
                + "É exatamente o tipo de coisa que a ANPD pergunta depois de um vazamento — o "
                + "art. 49 da LGPD exige que os sistemas atendam a padrões de boas práticas.",
                "1. Abra o painel do Wazuh, em Agents → esta máquina → SCA, e veja os itens que falharam.\n"
                + "2. Comece pelos que não quebram nada: bloqueio de conta, senha, conta de convidado, "
                + "protocolos antigos.\n"
                + "3. Trate como projeto, não como emergência: subir de 24% para 60% é trabalho de "
                + "semanas, e cada item corrigido some sozinho do relatório do mês seguinte.");
        }

        // ── Firewall desligado ──────────────────────────────────────────────────────────────
        if (t.Contains("ufw") || t.Contains("firewall"))
        {
            return new(
                "A auditoria detectou que o firewall do sistema não está ativo, ou está configurado "
                + "de forma que não protege.",
                "Sem firewall, qualquer serviço que suba na máquina fica alcançável por quem chegar "
                + "até ela pela rede — inclusive serviços que ninguém sabe que estão rodando. Num "
                + "servidor exposto à internet, isso é a diferença entre uma porta e uma porta aberta.",
                "1. Confirme se existe outro firewall no lugar (na nuvem, um grupo de segurança; "
                + "num VPS, regras de iptables) — pode não haver problema real.\n"
                + "2. Não havendo, ative o firewall liberando ANTES as portas em uso, ou você "
                + "derruba o próprio acesso ao servidor.\n"
                + "3. Refaça a auditoria e confirme que o item saiu do relatório.");
        }

        // ── Serviço web onde não deveria ────────────────────────────────────────────────────
        if (t.Contains("web server services are not in use") || t.Contains("apache") || t.Contains("nginx"))
        {
            return new(
                "A auditoria encontrou um serviço de servidor web rodando numa máquina em que o "
                + "benchmark não espera encontrá-lo.",
                "Todo serviço a mais é uma porta a mais para tentar. Quando o serviço é legítimo, o "
                + "alerta é só o benchmark não sabendo o papel da máquina; quando não é, alguém "
                + "subiu algo ali.",
                "1. Descubra qual serviço é e se ele deve mesmo estar ali.\n"
                + "2. Sendo legítimo, marque como falso positivo aqui — o relatório do cliente não "
                + "pode carregar um achado que a operação já decidiu aceitar.\n"
                + "3. Não sendo, desligue o serviço e investigue quem o subiu.");
        }

        // ── Falha de autenticação ───────────────────────────────────────────────────────────
        if (t.Contains("authentication fail") || t.Contains("failed password")
            || t.Contains("brute force") || t.Contains("invalid user"))
        {
            return new(
                "Houve tentativa (ou várias) de entrar na máquina com credencial errada.",
                "Em servidor exposto à internet isso acontece o dia inteiro, por robô, e sozinho não "
                + "significa nada. O que importa é o padrão: muitas tentativas do mesmo endereço, "
                + "ou uma tentativa que DEU CERTO logo depois de várias que falharam.",
                "1. Veja se alguma tentativa teve sucesso em seguida — é isso que separa ruído de incidente.\n"
                + "2. Se o acesso por senha ainda estiver ligado no SSH, desligue e use só chave.\n"
                + "3. Endereço insistente pode ser bloqueado no firewall, mas trocar senha por chave "
                + "resolve o problema; bloquear IP só empurra.");
        }

        // ── Integridade de arquivo ──────────────────────────────────────────────────────────
        if (t.Contains("integrity") || t.Contains("checksum changed") || t.Contains("syscheck")
            || (categoria ?? "").Contains("ossec", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                "Um arquivo monitorado do sistema foi alterado, criado ou removido.",
                "Atualização de programa muda arquivo o tempo todo — é o caso comum. O que preocupa "
                + "é alteração em arquivo de configuração ou de sistema fora de uma janela de "
                + "manutenção, porque é assim que se instala permanência numa máquina invadida.",
                "1. Veja QUAL arquivo mudou e a hora.\n"
                + "2. Cruze com atualizações e manutenções feitas naquele momento.\n"
                + "3. Sem explicação, compare o conteúdo com uma cópia boa antes de qualquer coisa.");
        }

        // ── Benchmark, os demais ────────────────────────────────────────────────────────────
        if (t.Contains("cis ") || t.Contains("benchmark") || t.Contains("status changed from passed"))
        {
            return new(
                "Um item da auditoria de configuração deixou de ser cumprido — a máquina saiu do que "
                + "o benchmark do CIS recomenda.",
                "Cada item desses é uma opção de segurança desligada. Isoladamente costuma ser pequeno; "
                + "somados é o que faz a nota de conformidade cair, e é a nota que aparece no relatório "
                + "mensal que o cliente arquiva como evidência do art. 6º, X da LGPD.",
                "1. No painel do Wazuh, em SCA, abra este item: ele traz o que se espera, o que a "
                + "máquina tem e o comando de correção.\n"
                + "2. Aplique em uma máquina primeiro e observe.\n"
                + "3. Se o item não fizer sentido para o papel desta máquina, marque como falso positivo "
                + "com a justificativa — decisão registrada vale mais que item ignorado.");
        }

        // ── Serviço subiu ───────────────────────────────────────────────────────────────────
        if (t.Contains("started") || t.Contains("stopped") || t.Contains("restarted"))
        {
            return new(
                "Um serviço do próprio monitoramento foi iniciado, parado ou reiniciado.",
                "Normalmente é manutenção ou reinício da máquina. Vira sinal quando o monitoramento "
                + "PARA e não volta: a partir daí ninguém está olhando, e o silêncio parece "
                + "tranquilidade.",
                "1. Confirme que o agente voltou a reportar — a data do último dado está na tela de LGPD.\n"
                + "2. Parada repetida costuma ser falta de memória ou conflito com antivírus na máquina.");
        }

        // ── Não sabemos ─────────────────────────────────────────────────────────────────────
        var cru = string.IsNullOrWhiteSpace(descricao) ? null : descricao!.Trim();
        return new(
            cru ?? "Este alerta não tem uma explicação no catálogo da plataforma.",
            "A regra que o gerou vem da ferramenta do cliente. O detalhe completo — inclusive o log "
            + "que originou o alerta — está no painel da ferramenta, com o mesmo título e horário.",
            "1. Abra o painel do Wazuh e procure por este título na máquina indicada.\n"
            + "2. Se este tipo de alerta for se repetir, vale acrescentá-lo ao catálogo de "
            + "explicações — assim a próxima pessoa não precisa refazer o caminho.",
            DoCatalogo: false);
    }
}
