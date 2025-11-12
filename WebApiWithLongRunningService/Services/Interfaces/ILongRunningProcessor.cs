using LongRunningService.Models;

namespace LongRunningService.Services.Interfaces;

public interface ILongRunningProcessor
{
    Task<ProcessorResult> ProcessLongRunningTaskAsync(
        LongRunningRequest request,
        CancellationToken cancellationToken = default);

    Task<ProcessorStatus> GetStatusAsync();
}