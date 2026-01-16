using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fixon.Infrastructure.BackgroundJobs.Hosted;

public sealed class BackgroundJobHostedService : BackgroundService
{
    private readonly BackgroundJobRunner _runner;
    private readonly BackgroundJobRunnerOptions _options;
    private readonly ILogger<BackgroundJobHostedService> _logger;

    public BackgroundJobHostedService(
        BackgroundJobRunner runner,
        BackgroundJobRunnerOptions options,
        ILogger<BackgroundJobHostedService> logger)
    {
        _runner = runner;
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
                await _runner.RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job runner iteration failed.");
            }

            await Task.Delay(_options.PollInterval, stoppingToken);
        }
    }
}

