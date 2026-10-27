namespace IntegrationWatch.Core.Enums;

public enum EndpointHealthStatus
{
    Healthy = 1,
    Degraded = 2,
    Unhealthy = 3,
    Paused = 4
}

public enum HttpMethodType
{
    GET = 1,
    POST = 2,
    HEAD = 3
}

public enum IncidentSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum IncidentStatus
{
    Triggered = 1,
    Investigating = 2,
    Identified = 3,
    Resolved = 4
}
