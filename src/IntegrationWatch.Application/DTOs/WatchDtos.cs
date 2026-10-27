using IntegrationWatch.Core.Enums;

namespace IntegrationWatch.Application.DTOs;

public record EndpointDto(
    Guid Id,
    string Name,
    string TargetUrl,
    HttpMethodType HttpMethod,
    int ExpectedStatusCode,
    int IntervalSeconds,
    int TimeoutMs,
    int FailureThreshold,
    int ConsecutiveFailures,
    EndpointHealthStatus Status,
    DateTime? LastCheckedAtUtc,
    long LastLatencyMs,
    DateTime CreatedAtUtc
);

public record CreateEndpointRequest(
    string Name,
    string TargetUrl,
    HttpMethodType HttpMethod,
    int ExpectedStatusCode,
    int IntervalSeconds,
    int TimeoutMs,
    int FailureThreshold
);

public record UpdateEndpointRequest(
    string Name,
    string TargetUrl,
    HttpMethodType HttpMethod,
    int ExpectedStatusCode,
    int IntervalSeconds,
    int TimeoutMs,
    int FailureThreshold
);

public record ProbeExecutionDto(
    Guid Id,
    Guid EndpointId,
    DateTime ExecutedAtUtc,
    int? StatusCode,
    long LatencyMs,
    bool IsSuccess,
    string? ErrorMessage,
    int RetryAttempts,
    string CorrelationId
);

public record IncidentDto(
    Guid Id,
    Guid EndpointId,
    string EndpointName,
    IncidentSeverity Severity,
    IncidentStatus Status,
    string Title,
    string Description,
    DateTime TriggeredAtUtc,
    DateTime? AcknowledgedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionNotes
);

public record ResolveIncidentRequest(
    string ResolutionNotes
);

public record EndpointMetricsDto(
    Guid EndpointId,
    string EndpointName,
    double UptimePercentage,
    double AvgLatencyMs,
    long P50LatencyMs,
    long P95LatencyMs,
    long P99LatencyMs,
    int TotalProbes,
    int FailedProbes,
    IReadOnlyList<ProbeExecutionDto> RecentExecutions
);

public record SystemHealthSummaryDto(
    int TotalEndpoints,
    int HealthyCount,
    int DegradedCount,
    int UnhealthyCount,
    int OpenIncidentsCount,
    double SystemAverageUptimePercentage,
    IReadOnlyList<IncidentDto> ActiveIncidents
);
