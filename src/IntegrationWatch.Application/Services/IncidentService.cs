using IntegrationWatch.Application.DTOs;
using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Exceptions;
using IntegrationWatch.Core.Interfaces;

namespace IntegrationWatch.Application.Services;

public interface IIncidentService
{
    Task<IReadOnlyList<IncidentDto>> GetActiveIncidentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IncidentDto>> GetAllIncidentsAsync(CancellationToken ct = default);
    Task<IncidentDto> AcknowledgeIncidentAsync(Guid id, CancellationToken ct = default);
    Task<IncidentDto> ResolveIncidentAsync(Guid id, ResolveIncidentRequest request, CancellationToken ct = default);
}

public class IncidentService : IIncidentService
{
    private readonly IIncidentRepository _incidentRepo;
    private readonly IAlertDispatcher _alertDispatcher;

    public IncidentService(IIncidentRepository incidentRepo, IAlertDispatcher alertDispatcher)
    {
        _incidentRepo = incidentRepo;
        _alertDispatcher = alertDispatcher;
    }

    public async Task<IReadOnlyList<IncidentDto>> GetActiveIncidentsAsync(CancellationToken ct = default)
    {
        var incidents = await _incidentRepo.GetOpenIncidentsAsync(ct);
        return incidents.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<IncidentDto>> GetAllIncidentsAsync(CancellationToken ct = default)
    {
        var incidents = await _incidentRepo.GetAllAsync(ct);
        return incidents.Select(MapToDto).ToList();
    }

    public async Task<IncidentDto> AcknowledgeIncidentAsync(Guid id, CancellationToken ct = default)
    {
        var incident = await _incidentRepo.GetByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException(nameof(Incident), id);

        incident.Acknowledge();
        await _incidentRepo.UpdateAsync(incident, ct);
        return MapToDto(incident);
    }

    public async Task<IncidentDto> ResolveIncidentAsync(Guid id, ResolveIncidentRequest request, CancellationToken ct = default)
    {
        var incident = await _incidentRepo.GetByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException(nameof(Incident), id);

        incident.Resolve(request.ResolutionNotes);
        await _incidentRepo.UpdateAsync(incident, ct);
        await _alertDispatcher.DispatchResolutionAlertAsync(incident, ct);

        return MapToDto(incident);
    }

    private static IncidentDto MapToDto(Incident i) => new(
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
    );
}
