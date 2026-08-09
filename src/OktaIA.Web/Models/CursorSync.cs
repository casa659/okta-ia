namespace OktaIA.Web.Models;

/// <summary>O que está sendo sincronizado. Nem todo fabricante oferece todos.</summary>
public enum EscopoSync { Alertas, Ativos, Incidentes, Vulnerabilidades, Eventos, Usuarios }

/// <summary>
/// Posição da última sincronização incremental, por conector e escopo. É o que permite o sync ser
/// retomável: em vez de reler tudo a cada ciclo, o adaptador pede "o que mudou depois deste ponto".
///
/// O <see cref="Valor"/> é OPACO de propósito — cada fabricante usa uma coisa (timestamp ISO,
/// id sequencial, continuation token). Só o adaptador que gravou sabe interpretar; o motor de sync
/// apenas guarda e devolve.
/// </summary>
public class CursorSync
{
    public int Id { get; set; }

    public int ConectorId { get; set; }
    public Conector? Conector { get; set; }

    public EscopoSync Escopo { get; set; }

    public string? Valor { get; set; }

    public DateTimeOffset? UltimoSyncEm { get; set; }
    public int ItensNoUltimoSync { get; set; }
}
