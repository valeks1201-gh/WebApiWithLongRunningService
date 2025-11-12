using Microsoft.AspNetCore.Mvc;
namespace LongRunningService.Models;

[ApiController]
[Route("api/[controller]")]
public class SimpleRequestController : ControllerBase
{
    private readonly ISimpleProcessor _simpleProcessor;
    private readonly ILogger<SimpleRequestController> _logger;

    public SimpleRequestController(
        ISimpleProcessor simpleProcessor,
        ILogger<SimpleRequestController> logger)
    {
        _simpleProcessor = simpleProcessor;
        _logger = logger;
    }

    [HttpPost("process/{isToLog:bool}")]
    public async Task<IActionResult> SimpleProcessRunning([FromBody] SimpleRequest request, bool isToLog = false)
    {
        try
        {
            if (isToLog) _logger.LogInformation("Received simple request for {Iterations} iterations", request.Iterations);

            var result = await _simpleProcessor.ProcessSimpleTaskAsync(request, HttpContext.RequestAborted, isToLog);

            if (result.Success)
            {
                return Ok(new
                {
                    Message = result.Message,
                    Result = result.Result,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    Error = result.Error,
                    Message = result.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Simple request was cancelled");
            return StatusCode(StatusCodes.Status499ClientClosedRequest, "Request was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing simple request");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                Error = "Internal server error",
                Details = ex.Message
            });
        }
    }

}