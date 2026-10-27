using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace IntegrationWatch.Infrastructure.Alerts;

public class AlertDispatcher : IAlertDispatcher
{
    private readonly ILogger<AlertDispatcher> _logger;

    public AlertDispatcher(ILogger<AlertDispatcher> logger)
    {
        _logger = logger;
    }

    public Task DispatchIncidentAlertAsync(Incident incident, CancellationToken ct = default)
    {
        _logger.LogCritical("🚨 [ALERT DISPATCHED] Endpoint '{Endpoint}' is DOWN! Incident ID: {Id}. Details: {Description}",
            incident.EndpointName, incident.Id, incident.Description);
        return Task.CompletedTask;
    }

    public Task DispatchResolutionAlertAsync(Incident incident, CancellationToken ct = default)
    {
        _logger.LogInformation("✅ [RECOVERY DISPATCHED] Endpoint '{Endpoint}' RECOVERED. Incident ID: {Id}. Notes: {Notes}",
            incident.EndpointName, incident.Id, incident.ResolutionNotes);
        return Task.CompletedTask;
    }
}
