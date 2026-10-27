using IntegrationWatch.Application.DTOs;
using IntegrationWatch.Application.Services;
using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationWatch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EndpointsController : ControllerBase
{
    private readonly IEndpointService _endpointService;
    private readonly IProbeExecutionRepository _probeRepo;
    private readonly IMonitoredEndpointRepository _endpointRepo;
    private readonly IMetricsCalculator _metricsCalculator;

    public EndpointsController(
        IEndpointService endpointService,
        IProbeExecutionRepository probeRepo,
        IMonitoredEndpointRepository endpointRepo,
        IMetricsCalculator metricsCalculator)
    {
        _endpointService = endpointService;
        _probeRepo = probeRepo;
        _endpointRepo = endpointRepo;
        _metricsCalculator = metricsCalculator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EndpointDto>>> GetAll(CancellationToken ct)
    {
        var endpoints = await _endpointService.GetAllEndpointsAsync(ct);
        return Ok(endpoints);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EndpointDto>> GetById(Guid id, CancellationToken ct)
    {
        var endpoint = await _endpointService.GetEndpointByIdAsync(id, ct);
        return Ok(endpoint);
    }

    [HttpPost]
    public async Task<ActionResult<EndpointDto>> Create([FromBody] CreateEndpointRequest request, CancellationToken ct)
    {
        var created = await _endpointService.CreateEndpointAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}/metrics")]
    public async Task<ActionResult<EndpointMetricsDto>> GetMetrics(Guid id, CancellationToken ct)
    {
        var endpoint = await _endpointRepo.GetByIdAsync(id, ct);
        if (endpoint == null) return NotFound();

        var executions = await _probeRepo.GetRecentByEndpointIdAsync(id, 100, ct);
        var metrics = _metricsCalculator.CalculateMetrics(endpoint, executions);
        return Ok(metrics);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _endpointService.DeleteEndpointAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public IncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<IncidentDto>>> GetActive(CancellationToken ct)
    {
        var incidents = await _incidentService.GetActiveIncidentsAsync(ct);
        return Ok(incidents);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncidentDto>>> GetAll(CancellationToken ct)
    {
        var incidents = await _incidentService.GetAllIncidentsAsync(ct);
        return Ok(incidents);
    }

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<ActionResult<IncidentDto>> Acknowledge(Guid id, CancellationToken ct)
    {
        var incident = await _incidentService.AcknowledgeIncidentAsync(id, ct);
        return Ok(incident);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<IncidentDto>> Resolve(Guid id, [FromBody] ResolveIncidentRequest request, CancellationToken ct)
    {
        var incident = await _incidentService.ResolveIncidentAsync(id, request, ct);
        return Ok(incident);
    }
}

[ApiController]
[Route("api/health-summary")]
public class SystemHealthController : ControllerBase
{
    private readonly IEndpointService _endpointService;

    public SystemHealthController(IEndpointService endpointService)
    {
        _endpointService = endpointService;
    }

    [HttpGet]
    public async Task<ActionResult<SystemHealthSummaryDto>> GetSummary(CancellationToken ct)
    {
        var summary = await _endpointService.GetSystemHealthSummaryAsync(ct);
        return Ok(summary);
    }
}
