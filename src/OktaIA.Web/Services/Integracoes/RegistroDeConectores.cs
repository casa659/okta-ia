namespace OktaIA.Web.Services.Integracoes;

/// <summary>
/// Resolve o adaptador certo a partir do slug gravado no banco. É a única ponte entre o registro de
/// <see cref="Models.Conector"/> (dado) e o código que sabe falar com aquele fabricante.
///
/// Adicionar um fabricante novo passa a ser: escrever a classe, registrar no DI e pronto — nem esta
/// classe nem o motor de sync mudam.
/// </summary>
public class RegistroDeConectores
{
    private readonly IReadOnlyList<IConnector> _conectores;

    public RegistroDeConectores(IEnumerable<IConnector> conectores)
    {
        _conectores = conectores.ToList();
    }

    /// <summary>Catálogo do que a plataforma realmente sabe integrar — diferente do marketplace de vitrine.</summary>
    public IReadOnlyList<CapacidadesConector> Disponiveis =>
        _conectores.Select(c => c.Capacidades).OrderBy(c => c.Nome).ToList();

    public IConnector? Resolver(string slug) =>
        _conectores.FirstOrDefault(c => string.Equals(c.Capacidades.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
