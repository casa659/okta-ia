using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Integracoes;

/// <summary>
/// Sincronização automática dos conectores instalados. Sem isto, a plataforma só se alimenta quando
/// alguém clica em "Sincronizar agora" — e o gestor que a compra justamente para não precisar olhar
/// cada ferramenta acabaria tendo que olhar esta também.
///
/// Mesmo desenho do <see cref="ScanAgendadorService"/>: BackgroundService é singleton e o DbContext
/// é scoped, então cada ciclo abre o próprio escopo.
///
/// Diferença importante de intervalo: alerta de EDR/SIEM tem valor em minutos, não em dias como o
/// scanner de superfície externa. Por isso o padrão aqui é 15 minutos por conector, e não 24 horas.
/// </summary>
public class SyncAgendadorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<SyncAgendadorService> _log;

    public SyncAgendadorService(IServiceScopeFactory scopes, IConfiguration config, ILogger<SyncAgendadorService> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    /// <summary>Intervalo entre sincronizações do MESMO conector.</summary>
    private TimeSpan IntervaloPorConector =>
        TimeSpan.FromMinutes(_config.GetValue("Integracoes:SyncIntervaloMinutos", 15d));

    /// <summary>De quanto em quanto tempo o serviço acorda pra ver quem venceu. É a granularidade da fila.</summary>
    private TimeSpan IntervaloDoCiclo =>
        TimeSpan.FromMinutes(_config.GetValue("Integracoes:SyncCicloMinutos", 5d));

    /// <summary>Teto por ciclo, pra ligar muitos conectores de uma vez não disparar tudo junto.</summary>
    private int MaxPorCiclo => _config.GetValue("Integracoes:SyncMaxPorCiclo", 5);

    private bool Habilitado => _config.GetValue("Integracoes:SyncAgendadorHabilitado", true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Habilitado)
        {
            _log.LogInformation("Agendador de sync desabilitado por configuração (Integracoes:SyncAgendadorHabilitado=false).");
            return;
        }

        // Espera antes do primeiro ciclo: no boot, migration e seed ainda estão rodando, e disparar
        // sync no meio disso competiria por conexão de banco à toa.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecutarCicloAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Um conector quebrado não pode derrubar o laço — o próximo ciclo tenta de novo.
                _log.LogError(ex, "Ciclo do agendador de sync falhou.");
            }

            try
            {
                await Task.Delay(IntervaloDoCiclo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ExecutarCicloAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var motor = scope.ServiceProvider.GetRequiredService<MotorDeSync>();
        var protetor = scope.ServiceProvider.GetRequiredService<ProtetorDeCredencial>();

        // Sem chave de criptografia não há como decifrar credencial: sincronizar só produziria uma
        // enxurrada de erro idêntico a cada ciclo. Melhor não tentar e dizer o motivo uma vez.
        if (!protetor.Configurado)
        {
            _log.LogWarning("Agendador de sync ocioso: Integracoes:ChaveCriptografia não está definida.");
            return;
        }

        var limite = DateTimeOffset.UtcNow - IntervaloPorConector;

        // Só conector ATIVO entra. Pausado é escolha do gestor; Erro e NuncaConectado esperam alguém
        // testar a conexão — insistir sozinho em credencial errada rende bloqueio no fabricante.
        var vencidos = await db.Conectores
            .Where(c => c.Status == StatusConector.Ativo
                        && (c.UltimoSyncEm == null || c.UltimoSyncEm < limite))
            .OrderBy(c => c.UltimoSyncEm)   // nunca sincronizado primeiro, depois o mais antigo
            .Take(MaxPorCiclo)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (vencidos.Count == 0)
        {
            return;
        }

        foreach (var conectorId in vencidos)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var resumo = await motor.ExecutarAsync(conectorId, EscopoSync.Alertas, automatico: true, ct);
            _log.LogInformation("Sync automático do conector {Id}: sucesso={Sucesso}, lidos={Lidos}, novos={Novos}.",
                conectorId, resumo.Sucesso, resumo.Lidos, resumo.Novos);
        }
    }
}
