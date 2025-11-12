namespace LongRunningService.Models;

public record LongRunningRequest
{
    public string RequestId { get; init; } = Guid.NewGuid().ToString();
    public int Iterations { get; init; } = 10;
    public string Data { get; init; } = string.Empty;
    public string? Priority { get; init; }
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? ClientId { get; init; }

    public LongRunningRequest() { }

    public LongRunningRequest(string data, int iterations = 10, string? priority = null, string? clientId = null)
    {
        Data = data;
        Iterations = iterations;
        Priority = priority;
        ClientId = clientId;
    }
}