using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;

namespace IntegrationWatch.Core.Interfaces;

public interface IMonitoredEndpointRepository
{
    Task<IReadOnlyList<MonitoredEndpoint>> GetAllAsync(CancellationToken ct = default);
    Task<MonitoredEndpoint?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(MonitoredEndpoint endpoint, CancellationToken ct = default);
    Task UpdateAsync(MonitoredEndpoint endpoint, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IProbeExecutionRepository
{
    Task AddAsync(ProbeExecution execution, CancellationToken ct = default);
    Task<IReadOnlyList<ProbeExecution>> GetRecentByEndpointIdAsync(Guid endpointId, int count = 100, CancellationToken ct = default);
    Task<IReadOnlyList<ProbeExecution>> GetAllRecentAsync(int count = 100, CancellationToken ct = default);
}

public interface IIncidentRepository
{
    Task AddAsync(Incident incident, CancellationToken ct = default);
    Task UpdateAsync(Incident incident, CancellationToken ct = default);
    Task<Incident?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Incident>> GetOpenIncidentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Incident>> GetAllAsync(CancellationToken ct = default);
}

public interface IAlertDispatcher
{
    Task DispatchIncidentAlertAsync(Incident incident, CancellationToken ct = default);
    Task DispatchResolutionAlertAsync(Incident incident, CancellationToken ct = default);
}
