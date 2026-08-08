using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;

namespace OktaIA.Web.Services;

// Revarredura automática dos ativos reais autorizados — o que transforma o scanner de laudo
// pontual ("como estava no dia em que alguém clicou") em monitoramento contínuo ("avisamos quando
// mudou"). Sem isto, a promessa de varredura contínua da proposta comercial não existe no produto.
//
// BackgroundService é singleton e ApplicationDbContext/ScanExecutor são scoped, por isso cada
// ciclo abre o próprio escopo em vez de injetar os serviços no construtor.
public class ScanAgendadorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<ScanAgendadorService> _log;

    public ScanAgendadorService(IServiceScopeFactory scopes, IConfiguration config, ILogger<ScanAgendadorService> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    // Intervalo entre varreduras do MESMO ativo. 24h por padrão: as checagens são de superfície
    // externa (certificado, cabeçalho, DNS, porta), que mudam em escala de dias — varrer de hora
    // em hora não acharia mais nada e só geraria tráfego repetido contra o domínio do cliente.
    private TimeSpan IntervaloPorAtivo =>
        TimeSpan.FromHours(_config.GetValue("Scanner:IntervaloHoras", 24d));

    // De quanto em quanto tempo o serviço acorda pra ver se algum ativo venceu o intervalo. Não é
    // a frequência de scan — é a granularidade da fila.
    private TimeSpan IntervaloDoCiclo =>
        TimeSpan.FromMinutes(_config.GetValue("Scanner:CicloMinutos", 15d));

    // Teto de ativos escaneados por ciclo. Evita que, ao ligar o agendador num banco com muitos
    // ativos vencidos (ou depois de o app ficar parado), tudo dispare de uma vez.
    private int MaxPorCiclo => _config.GetValue("Scanner:MaxPorCiclo", 5);

    private bool Habilitado => _config.GetValue("Scanner:AgendadorHabilitado", true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Habilitado)
        {
            _log.LogInformation("Agendador de scan desabilitado por configuração (Scanner:AgendadorHabilitado=false).");
            return;
        }

        _log.LogInformation("Agendador de scan ativo — ciclo a cada {Ciclo}, revarredura por ativo a cada {Intervalo}.",
            IntervaloDoCiclo, IntervaloPorAtivo);

        // Espera antes do primeiro ciclo: no startup o app ainda está aplicando migration e
        // rodando o seed, e não há por que competir com isso.
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
                // Um domínio fora do ar, DNS instável ou erro de banco não pode matar o serviço —
                // ele precisa continuar tentando nos ciclos seguintes.
                _log.LogError(ex, "Falha no ciclo do agendador de scan; segue no próximo ciclo.");
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
        var executor = scope.ServiceProvider.GetRequiredService<ScanExecutor>();

        var limite = DateTimeOffset.UtcNow - IntervaloPorAtivo;

        var vencidos = await db.Assets
            .Where(a => a.Real && a.AutorizadoParaScan && a.MonitoramentoContinuo
                        && (a.UltimoScanEm == null || a.UltimoScanEm < limite))
            .OrderBy(a => a.UltimoScanEm)   // nunca escaneado primeiro, depois o mais antigo
            .Take(MaxPorCiclo)
            .ToListAsync(ct);

        if (vencidos.Count == 0)
        {
            return;
        }

        foreach (var asset in vencidos)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await executor.ExecutarAsync(asset, automatico: true, ct);
            }
            catch (Exception ex)
            {
                // Falha em um ativo não interrompe a fila dos demais. UltimoScanEm continua
                // antigo, então ele volta a ser candidato no próximo ciclo naturalmente.
                _log.LogWarning(ex, "Scan automático falhou para {Dominio}.", asset.Nome);
            }
        }
    }
}
