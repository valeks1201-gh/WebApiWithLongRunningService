using LongRunningService.Models;

namespace LongRunningService.Services.Interfaces;

public interface IProcessorAffinityService
{
    Task<bool> TryAcquireProcessorAsync(int processorId, CancellationToken cancellationToken = default);
    Task<ProcessorAcquisitionResult> TryAcquireAnyProcessorAsync(CancellationToken cancellationToken = default);
    Task<ProcessorAcquisitionResult> TryAcquireAnyProcessorAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    void ReleaseProcessor(int processorId);
    int GetAvailableProcessor();
    ProcessorInfo[] GetProcessorInfo();
    int TotalProcessors { get; }
    int AvailableProcessors { get; }
    int BusyProcessors { get; }
    double UtilizationPercentage { get; }
}

public record ProcessorAcquisitionResult
{
    public bool Success { get; init; }
    public int ProcessorId { get; init; } = -1;
    public string? Message { get; init; }
    public TimeSpan WaitTime { get; init; }

    public static ProcessorAcquisitionResult Successful(int processorId, TimeSpan waitTime)
        => new() { Success = true, ProcessorId = processorId, WaitTime = waitTime };

    public static ProcessorAcquisitionResult Failed(string message, TimeSpan waitTime = default)
        => new() { Success = false, Message = message, WaitTime = waitTime };
}

public record ProcessorInfo
{
    public int Id { get; init; }
    public bool IsAvailable { get; init; }
    public DateTimeOffset? AcquiredSince { get; init; }
    public string? CurrentRequestId { get; init; }
    public TimeSpan? HoldDuration => AcquiredSince.HasValue ? DateTimeOffset.UtcNow - AcquiredSince.Value : null;
}