namespace OktaIA.Web.Models;

/// <summary>Em que pé está a proposta. Nasce Rascunho e só sai daí por ato de gente.</summary>
public enum StatusOrcamento
{
    Rascunho,
    Enviada,
    Aceita,
    Recusada,
}

/// <summary>De quanto tempo é o plantão contratado. Muda o preço e muda a promessa.</summary>
public enum CoberturaOrcamento
{
    /// <summary>Dias úteis, horário comercial.</summary>
    Comercial,
    /// <summary>Todo dia, 12 horas.</summary>
    Estendida,
    /// <summary>Sem intervalo. ⚠️ Só prometa com plantão de verdade por trás.</summary>
    VinteQuatroPorSete,
}

/// <summary>
/// Um orçamento de monitoramento gerenciado para um cliente em potencial.
///
/// POR QUE ISTO EXISTE COMO TABELA, e não como planilha: o levantamento que gera o preço é o
/// MESMO que vira a implantação depois — quantas máquinas, quais sistemas, quem hospeda, que
/// cobertura foi prometida. Numa planilha, essa informação morre no dia em que a proposta é
/// aceita, e a implantação recomeça perguntando tudo de novo ao cliente. Aqui ela fica.
///
/// ⚠️ OS VALORES SÃO GRAVADOS, não recalculados na hora de exibir. Uma proposta é uma promessa
/// com data: se o parâmetro de preço mudar amanhã, a proposta que o cliente recebeu ontem tem de
/// continuar dizendo o que dizia. Ver CalculadoraDeOrcamento.
/// </summary>
public class OrcamentoMonitoramento
{
    public int Id { get; set; }

    /// <summary>Número visível ao cliente: ORC-2026-000001. Gerado na criação, nunca reaproveitado.</summary>
    public required string Numero { get; set; }

    // ── Quem é o cliente ────────────────────────────────────────────────────────────────────
    public required string NomeEmpresa { get; set; }
    public string? Cnpj { get; set; }
    public string? Contato { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }

    /// <summary>
    /// A empresa já cadastrada no L'okta, quando a proposta virar cliente.
    ///
    /// ⚠️ Nulo enquanto é só proposta, e de propósito: criar a empresa no momento do orçamento
    /// encheria a lista de clientes de gente que nunca fechou — e o painel de todos os clientes
    /// passaria a contar prospecto como cliente.
    /// </summary>
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    // ── O levantamento ──────────────────────────────────────────────────────────────────────
    /// <summary>Estações de trabalho Windows.</summary>
    public int EstacoesWindows { get; set; }
    /// <summary>Estações/desktops Linux ou macOS.</summary>
    public int EstacoesOutras { get; set; }
    /// <summary>Servidores, de qualquer sistema.</summary>
    public int Servidores { get; set; }

    /// <summary>
    /// Quantos servidores respondem na internet.
    ///
    /// ⚠️ NÃO entra no preço por máquina — entra no ESFORÇO. Servidor exposto gera tentativa de
    /// invasão todo dia, e é o que enche a fila de quem atende. Sem perguntar isto, dois clientes
    /// com seis máquinas cada custariam o mesmo e dariam trabalho muito diferente.
    /// </summary>
    public int ServidoresExpostos { get; set; }

    /// <summary>
    /// O cliente hospeda o Wazuh na infraestrutura dele?
    ///
    /// Verdadeiro tira o VPS do custo e do preço. É também um argumento comercial: quem tem
    /// política própria de segurança costuma preferir que o dado não saia de casa.
    /// </summary>
    public bool HospedagemDoCliente { get; set; }

    /// <summary>Dias de histórico contratados. 90 é o padrão e cabe no disco do VPS básico.</summary>
    public int RetencaoDias { get; set; } = 90;

    public CoberturaOrcamento Cobertura { get; set; } = CoberturaOrcamento.Comercial;

    /// <summary>LGPD, ISO 27001, exigência de um cliente do cliente… Texto livre porque a resposta é.</summary>
    public string? Conformidade { get; set; }

    /// <summary>
    /// O cliente trata dados de CRIANÇAS E ADOLESCENTES — escola, creche, clínica pediátrica,
    /// curso infantil.
    ///
    /// ⚠️ NÃO É DETALHE: a LGPD dá a esses dados um regime PRÓPRIO (art. 14), com consentimento
    /// específico e destacado de um dos pais e o "melhor interesse" da criança como critério. Numa
    /// escola, praticamente todo o banco de dados cai nesse regime — e é isso que muda o tamanho
    /// da conversa sobre segurança da informação com o diretor.
    ///
    /// Liga o parágrafo específico no documento. Ver OrcamentoPdfService.
    /// </summary>
    public bool TrataDadosDeCriancas { get; set; }

    public string? Observacoes { get; set; }

    // ── O que foi calculado, e ficou ────────────────────────────────────────────────────────
    public int MaquinasTotal { get; set; }

    /// <summary>O que VALE — é este número que vai para o cliente e para o PDF.</summary>
    public decimal ValorImplantacao { get; set; }
    public decimal ValorMensal { get; set; }

    /// <summary>
    /// O valor escrito À MÃO, quando alguém decidiu fechar em outro número. Nulo = o preço é o
    /// que a conta deu.
    ///
    /// ⚠️ EXISTE PORQUE A CONTA É SUGESTÃO, NÃO SENTENÇA. Numa negociação o preço muda — some um
    /// desconto, entra um arredondamento, o cliente traz uma proposta concorrente. Sem um lugar
    /// para isso, quem vende sai da tela e manda o número por WhatsApp, e o sistema passa a
    /// guardar um preço que não é o combinado.
    ///
    /// ⚠️ GUARDADO SEPARADO do valor final de propósito: é o que permite a tela mostrar, lado a
    /// lado, "a conta deu X, você fechou em Y". Sobrescrever direto apagaria a diferença — e a
    /// diferença é o desconto, que é justamente o que se quer enxergar depois.
    /// </summary>
    public decimal? ValorImplantacaoManual { get; set; }
    public decimal? ValorMensalManual { get; set; }

    /// <summary>O que a conta deu, guardado para a comparação continuar existindo depois.</summary>
    public decimal ValorImplantacaoCalculado { get; set; }
    public decimal ValorMensalCalculado { get; set; }

    /// <summary>
    /// Custo direto mensal (VPS). ⚠️ NÚMERO INTERNO — nunca aparece na via do cliente.
    /// Existe para a margem ser visível na hora de decidir o desconto, e não depois.
    /// </summary>
    public decimal CustoDiretoMensal { get; set; }

    /// <summary>Como o total foi montado, em texto, do jeito que foi mostrado na tela.</summary>
    public string? MemoriaDeCalculo { get; set; }

    // ── Trilha ──────────────────────────────────────────────────────────────────────────────
    public StatusOrcamento Status { get; set; } = StatusOrcamento.Rascunho;
    public DateTimeOffset CriadaEm { get; set; } = DateTimeOffset.UtcNow;
    public string? CriadaPor { get; set; }
    public DateTimeOffset? EnviadaEm { get; set; }
    public DateTimeOffset? RespondidaEm { get; set; }

    /// <summary>
    /// Até quando o preço vale. Proposta sem validade é proposta que volta seis meses depois
    /// cobrando o preço de antes.
    /// </summary>
    public DateOnly? ValidaAte { get; set; }
}
