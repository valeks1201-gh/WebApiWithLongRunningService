using LongRunningService.Models;
using LongRunningService.Services.Interfaces;
using System.Threading.Channels;

namespace LongRunningService.Services.Implementations;

public class LongRunningProcessorService : ILongRunningProcessor
{
    private readonly ProcessingQueueService _queueService;
    private readonly IProcessorAffinityService _affinityService;
    private readonly ILogger<LongRunningProcessorService> _logger;

    public LongRunningProcessorService(
        ProcessingQueueService queueService,
        IProcessorAffinityService affinityService,
        ILogger<LongRunningProcessorService> logger)
    {
        _queueService = queueService;
        _affinityService = affinityService;
        _logger = logger;
    }

    public async Task<ProcessorResult> ProcessLongRunningTaskAsync(
        LongRunningRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Queueing long-running request {RequestId} from client {ClientId}",
            request.RequestId, request.ClientId);

        var completionSource = new TaskCompletionSource<ProcessorResult>();
        var processingRequest = new ProcessingQueueService.ProcessingRequest(request, completionSource, cancellationToken);

        try
        {
            await _queueService.Writer.WriteAsync(processingRequest, cancellationToken);

            _logger.LogDebug("Request {RequestId} queued successfully. Queue length: {QueueLength}",
                request.RequestId, _queueService.QueuedCount);

            var result = await completionSource.Task;
            return result;
        }
        catch (ChannelClosedException ex)
        {
            _logger.LogError(ex, "Failed to queue request {RequestId} - service is shutting down", request.RequestId);
            return ProcessorResult.ErrorResult("Service unavailable", "Processor is shutting down", request.RequestId);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Request {RequestId} was cancelled while queuing", request.RequestId);
            return ProcessorResult.CancelledResult("Request cancelled while queuing", request.RequestId);
        }
    }

    public async Task<ProcessorStatus> GetStatusAsync()
    {
        var processorInfo = _affinityService.GetProcessorInfo();
        var busyProcessors = processorInfo.Count(p => !p.IsAvailable);

        return new ProcessorStatus
        {
            IsRunning = true, // Assuming service is always running when called
            TotalProcessors = _affinityService.TotalProcessors,
            AvailableProcessors = _affinityService.AvailableProcessors,
            BusyProcessors = busyProcessors,
            QueuedRequests = _queueService.QueuedCount,
            ActiveWorkers = -1, // Not tracked in this simple implementation
            Metrics = new Dictionary<string, string>
            {
                ["UtilizationPercentage"] = _affinityService.UtilizationPercentage.ToString("F2"),
                ["QueueLength"] = _queueService.QueuedCount.ToString(),
                ["TotalProcessors"] = _affinityService.TotalProcessors.ToString()
            }
        };
    }
}