using LongRunningService.Services.Implementations;
using LongRunningService.Services.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging.File;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ISimpleProcessor, SimpleProcessor>();

// Configuration
builder.Services.Configure<ProcessorAffinityConfig>(options =>
{
options.TotalProcessors = builder.Configuration.GetValue("ProcessorSettings:TotalProcessors", Environment.ProcessorCount);
options.DefaultAcquisitionTimeout = TimeSpan.FromSeconds(
    builder.Configuration.GetValue("ProcessorSettings:DefaultAcquisitionTimeoutSeconds", 30));
});

// Register services
builder.Services.AddSingleton<IProcessorAffinityService, ProcessorAffinityService>();
builder.Services.AddSingleton<ProcessingQueueService>();
builder.Services.AddSingleton<ILongRunningProcessor, LongRunningProcessorService>();

// Register hosted service
builder.Services.AddHostedService(provider => provider.GetRequiredService<ProcessingQueueService>());

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<ProcessorHealthCheck>("processor_health");

builder.Logging.AddFile(builder.Configuration.GetSection("Logging"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
app.UseSwagger();
app.UseSwaggerUI();
}

app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Health check implementation
public class ProcessorHealthCheck : IHealthCheck
{
    private readonly IProcessorAffinityService _affinityService;
    private readonly ProcessingQueueService _queueService;

    public ProcessorHealthCheck(IProcessorAffinityService affinityService, ProcessingQueueService queueService)
    {
        _affinityService = affinityService;
        _queueService = queueService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var availableProcessors = _affinityService.AvailableProcessors;
            var totalProcessors = _affinityService.TotalProcessors;
            var queueLength = _queueService.QueuedCount;

            var data = new Dictionary<string, object>
            {
                ["available_processors"] = availableProcessors,
                ["total_processors"] = totalProcessors,
                ["queued_requests"] = queueLength,
                ["utilization_percentage"] = _affinityService.UtilizationPercentage
            };

            if (availableProcessors == 0 && queueLength > 10)
            {
                return Task.FromResult(HealthCheckResult.Degraded("All processors busy and queue is growing", data: data));
            }

            if (availableProcessors > 0)
            {
                return Task.FromResult(HealthCheckResult.Healthy("Service is healthy", data));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Service is operational", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Health check failed", ex));
        }
    }
}

















//// Program.cs
//using Microsoft.Extensions.Logging;
//using Serilog.Extensions.Logging.File;

//var builder = WebApplication.CreateBuilder(args);

//// Add services
//builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//builder.Services.AddScoped<ISimpleProcessor, SimpleProcessor>();

////// Register our services
////builder.Services.AddSingleton<IProcessorAffinityService, ProcessorAffinityService>();
//////builder.Services.AddScoped<ILongRunningProcessor, LongRunningProcessor>();
////builder.Services.AddSingleton<ILongRunningProcessor, QueuedLongRunningProcessor>();
////builder.Services.AddScoped<ISimpleProcessor, SimpleProcessor>();





//builder.Logging.AddFile(builder.Configuration.GetSection("Logging"));

//var app = builder.Build();

//// Configure the HTTP request pipeline
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseRouting();
//app.UseHttpsRedirection();
//app.MapControllers();

//app.Run();











////var builder = WebApplication.CreateBuilder(args);

////// Add services to the container.

////builder.Services.AddControllers();
////// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
////builder.Services.AddEndpointsApiExplorer();
////builder.Services.AddSwaggerGen();

////var app = builder.Build();

////// Configure the HTTP request pipeline.
////if (app.Environment.IsDevelopment())
////{
////    app.UseSwagger();
////    app.UseSwaggerUI();
////}

////app.UseHttpsRedirection();

////app.UseAuthorization();

////app.MapControllers();

////app.Run();
