using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Fixon.Infrastructure.BackgroundJobs.Hosted;

public sealed class BackgroundJobHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackgroundJobRunnerOptions _options;
    private readonly ILogger<BackgroundJobHostedService> _logger;

    public BackgroundJobHostedService(
        IServiceScopeFactory scopeFactory,
        BackgroundJobRunnerOptions options,
        ILogger<BackgroundJobHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundJobHostedService started. poll={PollInterval} worker={WorkerId}",
            _options.PollInterval, _options.WorkerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<BackgroundJobRunner>();
                await runner.RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job runner iteration failed.");
            }

            await Task.Delay(_options.PollInterval, stoppingToken);
        }
    }
}

