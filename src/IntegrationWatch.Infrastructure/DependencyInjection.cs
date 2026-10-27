using IntegrationWatch.Application.Services;
using IntegrationWatch.Core.Interfaces;
using IntegrationWatch.Infrastructure.Alerts;
using IntegrationWatch.Infrastructure.Background;
using IntegrationWatch.Infrastructure.Persistence;
using IntegrationWatch.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationWatch.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrationWatchInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryWatchStore>();

        services.AddScoped<IMonitoredEndpointRepository, MonitoredEndpointRepository>();
        services.AddScoped<IProbeExecutionRepository, ProbeExecutionRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IAlertDispatcher, AlertDispatcher>();

        services.AddHttpClient<IResilientHttpClient, ResilientHttpClient>();

        services.AddScoped<IMetricsCalculator, MetricsCalculator>();
        services.AddScoped<IEndpointService, EndpointService>();
        services.AddScoped<IIncidentService, IncidentService>();

        // Register background worker service
        services.AddHostedService<EndpointProbeWorker>();

        return services;
    }
}
