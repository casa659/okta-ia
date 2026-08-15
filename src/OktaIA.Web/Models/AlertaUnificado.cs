namespace OktaIA.Web.Models;

/// <summary>
/// Estado da TRIAGEM — nosso, não do fabricante. Fica separado de <c>StatusOrigem</c> de propósito:
/// o Wazuh dizer "New" não significa que ninguém aqui olhou, e um analista marcar "Resolvido" aqui
/// não muda nada lá. Misturar os dois campos apagaria justamente a pergunta que o gestor faz —
/// "alguém tratou isso?".
///
/// FalsoPositivo é estado de primeira classe, e não um "resolvido" disfarçado: um alerta que nunca
/// foi ameaça não deveria contar como trabalho feito nem como risco eliminado.
/// </summary>
public enum StatusTriagem
{
    Novo = 0,
    EmAndamento = 1,
    Resolvido = 2,
    FalsoPositivo = 3,
}

/// <summary>
/// Alerta vindo de QUALQUER fabricante, já normalizado para uma forma só. Esta classe é o coração
/// da proposta da plataforma: se um alerta do Defender e um do Wazuh não virarem a mesma forma,
/// a IA não tem o que correlacionar e o gestor volta a ter N abas separadas — que é exatamente o
/// problema que ele já tem hoje.
///
/// Reusa <see cref="Severidade"/> (Baixa/Media/Alta/Critica) de propósito: é a mesma escala já usada
/// por Vulnerability, Incident e SecurityEvent, então KPI e cor de badge continuam valendo sem
/// tradução. Cada adaptador é responsável por mapear a escala do fabricante pra essa.
/// </summary>
public class AlertaUnificado
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public int ConectorId { get; set; }
    public Conector? Conector { get; set; }

    /// <summary>
    /// Id do alerta no sistema de origem. Junto com ConectorId forma a chave de idempotência
    /// (índice único) — reprocessar a mesma janela não duplica alerta.
    /// </summary>
    public required string IdExterno { get; set; }

    public required string Titulo { get; set; }
    public string? Descricao { get; set; }

    public Severidade Severidade { get; set; }

    /// <summary>Classe do alerta normalizada ("malware", "intrusao", "phishing", "politica", "vuln").</summary>
    public string? Categoria { get; set; }

    // Ligação com o inventário pelo NOME do ativo — mesma convenção já usada por Vulnerability e
    // ScanAlerta neste projeto. Evita FK que quebraria quando o alerta chega antes do ativo existir.
    public string? AtivoNome { get; set; }
    public string? AtivoIp { get; set; }
    public string? UsuarioAfetado { get; set; }

    /// <summary>Quando aconteceu, segundo a origem.</summary>
    public DateTimeOffset OcorridoEm { get; set; }

    /// <summary>Quando nós lemos. A diferença entre os dois é a defasagem real do pipeline.</summary>
    public DateTimeOffset IngeridoEm { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Status como o fabricante chama ("New", "InProgress", "Resolved") — texto cru, sem tradução.</summary>
    public string? StatusOrigem { get; set; }

    // ── Triagem ──────────────────────────────────────────────────────────────
    // Substitui o antigo `bool Resolvido`, que existia no modelo mas NUNCA era lido nem escrito por
    // ninguém — dois estados não davam conta da pergunta real ("alguém está tratando?").

    public StatusTriagem Status { get; set; } = StatusTriagem.Novo;

    /// <summary>Quem ficou responsável por tratar. Texto livre: pode ser alguém de fora da plataforma.</summary>
    public string? Responsavel { get; set; }

    /// <summary>O que foi feito/concluído. É o que transforma "resolvido" em algo auditável.</summary>
    public string? NotaTriagem { get; set; }

    /// <summary>Quando a triagem mudou pela última vez — e não quando o alerta chegou.</summary>
    public DateTimeOffset? TriadoEm { get; set; }

    /// <summary>Usuário da plataforma que fez a última mudança. Autoria não é a mesma coisa que responsável.</summary>
    public string? TriadoPor { get; set; }

    /// <summary>
    /// Payload original em JSON. Serve pra auditoria e pra depurar mapeamento errado sem precisar
    /// re-consultar o fabricante. Truncado pelo adaptador se vier gigante.
    /// </summary>
    public string? DadosBrutosJson { get; set; }
}
