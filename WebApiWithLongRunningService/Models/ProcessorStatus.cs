namespace LongRunningService.Models;

public record ProcessorStatus
{
    public bool IsRunning { get; init; }
    public int TotalProcessors { get; init; }
    public int AvailableProcessors { get; init; }
    public int BusyProcessors { get; init; }
    public int QueuedRequests { get; init; }
    public int ActiveWorkers { get; init; }
    public DateTimeOffset StatusTime { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string> Metrics { get; init; } = new Dictionary<string, string>();

    public double UtilizationPercentage => TotalProcessors > 0 ? (BusyProcessors / (double)TotalProcessors) * 100 : 0;
}