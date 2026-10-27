using IntegrationWatch.Application.DTOs;
using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Exceptions;
using IntegrationWatch.Core.Interfaces;

namespace IntegrationWatch.Application.Services;

public interface IEndpointService
{
    Task<IReadOnlyList<EndpointDto>> GetAllEndpointsAsync(CancellationToken ct = default);
    Task<EndpointDto> GetEndpointByIdAsync(Guid id, CancellationToken ct = default);
    Task<EndpointDto> CreateEndpointAsync(CreateEndpointRequest request, CancellationToken ct = default);
    Task DeleteEndpointAsync(Guid id, CancellationToken ct = default);
    Task<SystemHealthSummaryDto> GetSystemHealthSummaryAsync(CancellationToken ct = default);
}

public class EndpointService : IEndpointService
{
    private readonly IMonitoredEndpointRepository _endpointRepo;
    private readonly IProbeExecutionRepository _probeRepo;
    private readonly IIncidentRepository _incidentRepo;
    private readonly IMetricsCalculator _metricsCalculator;

    public EndpointService(
        IMonitoredEndpointRepository endpointRepo,
        IProbeExecutionRepository probeRepo,
        IIncidentRepository incidentRepo,
        IMetricsCalculator metricsCalculator)
    {
        _endpointRepo = endpointRepo;
        _probeRepo = probeRepo;
        _incidentRepo = incidentRepo;
        _metricsCalculator = metricsCalculator;
    }

    public async Task<IReadOnlyList<EndpointDto>> GetAllEndpointsAsync(CancellationToken ct = default)
    {
        var endpoints = await _endpointRepo.GetAllAsync(ct);
        return endpoints.Select(MapToDto).ToList();
    }

    public async Task<EndpointDto> GetEndpointByIdAsync(Guid id, CancellationToken ct = default)
    {
        var endpoint = await _endpointRepo.GetByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException(nameof(MonitoredEndpoint), id);

        return MapToDto(endpoint);
    }

    public async Task<EndpointDto> CreateEndpointAsync(CreateEndpointRequest request, CancellationToken ct = default)
    {
        var endpoint = MonitoredEndpoint.Create(
            request.Name,
            request.TargetUrl,
            request.HttpMethod,
            request.ExpectedStatusCode,
            request.IntervalSeconds,
            request.TimeoutMs,
            request.FailureThreshold
        );

        await _endpointRepo.AddAsync(endpoint, ct);
        return MapToDto(endpoint);
    }

    public async Task DeleteEndpointAsync(Guid id, CancellationToken ct = default)
    {
        var endpoint = await _endpointRepo.GetByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException(nameof(MonitoredEndpoint), id);

        await _endpointRepo.DeleteAsync(id, ct);
    }

    public async Task<SystemHealthSummaryDto> GetSystemHealthSummaryAsync(CancellationToken ct = default)
    {
        var endpoints = await _endpointRepo.GetAllAsync(ct);
        var openIncidents = await _incidentRepo.GetOpenIncidentsAsync(ct);

        var healthy = endpoints.Count(e => e.Status == Core.Enums.EndpointHealthStatus.Healthy);
        var degraded = endpoints.Count(e => e.Status == Core.Enums.EndpointHealthStatus.Degraded);
        var unhealthy = endpoints.Count(e => e.Status == Core.Enums.EndpointHealthStatus.Unhealthy);

        double totalUptimeSum = 0;
        foreach (var ep in endpoints)
        {
            var executions = await _probeRepo.GetRecentByEndpointIdAsync(ep.Id, 100, ct);
            var metrics = _metricsCalculator.CalculateMetrics(ep, executions);
            totalUptimeSum += metrics.UptimePercentage;
        }

        var avgUptime = endpoints.Count > 0 ? Math.Round(totalUptimeSum / endpoints.Count, 2) : 100.0;

        var activeIncidentDtos = openIncidents.Select(i => new IncidentDto(
            i.Id,
            i.EndpointId,
            i.EndpointName,
            i.Severity,
            i.Status,
            i.Title,
            i.Description,
            i.TriggeredAtUtc,
            i.AcknowledgedAtUtc,
            i.ResolvedAtUtc,
            i.ResolutionNotes
        )).ToList();

        return new SystemHealthSummaryDto(
            endpoints.Count,
            healthy,
            degraded,
            unhealthy,
            openIncidents.Count,
            avgUptime,
            activeIncidentDtos
        );
    }

    private static EndpointDto MapToDto(MonitoredEndpoint ep) => new(
        ep.Id,
        ep.Name,
        ep.TargetUrl,
        ep.HttpMethod,
        ep.ExpectedStatusCode,
        ep.IntervalSeconds,
        ep.TimeoutMs,
        ep.FailureThreshold,
        ep.ConsecutiveFailures,
        ep.Status,
        ep.LastCheckedAtUtc,
        ep.LastLatencyMs,
        ep.CreatedAtUtc
    );
}
