using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;

namespace IntegrationWatch.Application.Services;

public record SlaMetricDto(
    Guid EndpointId,
    string EndpointName,
    double AvailabilityPercent,
    double TargetSloPercent,
    bool SloMet,
    double MeanTimeToRecoveryMinutes,
    int TotalIncidentsCount,
    long TotalDowntimeSeconds
);

public interface ISlaCalculator
{
    SlaMetricDto CalculateSla(MonitoredEndpoint endpoint, IReadOnlyList<Incident> incidents, TimeSpan evaluationWindow, double targetSloPercent = 99.9);
}

public class SlaCalculator : ISlaCalculator
{
    public SlaMetricDto CalculateSla(MonitoredEndpoint endpoint, IReadOnlyList<Incident> incidents, TimeSpan evaluationWindow, double targetSloPercent = 99.9)
    {
        var windowSeconds = (long)evaluationWindow.TotalSeconds;
        if (windowSeconds <= 0) windowSeconds = 86400; // Default 24 hours

        var relevantIncidents = incidents.Where(i => i.EndpointId == endpoint.Id).ToList();

        long totalDowntimeSeconds = 0;
        var recoveryDurationsMinutes = new List<double>();

        foreach (var inc in relevantIncidents)
        {
            DateTime endTime = inc.Status == IncidentStatus.Resolved && inc.ResolvedAtUtc.HasValue
                ? inc.ResolvedAtUtc.Value
                : DateTime.UtcNow;

            var duration = endTime - inc.TriggeredAtUtc;
            if (duration.TotalSeconds > 0)
            {
                totalDowntimeSeconds += (long)duration.TotalSeconds;
                recoveryDurationsMinutes.Add(duration.TotalMinutes);
            }
        }

        // Bounded downtime
        if (totalDowntimeSeconds > windowSeconds)
        {
            totalDowntimeSeconds = windowSeconds;
        }

        long uptimeSeconds = Math.Max(0, windowSeconds - totalDowntimeSeconds);
        double availabilityPercent = Math.Round(((double)uptimeSeconds / windowSeconds) * 100.0, 3);
        bool sloMet = availabilityPercent >= targetSloPercent;

        double mttrMinutes = recoveryDurationsMinutes.Count > 0
            ? Math.Round(recoveryDurationsMinutes.Average(), 1)
            : 0.0;

        return new SlaMetricDto(
            EndpointId: endpoint.Id,
            EndpointName: endpoint.Name,
            AvailabilityPercent: availabilityPercent,
            TargetSloPercent: targetSloPercent,
            SloMet: sloMet,
            MeanTimeToRecoveryMinutes: mttrMinutes,
            TotalIncidentsCount: relevantIncidents.Count,
            TotalDowntimeSeconds: totalDowntimeSeconds
        );
    }
}
