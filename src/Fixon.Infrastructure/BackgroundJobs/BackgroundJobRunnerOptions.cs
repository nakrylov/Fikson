namespace Fixon.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobRunnerOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public string WorkerId { get; set; } = Environment.MachineName;
}

