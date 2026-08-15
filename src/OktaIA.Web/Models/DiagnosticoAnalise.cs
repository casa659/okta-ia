namespace OktaIA.Web.Models;

/// <summary>Desfecho de uma chamada ao modelo. Recusa é resultado normal, não exceção.</summary>
public enum ResultadoAnalise
{
    /// <summary>Ainda rodando ou nunca executada.</summary>
    Pendente,
    Sucesso,

    /// <summary>
    /// O modelo recusou. Vem como HTTP 200 com `stop_reason: "refusal"`, não como erro — e o
    /// L'Okta IA vive inteiro no domínio de cibersegurança, que é exatamente o que os
    /// classificadores vigiam. Guardamos a categoria para saber se é falso positivo recorrente.
    /// </summary>
    Recusado,

    /// <summary>Falha de rede, credencial, cota. Distinta de recusa: esta se resolve tentando de novo.</summary>
    Falhou,
}

/// <summary>
/// A leitura que o modelo fez de um diagnóstico, GRAVADA.
///
/// Guardar em vez de gerar na hora tem três razões, todas práticas: o relatório entregue ao cliente
/// não pode mudar de texto entre uma abertura e outra; a chamada custa dinheiro e leva segundos; e
/// numa auditoria é preciso mostrar o que foi dito, quando, e por qual modelo.
///
/// ⚠️ Nada aqui é FATO. É a interpretação de um modelo sobre respostas que o cliente declarou. A
/// tela e o PDF precisam dizer isso — ver <see cref="OrigemDaInformacao"/>.
/// </summary>
public class DiagnosticoAnalise
{
    public int Id { get; set; }

    public int DiagnosticoId { get; set; }
    public Diagnostico? Diagnostico { get; set; }

    public DateTimeOffset GeradaEm { get; set; } = DateTimeOffset.UtcNow;
    public required string GeradaPor { get; set; }

    public ResultadoAnalise Resultado { get; set; } = ResultadoAnalise.Pendente;

    /// <summary>Modelo que de fato respondeu. Pode diferir do pedido quando o fallback entra.</summary>
    public string? Modelo { get; set; }

    /// <summary>Categoria da recusa quando <see cref="Resultado"/> é Recusado ("cyber", "bio"...).</summary>
    public string? MotivoRecusa { get; set; }

    /// <summary>Mensagem da falha quando <see cref="Resultado"/> é Falhou. Nunca mostrada crua ao cliente.</summary>
    public string? Erro { get; set; }

    // ── O que o modelo devolveu ──────────────────────────────────────────────
    // Campos separados em vez de um blob de texto: cada um vai para um lugar diferente do PDF, e
    // pedir ao modelo uma saída estruturada é o que impede a resposta de virar prosa que ninguém
    // consegue diagramar.

    /// <summary>Para a diretoria. Sem jargão, sem nome de produto.</summary>
    public string? ResumoExecutivo { get; set; }

    /// <summary>Para quem opera. Pode citar tecnologia e configuração.</summary>
    public string? ResumoTecnico { get; set; }

    /// <summary>Inconsistências entre respostas (diz que tem SIEM, mas nenhuma fonte envia log).</summary>
    public string? Inconsistencias { get; set; }

    /// <summary>O argumento de aproveitamento do que já foi comprado.</summary>
    public string? LeituraDoInvestimento { get; set; }

    /// <summary>Perguntas que faltaram — o que o consultor deveria ter levantado e não levantou.</summary>
    public string? PerguntasAdicionais { get; set; }

    // ── Custo e rastreabilidade ──────────────────────────────────────────────
    public int? TokensEntrada { get; set; }
    public int? TokensSaida { get; set; }
    /// <summary>Tokens lidos do cache — o catálogo é idêntico entre clientes e custa ~10% relido.</summary>
    public int? TokensCacheLidos { get; set; }
    public int? DuracaoMs { get; set; }
}
