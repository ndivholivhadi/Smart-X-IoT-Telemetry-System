using SmartX.API.Data;
using SmartX.API.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 
builder.Services.AddSingleton<SmartXDataStore>();
// processInline: false, so telemetry waits in the queues until TelemetryIngestionWorker consumes it.
builder.Services.AddSingleton(sp => new TelemetryService(
    sp.GetRequiredService<SmartXDataStore>(),
    sp.GetRequiredService<AnomalyDetectionService>(),
    processInline: false));
builder.Services.AddSingleton<TelemetrySimulator>();
builder.Services.AddSingleton(sp => new AttachmentEncryptionService(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<SmartXDataStore>().DataDirectory));
builder.Services.AddSingleton<AttachmentService>();
builder.Services.AddSingleton<AnomalyDetectionService>();
builder.Services.AddSingleton<SensorMonitoringService>();
builder.Services.AddSingleton<TelemetryBatchService>();
builder.Services.AddSingleton<DeploymentValidationService>();
builder.Services.AddSingleton<TelemetrySearchService>();
builder.Services.AddSingleton<TelemetrySimulationService>();
builder.Services.AddSingleton<CommandService>();
builder.Services.AddSingleton<RecommendationService>();
builder.Services.AddSingleton<MeshTopologyService>();

builder.Services.AddHostedService<TelemetrySimulationWorker>();
builder.Services.AddHostedService<SensorMonitoringWorker>();
builder.Services.AddHostedService<TelemetryIngestionWorker>();
builder.Services.AddHostedService<CommandProcessingWorker>();

var app = builder.Build();

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();


app.MapControllers();

await app.RunAsync();