using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Integracoes;

/// <param name="Chave">Nome da chave no JSON da credencial.</param>
/// <param name="Rotulo">Como o campo aparece no formulário.</param>
/// <param name="Segredo">true = campo mascarado, nunca reexibido depois de salvo.</param>
public record CampoCredencial(string Chave, string Rotulo, bool Segredo);

/// <summary>O que o adaptador declara saber fazer. O motor de sync só pede o que está aqui.</summary>
/// <param name="ExemploUrlBase">
/// O que aparece esmaecido no campo "URL do serviço".
///
/// 🔴 POR ADAPTADOR (02/10/2026, visto pelo dono na tela de instalar o LinkEscola). A caixa sugeria
/// `https://10.0.0.5:9200` para TODO conector — o endereço do Wazuh Indexer, na rede interna do
/// cliente. Para um serviço público como o LinkEscola, aquilo manda a pessoa digitar um IP privado
/// numa integração que fala com a internet.
///
/// ⚠️ Exemplo errado é pior que campo vazio: o vazio faz perguntar, o errado faz copiar.
/// </param>
/// <param name="ComoObterCredencial">
/// Uma frase dizendo de onde sai a credencial DESTE produto.
///
/// 🔴 A caixa dizia "as credenciais são geradas pelo cliente, no console dele… use sempre um usuário
/// somente-leitura" para todos. Vale para o Wazuh, onde há usuário e senha no Indexer; no LinkEscola
/// não existe usuário nenhum — é uma CHAVE num App Setting. Instrução genérica manda procurar o que
/// não existe.
/// </param>
public record CapacidadesConector(
    string Slug,
    string Nome,
    string Categoria,
    string Fabricante,
    TipoAuthConector TipoAuth,
    IReadOnlyList<EscopoSync> Escopos,
    bool ExigeUrlBase,
    IReadOnlyList<CampoCredencial> CamposCredencial,
    string? ExemploUrlBase = null,
    string? ComoObterCredencial = null);

/// <summary>Resultado de testar a conexão. Não grava nada — é a checagem da instalação.</summary>
public record ResultadoTeste(bool Ok, string Mensagem, int? LatenciaMs = null, string? Referencia = null);

public record ResultadoSaude(bool Saudavel, int? LatenciaMs, string? Detalhe);

/// <summary>
/// Uma página de sincronização. O motor chama em laço enquanto <see cref="TemMais"/> for true,
/// gravando <see cref="ProximoCursor"/> a cada página — assim uma falha no meio não perde o que já
/// entrou, e a retomada continua de onde parou em vez de reler tudo.
/// </summary>
public record ResultadoSync(
    IReadOnlyList<AlertaUnificado> Alertas,
    string? ProximoCursor,
    bool TemMais);

/// <summary>Tudo que o adaptador precisa pra falar com a API do cliente, já decifrado.</summary>
public record ContextoConector(
    int ConectorId,
    int CompanyId,
    string? UrlBase,
    IReadOnlyDictionary<string, string> Credencial);

/// <summary>
/// Contrato de um adaptador de fabricante. Um arquivo por produto (WazuhConnector,
/// DefenderConnector…), sem estado entre chamadas.
///
/// Diferenças conscientes em relação ao rascunho da maquete (AdminCatalog.ConnectorInterface):
///
/// 1. Não existe connect()/disconnect(). Essas APIs são HTTP sem sessão — autentica-se por
///    requisição (ou com token de curta duração renovado internamente). Manter "sessão aberta"
///    seria estado falso, que quebra assim que o app escala para mais de uma instância.
///
/// 2. SincronizarAsync devolve página + cursor em vez de stream infinito. Streaming esconde o ponto
///    exato de retomada; com página explícita o motor persiste o cursor a cada lote e o sync fica
///    idempotente de verdade — que é o requisito que a própria maquete pedia.
/// </summary>
public interface IConnector
{
    CapacidadesConector Capacidades { get; }

    /// <summary>Valida credencial e alcance de rede. Não persiste nada.</summary>
    Task<ResultadoTeste> TestarConexaoAsync(ContextoConector ctx, CancellationToken ct);

    /// <summary>Leitura incremental. <paramref name="cursor"/> nulo = primeira carga.</summary>
    Task<ResultadoSync> SincronizarAsync(ContextoConector ctx, EscopoSync escopo, string? cursor, CancellationToken ct);

    Task<ResultadoSaude> VerificarSaudeAsync(ContextoConector ctx, CancellationToken ct);
}
