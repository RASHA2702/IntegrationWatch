using System.Text.Json.Serialization;
using IntegrationWatch.Api.Middleware;
using IntegrationWatch.Infrastructure;
using IntegrationWatch.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddIntegrationWatchInfrastructure();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Seed sample monitored targets
using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<InMemoryWatchStore>();
    WatchSeedData.SeedSampleEndpoints(store);
}

app.UseMiddleware<GlobalErrorMiddleware>();
app.UseRouting();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "IntegrationWatch Engine",
    timestamp = DateTime.UtcNow
}));

app.MapGet("/", () => Results.Ok(new
{
    service = "IntegrationWatch Synthetic Monitoring API",
    dashboard = "/api/health-summary",
    endpoints = "/api/endpoints",
    incidents = "/api/incidents/active"
}));

app.Run();
