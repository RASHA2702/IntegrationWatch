using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;
using IntegrationWatch.Infrastructure.Persistence;

namespace IntegrationWatch.Infrastructure.Persistence;

public static class WatchSeedData
{
    public static void SeedSampleEndpoints(InMemoryWatchStore store)
    {
        if (store.Endpoints.Any()) return;

        // Realistic integration targets
        var ep1 = MonitoredEndpoint.Create("Stripe Webhook Gateway", "https://api.stripe.com/healthcheck", HttpMethodType.GET, 200, 30, 4000, 3);
        var ep2 = MonitoredEndpoint.Create("Auth0 OIDC Discovery", "https://auth.enterprise.com/.well-known/openid-configuration", HttpMethodType.GET, 200, 60, 5000, 2);
        var ep3 = MonitoredEndpoint.Create("Core Banking Settlement Bus", "https://core-settlement.internal.net/health", HttpMethodType.GET, 200, 15, 2000, 3);

        // Pre-populate some historical executions
        ep1.RecordSuccess(48);
        ep2.RecordSuccess(112);
        ep3.RecordFailure("Connection refused on target port 443", 503, 2010);
        ep3.RecordFailure("Connection refused on target port 443", 503, 2005);
        ep3.RecordFailure("Connection refused on target port 443", 503, 2012); // Triggers Unhealthy

        store.Endpoints[ep1.Id] = ep1;
        store.Endpoints[ep2.Id] = ep2;
        store.Endpoints[ep3.Id] = ep3;

        // Generate synthetic latency distributions for ep1 & ep2
        var rnd = new Random(42);
        for (int i = 0; i < 40; i++)
        {
            var latency = rnd.Next(35, 65);
            store.Executions.Add(ProbeExecution.Create(ep1.Id, 200, latency, true, null, 0, Guid.NewGuid().ToString("N")));
        }
        for (int i = 0; i < 40; i++)
        {
            var latency = rnd.Next(95, 140);
            store.Executions.Add(ProbeExecution.Create(ep2.Id, 200, latency, true, null, 0, Guid.NewGuid().ToString("N")));
        }

        // Ep3 failures + active incident
        store.Executions.Add(ProbeExecution.Create(ep3.Id, 503, 2010, false, "Connection refused", 2, Guid.NewGuid().ToString("N")));
        store.Executions.Add(ProbeExecution.Create(ep3.Id, 503, 2005, false, "Connection refused", 2, Guid.NewGuid().ToString("N")));
        store.Executions.Add(ProbeExecution.Create(ep3.Id, 503, 2012, false, "Connection refused", 2, Guid.NewGuid().ToString("N")));

        var incident = Incident.Trigger(ep3.Id, ep3.Name, "Consecutive threshold exceeded: 3 failed probes. Target HTTP 503.");
        store.Incidents[incident.Id] = incident;
    }
}
