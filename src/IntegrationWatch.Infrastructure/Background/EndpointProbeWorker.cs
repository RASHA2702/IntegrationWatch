using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;
using IntegrationWatch.Core.Interfaces;
using IntegrationWatch.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrationWatch.Infrastructure.Background;

public class EndpointProbeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EndpointProbeWorker> _logger;

    public EndpointProbeWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EndpointProbeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IntegrationWatch Synthetic Probe Worker initialized.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var endpointRepo = scope.ServiceProvider.GetRequiredService<IMonitoredEndpointRepository>();

                var endpoints = await endpointRepo.GetAllAsync(stoppingToken);
                var active = endpoints.Where(e => e.Status != EndpointHealthStatus.Paused).ToList();

                foreach (var ep in active)
                {
                    if (!ep.LastCheckedAtUtc.HasValue || (DateTime.UtcNow - ep.LastCheckedAtUtc.Value).TotalSeconds >= ep.IntervalSeconds)
                    {
                        await ProbeEndpointAsync(ep, scope.ServiceProvider, stoppingToken);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unexpected error in background probe iteration.");
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    public async Task<ProbeExecution> ProbeEndpointAsync(MonitoredEndpoint ep, IServiceProvider sp, CancellationToken ct = default)
    {
        var probeRepo = sp.GetRequiredService<IProbeExecutionRepository>();
        var endpointRepo = sp.GetRequiredService<IMonitoredEndpointRepository>();
        var incidentRepo = sp.GetRequiredService<IIncidentRepository>();
        var alertDispatcher = sp.GetRequiredService<IAlertDispatcher>();
        var resilientHttpClient = sp.GetRequiredService<IResilientHttpClient>();

        var correlationId = Guid.NewGuid().ToString("N");
        var execution = await resilientHttpClient.ExecuteProbeAsync(ep, correlationId, ct);
        await probeRepo.AddAsync(execution, ct);

        if (execution.IsSuccess)
        {
            var previousStatus = ep.Status;
            ep.RecordSuccess(execution.LatencyMs);
            await endpointRepo.UpdateAsync(ep, ct);

            if (previousStatus == EndpointHealthStatus.Unhealthy || previousStatus == EndpointHealthStatus.Degraded)
            {
                var openIncidents = await incidentRepo.GetOpenIncidentsAsync(ct);
                var relatedIncident = openIncidents.FirstOrDefault(i => i.EndpointId == ep.Id);
                if (relatedIncident != null)
                {
                    relatedIncident.Resolve($"Endpoint recovered automatically. HTTP probe succeeded with latency {execution.LatencyMs}ms.");
                    await incidentRepo.UpdateAsync(relatedIncident, ct);
                    await alertDispatcher.DispatchResolutionAlertAsync(relatedIncident, ct);
                }
            }
        }
        else
        {
            var triggerIncident = ep.RecordFailure(execution.ErrorMessage ?? "Unknown failure", execution.StatusCode, execution.LatencyMs);
            await endpointRepo.UpdateAsync(ep, ct);

            if (triggerIncident)
            {
                var openIncidents = await incidentRepo.GetOpenIncidentsAsync(ct);
                if (!openIncidents.Any(i => i.EndpointId == ep.Id))
                {
                    var incident = Incident.Trigger(ep.Id, ep.Name, execution.ErrorMessage ?? "Target failed to respond with expected HTTP status.");
                    await incidentRepo.AddAsync(incident, ct);
                    await alertDispatcher.DispatchIncidentAlertAsync(incident, ct);
                }
            }
        }

        return execution;
    }
}
