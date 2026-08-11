using FinAI.Api.Services.OpenFinance.Options;
using Microsoft.Extensions.Options;

namespace FinAI.Api.Services.OpenFinance.Background;

/// <summary>
/// Sincronização agendada (Modo A): roda o sync do usuário dono diariamente (configurável).
/// Ativado por Pluggy:ScheduleEnabled.
/// </summary>
public class OpenFinanceSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OpenFinanceSyncHostedService> _logger;

    public OpenFinanceSyncHostedService(IServiceScopeFactory scopeFactory, ILogger<OpenFinanceSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var options = scope.ServiceProvider.GetRequiredService<IOptions<PluggyOptions>>().Value;
                var interval = TimeSpan.FromHours(options.ScheduleIntervalHours);

                if (options.ScheduleEnabled && !string.IsNullOrWhiteSpace(options.ItemId))
                {
                    await RunScheduledSyncAsync(scope, options, stoppingToken);
                }

                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled Open Finance sync failed");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private static async Task RunScheduledSyncAsync(IServiceScope scope, PluggyOptions options, CancellationToken stoppingToken)
    {
        // Modo A: o sync usa o ItemId configurado; o UserId dono é o primeiro usuário com o item configurado.
        // Por simplicidade no MVP, o agendamento roda para o usuário que possui o ItemId (via connections)
        // — implementação completa com dono explícito fica na v1.0.
        var sync = scope.ServiceProvider.GetRequiredService<IOpenFinanceSyncService>();
        var repository = scope.ServiceProvider.GetRequiredService<FinAI.Api.Repositories.IOpenFinanceRepository>();

        // Busca usuários com conexões vinculadas ao ItemId configurado
        // (Modo A: o ItemId do Meu Pluggy pertence ao usuário que o configurou)
        var users = await repository.ListUsersWithItemAsync(options.ItemId, stoppingToken);
        foreach (var userId in users)
        {
            await sync.SyncAsync(userId, options.ItemId, stoppingToken);
        }
    }
}