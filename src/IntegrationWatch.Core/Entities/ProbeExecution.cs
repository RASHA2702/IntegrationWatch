namespace IntegrationWatch.Core.Entities;

public class ProbeExecution
{
    public Guid Id { get; private set; }
    public Guid EndpointId { get; private set; }
    public DateTime ExecutedAtUtc { get; private set; }
    public int? StatusCode { get; private set; }
    public long LatencyMs { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryAttempts { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;

    private ProbeExecution() { }

    public static ProbeExecution Create(
        Guid endpointId,
        int? statusCode,
        long latencyMs,
        bool isSuccess,
        string? errorMessage,
        int retryAttempts,
        string correlationId)
    {
        return new ProbeExecution
        {
            Id = Guid.NewGuid(),
            EndpointId = endpointId,
            ExecutedAtUtc = DateTime.UtcNow,
            StatusCode = statusCode,
            LatencyMs = latencyMs,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            RetryAttempts = retryAttempts,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId
        };
    }
}
