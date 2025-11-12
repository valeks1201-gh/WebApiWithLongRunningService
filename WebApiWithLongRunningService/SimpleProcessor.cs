using System.Diagnostics;
using  LongRunningService.Models;

public interface ISimpleProcessor
{
    Task<ProcessorResult> ProcessSimpleTaskAsync(SimpleRequest request, CancellationToken cancellationToken = default, bool isToLog = false);
}

public class SimpleProcessor : ISimpleProcessor
{
    private readonly ILogger<SimpleProcessor> _logger;
  
    public SimpleProcessor(ILogger<SimpleProcessor> logger)
    {
         _logger = logger;
    }

    public async Task<ProcessorResult> ProcessSimpleTaskAsync(
        SimpleRequest request,
        CancellationToken cancellationToken = default, bool isToLog=false)
    {
        var dtStart = DateTime.UtcNow;
        var result = await ExecuteSimpleTaskAsync(request, cancellationToken);
        var dtEnd = DateTime.UtcNow;
        if (isToLog)
        {
            _logger.LogInformation($"SimpleProcessor.ProcessSimpleTaskAsync. TotalDuration=" + (dtEnd - dtStart).TotalMilliseconds.ToString());
        }
        return ProcessorResult.SuccessResult("", $"Completed simple request", "");
    }

    private async Task<string> ExecuteSimpleTaskAsync(SimpleRequest request, CancellationToken cancellationToken)
    {
        // Simulate long-running work (replace with your actual logic)
        for (int i = 0; i < request.Iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Your actual work here
            await Task.Delay(1, cancellationToken);
        }

        return $"Processed {request.Iterations} iterations";
    }
}

public record SimpleRequest(int Iterations, string Data);

//public record ProcessorResult(bool Success, string Message, string? Result = null, string? Error = null)
//{
//    public static ProcessorResult SuccessResult(string result, string message) => new(true, message, result);
//    public static ProcessorResult BusyResult(string message) => new(false, message, Error: "Processor busy");
//    public static ProcessorResult ErrorResult(string message, string error) => new(false, message, Error: error);
//}
