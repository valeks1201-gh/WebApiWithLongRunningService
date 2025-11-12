namespace LongRunningService.Models;

public record ProcessorResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Result { get; init; }
    public string? Error { get; init; }
    public string? ProcessorId { get; init; }
    public string? WorkerId { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public string RequestId { get; init; } = string.Empty;

    private ProcessorResult(bool success, string message, string? result = null, string? error = null,
                          string? processorId = null, string? workerId = null, TimeSpan? processingTime = null,
                          string requestId = "")
    {
        Success = success;
        Message = message;
        Result = result;
        Error = error;
        ProcessorId = processorId;
        WorkerId = workerId;
        ProcessingTime = processingTime ?? TimeSpan.Zero;
        CompletedAt = DateTimeOffset.UtcNow;
        RequestId = requestId;
    }

    public static ProcessorResult SuccessResult(string result, string message, string requestId,
        string? processorId = null, string? workerId = null, TimeSpan? processingTime = null)
        => new(true, message, result, null, processorId, workerId, processingTime, requestId);

    public static ProcessorResult BusyResult(string message, string requestId)
        => new(false, message, error: "Processor busy", requestId: requestId);

    public static ProcessorResult ErrorResult(string message, string error, string requestId, string? processorId = null)
        => new(false, message, error: error, processorId: processorId, requestId: requestId);

    public static ProcessorResult CancelledResult(string message, string requestId)
        => new(false, message, error: "Operation cancelled", requestId: requestId);
}