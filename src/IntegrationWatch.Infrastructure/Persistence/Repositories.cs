using System.Collections.Concurrent;
using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;
using IntegrationWatch.Core.Interfaces;

namespace IntegrationWatch.Infrastructure.Persistence;

public class InMemoryWatchStore
{
    public ConcurrentDictionary<Guid, MonitoredEndpoint> Endpoints { get; } = new();
    public ConcurrentDictionary<Guid, Incident> Incidents { get; } = new();
    public ConcurrentBag<ProbeExecution> Executions { get; } = new();
}

public class MonitoredEndpointRepository : IMonitoredEndpointRepository
{
    private readonly InMemoryWatchStore _store;

    public MonitoredEndpointRepository(InMemoryWatchStore store) => _store = store;

    public Task<IReadOnlyList<MonitoredEndpoint>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<MonitoredEndpoint> list = _store.Endpoints.Values.OrderBy(e => e.Name).ToList();
        return Task.FromResult(list);
    }

    public Task<MonitoredEndpoint?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _store.Endpoints.TryGetValue(id, out var ep);
        return Task.FromResult(ep);
    }

    public Task AddAsync(MonitoredEndpoint endpoint, CancellationToken ct = default)
    {
        _store.Endpoints[endpoint.Id] = endpoint;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MonitoredEndpoint endpoint, CancellationToken ct = default)
    {
        _store.Endpoints[endpoint.Id] = endpoint;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _store.Endpoints.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public class ProbeExecutionRepository : IProbeExecutionRepository
{
    private readonly InMemoryWatchStore _store;

    public ProbeExecutionRepository(InMemoryWatchStore store) => _store = store;

    public Task AddAsync(ProbeExecution execution, CancellationToken ct = default)
    {
        _store.Executions.Add(execution);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProbeExecution>> GetRecentByEndpointIdAsync(Guid endpointId, int count = 100, CancellationToken ct = default)
    {
        IReadOnlyList<ProbeExecution> list = _store.Executions
            .Where(e => e.EndpointId == endpointId)
            .OrderByDescending(e => e.ExecutedAtUtc)
            .Take(count)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<ProbeExecution>> GetAllRecentAsync(int count = 100, CancellationToken ct = default)
    {
        IReadOnlyList<ProbeExecution> list = _store.Executions
            .OrderByDescending(e => e.ExecutedAtUtc)
            .Take(count)
            .ToList();
        return Task.FromResult(list);
    }
}

public class IncidentRepository : IIncidentRepository
{
    private readonly InMemoryWatchStore _store;

    public IncidentRepository(InMemoryWatchStore store) => _store = store;

    public Task AddAsync(Incident incident, CancellationToken ct = default)
    {
        _store.Incidents[incident.Id] = incident;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Incident incident, CancellationToken ct = default)
    {
        _store.Incidents[incident.Id] = incident;
        return Task.CompletedTask;
    }

    public Task<Incident?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _store.Incidents.TryGetValue(id, out var inc);
        return Task.FromResult(inc);
    }

    public Task<IReadOnlyList<Incident>> GetOpenIncidentsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Incident> list = _store.Incidents.Values
            .Where(i => i.Status != IncidentStatus.Resolved)
            .OrderByDescending(i => i.TriggeredAtUtc)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<Incident>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Incident> list = _store.Incidents.Values
            .OrderByDescending(i => i.TriggeredAtUtc)
            .ToList();
        return Task.FromResult(list);
    }
}
