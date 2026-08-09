namespace OktaIA.Web.Models;

/// <summary>
/// Registro de cada rodada de sincronização. É o que dá lastro à tela de "Eventos e sync" e às
/// métricas de observabilidade (taxa de sucesso, defasagem, fila de retry) que hoje são números
/// escritos à mão em AdminCatalog.
///
/// Grava tanto sucesso quanto falha de propósito: conector que parou de funcionar em silêncio é o
/// pior modo de falha de uma plataforma que promete monitoramento contínuo.
/// </summary>
public class ExecucaoSync
{
    public int Id { get; set; }

    public int ConectorId { get; set; }
    public Conector? Conector { get; set; }

    public EscopoSync Escopo { get; set; }

    public DateTimeOffset IniciadoEm { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinalizadoEm { get; set; }

    public int ItensLidos { get; set; }

    /// <summary>Quantos eram inéditos — o resto foi descartado pela chave de idempotência.</summary>
    public int ItensNovos { get; set; }

    public bool Sucesso { get; set; }
    public string? Erro { get; set; }

    /// <summary>Disparado pelo agendador (true) ou por clique em "Sincronizar agora" (false).</summary>
    public bool Automatico { get; set; }
}
