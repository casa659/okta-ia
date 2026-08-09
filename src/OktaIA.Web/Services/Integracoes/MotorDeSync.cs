using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Integracoes;

/// <summary>Resumo de uma rodada, pra UI e log.</summary>
public record ResumoSync(bool Sucesso, int Lidos, int Novos, string? Erro);

/// <summary>
/// Orquestra a sincronização de um conector: decifra a credencial, chama o adaptador em laço de
/// páginas, grava só o que é inédito, avança o cursor e registra a execução.
///
/// O adaptador sabe falar com o fabricante; o motor sabe o que fazer com o resultado. Essa divisão
/// é o que faz um fabricante novo custar só uma classe.
/// </summary>
public class MotorDeSync
{
    private readonly ApplicationDbContext _db;
    private readonly RegistroDeConectores _registro;
    private readonly ProtetorDeCredencial _protetor;
    private readonly ILogger<MotorDeSync> _log;

    /// <summary>
    /// Teto de páginas por rodada. Sem isso, a primeira carga de um SIEM ruidoso rodaria por horas
    /// segurando uma conexão de banco. O que sobrar entra na próxima rodada — o cursor já avançou.
    /// </summary>
    private const int MaxPaginasPorRodada = 20;

    public MotorDeSync(ApplicationDbContext db, RegistroDeConectores registro,
        ProtetorDeCredencial protetor, ILogger<MotorDeSync> log)
    {
        _db = db;
        _registro = registro;
        _protetor = protetor;
        _log = log;
    }

    public async Task<ResumoSync> ExecutarAsync(int conectorId, EscopoSync escopo, bool automatico, CancellationToken ct)
    {
        var conector = await _db.Conectores
            .Include(c => c.Credencial)
            .FirstOrDefaultAsync(c => c.Id == conectorId, ct);

        if (conector is null)
        {
            return new ResumoSync(false, 0, 0, "Conector não encontrado.");
        }

        var adaptador = _registro.Resolver(conector.Slug);
        if (adaptador is null)
        {
            return await FalharAsync(conector, escopo, automatico,
                $"Nenhum adaptador registrado para o slug '{conector.Slug}'.", ct);
        }

        if (conector.Credencial is null)
        {
            return await FalharAsync(conector, escopo, automatico, "Conector sem credencial cadastrada.", ct);
        }

        var execucao = new ExecucaoSync
        {
            ConectorId = conector.Id,
            Escopo = escopo,
            Automatico = automatico,
        };
        _db.ExecucoesSync.Add(execucao);
        await _db.SaveChangesAsync(ct);

        var lidos = 0;
        var novos = 0;

        try
        {
            var ctx = new ContextoConector(
                conector.Id,
                conector.CompanyId,
                conector.UrlBase,
                _protetor.Revelar(conector.Credencial.SegredoCifrado));

            var cursorEntity = await ObterOuCriarCursorAsync(conector.Id, escopo, ct);
            var cursor = cursorEntity.Valor;

            for (var pagina = 0; pagina < MaxPaginasPorRodada; pagina++)
            {
                ct.ThrowIfCancellationRequested();

                var resultado = await adaptador.SincronizarAsync(ctx, escopo, cursor, ct);
                lidos += resultado.Alertas.Count;
                novos += await GravarInéditosAsync(resultado.Alertas, conector.Id, ct);

                cursor = resultado.ProximoCursor;

                // Persistir o cursor A CADA página, não no fim: se a rodada morrer na página 7, as
                // 6 anteriores não são relidas na próxima. É o que torna o sync retomável de fato.
                cursorEntity.Valor = cursor;
                cursorEntity.UltimoSyncEm = DateTimeOffset.UtcNow;
                cursorEntity.ItensNoUltimoSync = resultado.Alertas.Count;
                await _db.SaveChangesAsync(ct);

                if (!resultado.TemMais)
                {
                    break;
                }
            }

            conector.Status = StatusConector.Ativo;
            conector.UltimoSyncEm = DateTimeOffset.UtcNow;
            conector.UltimoErro = null;
            conector.UltimoErroEm = null;

            execucao.FinalizadoEm = DateTimeOffset.UtcNow;
            execucao.ItensLidos = lidos;
            execucao.ItensNovos = novos;
            execucao.Sucesso = true;
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Sync {Slug}/{Escopo} do conector {Id}: {Lidos} lido(s), {Novos} novo(s).",
                conector.Slug, escopo, conector.Id, lidos, novos);

            return new ResumoSync(true, lidos, novos, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Sync do conector {Id} falhou.", conector.Id);

            execucao.FinalizadoEm = DateTimeOffset.UtcNow;
            execucao.ItensLidos = lidos;
            execucao.ItensNovos = novos;
            execucao.Sucesso = false;
            execucao.Erro = Truncar(ex.Message, 1000);

            conector.Status = StatusConector.Erro;
            conector.UltimoErro = Truncar(ex.Message, 1000);
            conector.UltimoErroEm = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new ResumoSync(false, lidos, novos, ex.Message);
        }
    }

    /// <summary>
    /// Grava só o que ainda não existe. Filtra ANTES de inserir em vez de deixar o índice único
    /// estourar: no Postgres, violar constraint aborta a transação inteira, então uma única
    /// repetição derrubaria o lote todo.
    /// </summary>
    private async Task<int> GravarInéditosAsync(IReadOnlyList<AlertaUnificado> alertas, int conectorId, CancellationToken ct)
    {
        if (alertas.Count == 0)
        {
            return 0;
        }

        var ids = alertas.Select(a => a.IdExterno).ToList();
        var jaExistem = await _db.AlertasUnificados
            .Where(a => a.ConectorId == conectorId && ids.Contains(a.IdExterno))
            .Select(a => a.IdExterno)
            .ToListAsync(ct);

        var existentes = jaExistem.ToHashSet();

        // Dedupe também DENTRO do lote: a mesma página pode trazer o mesmo _id duas vezes se o
        // índice tiver sido reescrito entre a leitura e a paginação.
        var inéditos = alertas
            .Where(a => !existentes.Contains(a.IdExterno))
            .GroupBy(a => a.IdExterno)
            .Select(g => g.First())
            .ToList();

        if (inéditos.Count == 0)
        {
            return 0;
        }

        _db.AlertasUnificados.AddRange(inéditos);
        await _db.SaveChangesAsync(ct);
        return inéditos.Count;
    }

    private async Task<CursorSync> ObterOuCriarCursorAsync(int conectorId, EscopoSync escopo, CancellationToken ct)
    {
        var cursor = await _db.CursoresSync
            .FirstOrDefaultAsync(c => c.ConectorId == conectorId && c.Escopo == escopo, ct);

        if (cursor is not null)
        {
            return cursor;
        }

        cursor = new CursorSync { ConectorId = conectorId, Escopo = escopo };
        _db.CursoresSync.Add(cursor);
        await _db.SaveChangesAsync(ct);
        return cursor;
    }

    private async Task<ResumoSync> FalharAsync(Conector conector, EscopoSync escopo, bool automatico,
        string erro, CancellationToken ct)
    {
        _db.ExecucoesSync.Add(new ExecucaoSync
        {
            ConectorId = conector.Id,
            Escopo = escopo,
            Automatico = automatico,
            FinalizadoEm = DateTimeOffset.UtcNow,
            Sucesso = false,
            Erro = erro,
        });

        conector.Status = StatusConector.Erro;
        conector.UltimoErro = erro;
        conector.UltimoErroEm = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ResumoSync(false, 0, 0, erro);
    }

    private static string Truncar(string texto, int max) =>
        texto.Length <= max ? texto : texto[..max];
}
