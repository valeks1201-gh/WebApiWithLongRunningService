using LongRunningService.Models;
using LongRunningService.Services.Interfaces;
using System.Threading.Channels;

namespace LongRunningService.Services.Implementations;

public class ProcessingQueueService : IHostedService, IDisposable
{
    private readonly Channel<ProcessingRequest> _processingChannel;
    private readonly IProcessorAffinityService _processorAffinityService;
    private readonly ILogger<ProcessingQueueService> _logger;
    private readonly List<Task> _workerTasks;
    private readonly int _maxConcurrentWorkers;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed = false;

    public ProcessingQueueService(
        IProcessorAffinityService processorAffinityService,
        ILogger<ProcessingQueueService> logger,
        IConfiguration configuration)
    {
        _processorAffinityService = processorAffinityService;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
        _workerTasks = new List<Task>();

        _maxConcurrentWorkers = configuration.GetValue("ProcessorSettings:MaxConcurrentWorkers",
            Math.Max(1, Environment.ProcessorCount));

        _processingChannel = Channel.CreateBounded<ProcessingRequest>(new BoundedChannelOptions(1000)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        _logger.LogInformation("ProcessingQueueService initialized with {WorkerCount} max workers", _maxConcurrentWorkers);
    }

    public ChannelWriter<ProcessingRequest> Writer => _processingChannel.Writer;
    public int QueuedCount => _processingChannel.Reader.Count;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting ProcessingQueueService with {WorkerCount} workers", _maxConcurrentWorkers);

        for (int i = 0; i < _maxConcurrentWorkers; i++)
        {
            var workerId = i;
            var workerTask = Task.Run(() => ProcessQueueAsync(workerId, _cancellationTokenSource.Token), cancellationToken);
            _workerTasks.Add(workerTask);
        }

        _logger.LogInformation("ProcessingQueueService started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping ProcessingQueueService...");

        _processingChannel.Writer.Complete();
        _cancellationTokenSource.Cancel();

        try
        {
            await Task.WhenAll(_workerTasks).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            _logger.LogInformation("ProcessingQueueService stopped gracefully");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Some workers did not complete within timeout period");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProcessingQueueService stop operation was cancelled");
        }
    }

    private async Task ProcessQueueAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker {WorkerId} started", workerId);

        try
        {
            await foreach (var processingRequest in _processingChannel.Reader.ReadAllAsync(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessSingleRequestAsync(workerId, processingRequest);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Worker {WorkerId} operation cancelled", workerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker {WorkerId} encountered an error", workerId);
        }

        _logger.LogInformation("Worker {WorkerId} stopped", workerId);
    }

    private async Task ProcessSingleRequestAsync(int workerId, ProcessingRequest processingRequest)
    {
        var startTime = DateTimeOffset.UtcNow;
        var processorId = -1;

        try
        {
            var dtStart = DateTime.UtcNow;
            _logger.LogInformation("Worker {WorkerId} processing request {RequestId}", workerId, processingRequest.Request.RequestId);

            // Try to acquire any available processor
            var acquisitionResult = await _processorAffinityService.TryAcquireAnyProcessorAsync(
                TimeSpan.FromSeconds(30), processingRequest.CancellationToken);

            if (!acquisitionResult.Success)
            {
                processingRequest.CompletionSource.SetResult(
                    ProcessorResult.BusyResult(acquisitionResult.Message!, processingRequest.Request.RequestId));
                return;
            }

            processorId = acquisitionResult.ProcessorId;

            _logger.LogInformation(
                "Worker {WorkerId} acquired processor {ProcessorId} for request {RequestId}",
                workerId, processorId, processingRequest.Request.RequestId);

            // Execute the actual long-running task
            var result = await ExecuteLongRunningTaskAsync(
                processingRequest.Request,
                processorId,
                workerId,
                processingRequest.CancellationToken);

            var processingTime = DateTimeOffset.UtcNow - startTime;

            processingRequest.CompletionSource.SetResult(ProcessorResult.SuccessResult(
                result,
                $"Completed by worker {workerId} on processor {processorId}",
                processingRequest.Request.RequestId,
                processorId.ToString(),
                workerId.ToString(),
                processingTime));

            _logger.LogInformation(
                "Worker {WorkerId} completed request {RequestId} in {ProcessingTimeMs}ms",
                workerId, processingRequest.Request.RequestId, processingTime.TotalMilliseconds);
            var dtEnd = DateTime.UtcNow;
            _logger.LogInformation($"ProcessingQueueService.ProcessSingleRequestAsync. TotalDuration=" + (dtEnd - dtStart).TotalMilliseconds.ToString() + ". processorId="+ processorId.ToString());
        }
        catch (OperationCanceledException)
        {
            processingRequest.CompletionSource.SetResult(
                ProcessorResult.CancelledResult("Request was cancelled", processingRequest.Request.RequestId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker {WorkerId} error processing request {RequestId}", workerId, processingRequest.Request.RequestId);
            processingRequest.CompletionSource.SetResult(
                ProcessorResult.ErrorResult("Processing failed", ex.Message, processingRequest.Request.RequestId));
        }
        finally
        {
            if (processorId != -1)
            {
                _processorAffinityService.ReleaseProcessor(processorId);
                _logger.LogDebug("Worker {WorkerId} released processor {ProcessorId}", workerId, processorId);
            }
        }
    }

    private async Task<string> ExecuteLongRunningTaskAsync(LongRunningRequest request, int processorId, int workerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Worker {WorkerId} starting long-running task on processor {ProcessorId} for request {RequestId} ({Iterations} iterations)",
            workerId, processorId, request.RequestId, request.Iterations);

        // Simulate long-running work - REPLACE WITH YOUR ACTUAL PROCESSING LOGIC
        for (int i = 0; i < request.Iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simulate CPU-intensive work
            await Task.Delay(1000, cancellationToken);

            // Optional: Add actual processing logic here
            ProcessIteration(request.Data, i);

            if ((i + 1) % 5 == 0 || i == 0)
            {
                _logger.LogInformation(
                    "Worker {WorkerId} on processor {ProcessorId} - Progress {Current}/{Total} for request {RequestId}",
                    workerId, processorId, i + 1, request.Iterations, request.RequestId);
            }
        }

        return $"Processed '{request.Data}' through {request.Iterations} iterations on processor {processorId}";
    }

    private void ProcessIteration(string data, int iteration)
    {
        // Replace with your actual processing logic
        // This is where you'd do your CPU-intensive work
        // Example: image processing, data analysis, machine learning inference, etc.
        var result = data.ToUpper().ToCharArray();
        Array.Reverse(result);
        Thread.Sleep(10); // Simulate small CPU work
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            foreach (var task in _workerTasks)
            {
                task?.Dispose();
            }
            _logger.LogInformation("ProcessingQueueService disposed");
        }
    }

    public record ProcessingRequest(
        LongRunningRequest Request,
        TaskCompletionSource<ProcessorResult> CompletionSource,
        CancellationToken CancellationToken);
}