using IntegrationWatch.Application.DTOs;
using IntegrationWatch.Core.Entities;

namespace IntegrationWatch.Application.Services;

public interface IMetricsCalculator
{
    EndpointMetricsDto CalculateMetrics(MonitoredEndpoint endpoint, IReadOnlyList<ProbeExecution> executions);
}

public class MetricsCalculator : IMetricsCalculator
{
    public EndpointMetricsDto CalculateMetrics(MonitoredEndpoint endpoint, IReadOnlyList<ProbeExecution> executions)
    {
        if (executions == null || executions.Count == 0)
        {
            return new EndpointMetricsDto(
                endpoint.Id,
                endpoint.Name,
                UptimePercentage: 100.0,
                AvgLatencyMs: 0,
                P50LatencyMs: 0,
                P95LatencyMs: 0,
                P99LatencyMs: 0,
                TotalProbes: 0,
                FailedProbes: 0,
                RecentExecutions: Array.Empty<ProbeExecutionDto>()
            );
        }

        var total = executions.Count;
        var successes = executions.Count(e => e.IsSuccess);
        var failures = total - successes;
        var uptimePercentage = Math.Round(((double)successes / total) * 100.0, 2);

        var latencies = executions.Select(e => e.LatencyMs).OrderBy(l => l).ToList();
        var avgLatency = Math.Round(latencies.Average(), 1);

        var p50 = CalculatePercentile(latencies, 0.50);
        var p95 = CalculatePercentile(latencies, 0.95);
        var p99 = CalculatePercentile(latencies, 0.99);

        var dtos = executions.Take(30).Select(e => new ProbeExecutionDto(
            e.Id,
            e.EndpointId,
            e.ExecutedAtUtc,
            e.StatusCode,
            e.LatencyMs,
            e.IsSuccess,
            e.ErrorMessage,
            e.RetryAttempts,
            e.CorrelationId
        )).ToList();

        return new EndpointMetricsDto(
            endpoint.Id,
            endpoint.Name,
            uptimePercentage,
            avgLatency,
            p50,
            p95,
            p99,
            total,
            failures,
            dtos
        );
    }

    public static long CalculatePercentile(List<long> sortedLatencies, double percentile)
    {
        if (sortedLatencies.Count == 0) return 0;
        if (percentile <= 0) return sortedLatencies[0];
        if (percentile >= 1.0) return sortedLatencies[^1];

        var index = (int)Math.Ceiling(percentile * sortedLatencies.Count) - 1;
        index = Math.Clamp(index, 0, sortedLatencies.Count - 1);
        return sortedLatencies[index];
    }
}
