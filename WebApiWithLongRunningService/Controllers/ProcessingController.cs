using LongRunningService.Models;
using LongRunningService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LongRunningService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessingController : ControllerBase
{
    private readonly ILongRunningProcessor _processor;
    private readonly ILogger<ProcessingController> _logger;

    public ProcessingController(
        ILongRunningProcessor processor,
        ILogger<ProcessingController> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    [HttpPost("LongRunning")]
    public async Task<IActionResult> StartLongRunningProcess([FromBody] LongRunningRequest request)
    {
        _logger.LogInformation(
            "Received long-running request {RequestId} from client {ClientId} for {Iterations} iterations",
            request.RequestId, request.ClientId, request.Iterations);

        try
        {
            var result = await _processor.ProcessLongRunningTaskAsync(request, HttpContext.RequestAborted);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Request {RequestId} completed successfully in {ProcessingTimeMs}ms",
                    request.RequestId, result.ProcessingTime.TotalMilliseconds);

                return Ok(new
                {
                    RequestId = result.RequestId,
                    Result = result.Result,
                    Message = result.Message,
                    ProcessorId = result.ProcessorId,
                    WorkerId = result.WorkerId,
                    ProcessingTimeMs = result.ProcessingTime.TotalMilliseconds,
                    CompletedAt = result.CompletedAt
                });
            }
            else
            {
                _logger.LogWarning(
                    "Request {RequestId} failed: {Error} - {Message}",
                    request.RequestId, result.Error, result.Message);

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    RequestId = result.RequestId,
                    Error = result.Error,
                    Message = result.Message,
                    CompletedAt = result.CompletedAt
                });
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request {RequestId} was cancelled by client", request.RequestId);
            return StatusCode(StatusCodes.Status499ClientClosedRequest, new
            {
                RequestId = request.RequestId,
                Error = "Request cancelled by client"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request {RequestId}", request.RequestId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                RequestId = request.RequestId,
                Error = "Internal server error",
                Details = ex.Message
            });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var status = await _processor.GetStatusAsync();

            return Ok(new
            {
                status.IsRunning,
                status.TotalProcessors,
                status.AvailableProcessors,
                status.BusyProcessors,
                status.QueuedRequests,
                status.ActiveWorkers,
                UtilizationPercentage = status.UtilizationPercentage,
                StatusTime = status.StatusTime,
                Metrics = status.Metrics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processor status");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Error = "Failed to get status",
                Details = ex.Message
            });
        }
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> StartBulkProcessing([FromBody] BulkProcessingRequest bulkRequest)
    {
        var tasks = new List<Task<ProcessorResult>>();

        _logger.LogInformation("Starting bulk processing of {Count} requests", bulkRequest.Requests.Count);

        foreach (var request in bulkRequest.Requests)
        {
            tasks.Add(_processor.ProcessLongRunningTaskAsync(request, HttpContext.RequestAborted));
        }

        try
        {
            var results = await Task.WhenAll(tasks);

            var successful = results.Count(r => r.Success);
            var failed = results.Count(r => !r.Success);

            _logger.LogInformation("Bulk processing completed: {Successful} successful, {Failed} failed", successful, failed);

            return Ok(new
            {
                Total = results.Length,
                Successful = successful,
                Failed = failed,
                Results = results.Select(r => new {
                    r.RequestId,
                    r.Success,
                    r.Message,
                    r.Error,
                    ProcessingTimeMs = r.ProcessingTime.TotalMilliseconds
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk processing");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Error = "Bulk processing failed",
                Details = ex.Message
            });
        }
    }

    public record BulkProcessingRequest(List<LongRunningRequest> Requests);
}