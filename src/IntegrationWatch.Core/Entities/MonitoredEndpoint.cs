using IntegrationWatch.Core.Enums;
using IntegrationWatch.Core.Exceptions;

namespace IntegrationWatch.Core.Entities;

public class MonitoredEndpoint
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string TargetUrl { get; private set; } = string.Empty;
    public HttpMethodType HttpMethod { get; private set; } = HttpMethodType.GET;
    public int ExpectedStatusCode { get; private set; } = 200;
    public int IntervalSeconds { get; private set; } = 60;
    public int TimeoutMs { get; private set; } = 5000;
    public int FailureThreshold { get; private set; } = 3;
    public int ConsecutiveFailures { get; private set; }
    public EndpointHealthStatus Status { get; private set; } = EndpointHealthStatus.Healthy;
    public DateTime? LastCheckedAtUtc { get; private set; }
    public long LastLatencyMs { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private MonitoredEndpoint() { }

    public static MonitoredEndpoint Create(
        string name,
        string targetUrl,
        HttpMethodType httpMethod = HttpMethodType.GET,
        int expectedStatusCode = 200,
        int intervalSeconds = 60,
        int timeoutMs = 5000,
        int failureThreshold = 3)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new EndpointValidationException(nameof(Name), "Endpoint name is required.");
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new EndpointValidationException(nameof(TargetUrl), "Target URL must be a valid HTTP or HTTPS absolute URI.");
        if (intervalSeconds < 5 || intervalSeconds > 3600)
            throw new EndpointValidationException(nameof(IntervalSeconds), "Interval must be between 5 seconds and 3600 seconds (1 hour).");
        if (timeoutMs < 500 || timeoutMs > 60000)
            throw new EndpointValidationException(nameof(TimeoutMs), "Timeout must be between 500ms and 60,000ms.");
        if (failureThreshold < 1 || failureThreshold > 10)
            throw new EndpointValidationException(nameof(FailureThreshold), "Failure threshold must be between 1 and 10.");

        return new MonitoredEndpoint
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            TargetUrl = targetUrl.Trim(),
            HttpMethod = httpMethod,
            ExpectedStatusCode = expectedStatusCode,
            IntervalSeconds = intervalSeconds,
            TimeoutMs = timeoutMs,
            FailureThreshold = failureThreshold,
            ConsecutiveFailures = 0,
            Status = EndpointHealthStatus.Healthy,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void RecordSuccess(long latencyMs)
    {
        ConsecutiveFailures = 0;
        Status = EndpointHealthStatus.Healthy;
        LastLatencyMs = latencyMs;
        LastCheckedAtUtc = DateTime.UtcNow;
    }

    public bool RecordFailure(string error, int? statusCode, long latencyMs)
    {
        ConsecutiveFailures++;
        LastLatencyMs = latencyMs;
        LastCheckedAtUtc = DateTime.UtcNow;

        if (ConsecutiveFailures >= FailureThreshold)
        {
            Status = EndpointHealthStatus.Unhealthy;
            return true; // Incident should be triggered
        }
        else
        {
            Status = EndpointHealthStatus.Degraded;
            return false;
        }
    }

    public void Pause()
    {
        Status = EndpointHealthStatus.Paused;
    }

    public void Resume()
    {
        Status = ConsecutiveFailures >= FailureThreshold ? EndpointHealthStatus.Unhealthy : EndpointHealthStatus.Healthy;
    }
}
