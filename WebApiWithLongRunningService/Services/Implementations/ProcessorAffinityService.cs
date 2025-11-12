using LongRunningService.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace LongRunningService.Services.Implementations;

public class ProcessorAffinityService : IProcessorAffinityService, IDisposable
{
    private readonly ConcurrentDictionary<int, ProcessorLock> _processorLocks;
    private readonly ILogger<ProcessorAffinityService> _logger;
    private readonly ProcessorAffinityConfig _config;
    private bool _disposed = false;

    public ProcessorAffinityService(
        ILogger<ProcessorAffinityService> logger,
        IOptions<ProcessorAffinityConfig> config)
    {
        _logger = logger;
        _config = config.Value;
        _processorLocks = new ConcurrentDictionary<int, ProcessorLock>();

        InitializeProcessors();
    }

    public int TotalProcessors => _config.TotalProcessors;
    public int AvailableProcessors => _processorLocks.Values.Count(lockObj => lockObj.IsAvailable);
    public int BusyProcessors => TotalProcessors - AvailableProcessors;
    public double UtilizationPercentage => TotalProcessors > 0 ? (BusyProcessors / (double)TotalProcessors) * 100 : 0;

    public async Task<bool> TryAcquireProcessorAsync(int processorId, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        if (processorId < 0 || processorId >= TotalProcessors)
            throw new ArgumentOutOfRangeException(nameof(processorId), $"Processor ID must be between 0 and {TotalProcessors - 1}");

        var processorLock = _processorLocks[processorId];
        var acquired = await processorLock.WaitAsync(TimeSpan.Zero, cancellationToken);

        if (acquired)
            _logger.LogDebug("Processor {ProcessorId} acquired successfully", processorId);
        else
            _logger.LogDebug("Processor {ProcessorId} is currently busy", processorId);

        return acquired;
    }

    public async Task<ProcessorAcquisitionResult> TryAcquireAnyProcessorAsync(CancellationToken cancellationToken = default)
    {
        return await TryAcquireAnyProcessorAsync(_config.DefaultAcquisitionTimeout, cancellationToken);
    }

    public async Task<ProcessorAcquisitionResult> TryAcquireAnyProcessorAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        var startTime = DateTimeOffset.UtcNow;
        var timeoutCts = new CancellationTokenSource(timeout);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                for (int processorId = 0; processorId < TotalProcessors; processorId++)
                {
                    if (linkedCts.Token.IsCancellationRequested)
                        break;

                    var processorLock = _processorLocks[processorId];

                    if (await processorLock.WaitAsync(TimeSpan.Zero, linkedCts.Token))
                    {
                        var waitTime = DateTimeOffset.UtcNow - startTime;
                        _logger.LogInformation("Acquired processor {ProcessorId} after {WaitTimeMs}ms", processorId, waitTime.TotalMilliseconds);
                        return ProcessorAcquisitionResult.Successful(processorId, waitTime);
                    }
                }

                if (!linkedCts.Token.IsCancellationRequested)
                    await Task.Delay(50, linkedCts.Token);
            }

            var totalWaitTime = DateTimeOffset.UtcNow - startTime;
            return ProcessorAcquisitionResult.Failed("No processors available within timeout", totalWaitTime);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            var totalWaitTime = DateTimeOffset.UtcNow - startTime;
            return ProcessorAcquisitionResult.Failed("Acquisition timeout", totalWaitTime);
        }
        catch (OperationCanceledException)
        {
            var totalWaitTime = DateTimeOffset.UtcNow - startTime;
            return ProcessorAcquisitionResult.Failed("Acquisition cancelled", totalWaitTime);
        }
        finally
        {
            timeoutCts.Dispose();
            linkedCts.Dispose();
        }
    }

    public void ReleaseProcessor(int processorId)
    {
        EnsureNotDisposed();

        if (processorId < 0 || processorId >= TotalProcessors)
            throw new ArgumentOutOfRangeException(nameof(processorId), $"Processor ID must be between 0 and {TotalProcessors - 1}");

        if (_processorLocks.TryGetValue(processorId, out var processorLock))
        {
            try
            {
                processorLock.Release();
                _logger.LogDebug("Processor {ProcessorId} released", processorId);
            }
            catch (SemaphoreFullException ex)
            {
                _logger.LogWarning(ex, "Attempted to release processor {ProcessorId} that was not acquired", processorId);
            }
        }
    }

    public int GetAvailableProcessor()
    {
        EnsureNotDisposed();

        for (int processorId = 0; processorId < TotalProcessors; processorId++)
        {
            if (_processorLocks[processorId].IsAvailable)
                return processorId;
        }

        return -1;
    }

    public ProcessorInfo[] GetProcessorInfo()
    {
        EnsureNotDisposed();

        var processorInfo = new ProcessorInfo[TotalProcessors];

        for (int i = 0; i < TotalProcessors; i++)
        {
            var processorLock = _processorLocks[i];
            processorInfo[i] = new ProcessorInfo
            {
                Id = i,
                IsAvailable = processorLock.IsAvailable,
                AcquiredSince = processorLock.AcquiredSince,
                CurrentRequestId = processorLock.CurrentRequestId
            };
        }

        return processorInfo;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            foreach (var processorLock in _processorLocks.Values)
                processorLock.Dispose();
            _processorLocks.Clear();
            _logger.LogInformation("ProcessorAffinityService disposed");
        }
    }

    private void InitializeProcessors()
    {
        _logger.LogInformation("Initializing ProcessorAffinityService with {ProcessorCount} processors", TotalProcessors);

        for (int i = 0; i < TotalProcessors; i++)
            _processorLocks[i] = new ProcessorLock(i, _logger);

        _logger.LogInformation("ProcessorAffinityService initialized successfully");
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProcessorAffinityService));
    }

    private class ProcessorLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger _logger;
        private readonly int _processorId;
        private DateTimeOffset? _acquiredSince;
        private string _currentRequestId;
        private bool _disposed = false;

        public ProcessorLock(int processorId, ILogger logger)
        {
            _processorId = processorId;
            _logger = logger;
            _semaphore = new SemaphoreSlim(1, 1);
        }

        public bool IsAvailable => _semaphore.CurrentCount > 0;
        public DateTimeOffset? AcquiredSince => _acquiredSince;
        public string CurrentRequestId => _currentRequestId;

        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (_disposed) return false;

            var acquired = await _semaphore.WaitAsync(timeout, cancellationToken);

            if (acquired)
            {
                _acquiredSince = DateTimeOffset.UtcNow;
                _currentRequestId = Guid.NewGuid().ToString();
            }

            return acquired;
        }

        public void Release()
        {
            if (_disposed) return;

            try
            {
                _semaphore.Release();
            }
            finally
            {
                _acquiredSince = null;
                _currentRequestId = null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _semaphore?.Dispose();
            }
        }
    }
}

public class ProcessorAffinityConfig
{
    public int TotalProcessors { get; set; } = Environment.ProcessorCount;
    public TimeSpan DefaultAcquisitionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}