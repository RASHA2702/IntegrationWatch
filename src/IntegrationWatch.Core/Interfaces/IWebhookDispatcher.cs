namespace IntegrationWatch.Core.Interfaces;

public record WebhookAlertPayload(
    string EventId,
    string EventType,
    DateTime TimestampUtc,
    Guid EndpointId,
    string EndpointName,
    int? StatusCode,
    long LatencyMs,
    string Message
);

public interface IWebhookDispatcher
{
    Task<bool> DispatchWebhookAsync(string webhookUrl, string secretKey, WebhookAlertPayload payload, CancellationToken ct = default);
}
