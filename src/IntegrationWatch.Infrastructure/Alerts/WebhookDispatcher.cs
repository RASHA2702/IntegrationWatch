using System.Text;
using System.Text.Json;
using IntegrationWatch.Core.Interfaces;
using IntegrationWatch.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace IntegrationWatch.Infrastructure.Alerts;

public class WebhookDispatcher : IWebhookDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly IEndpointUrlValidator _urlValidator;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        HttpClient httpClient,
        IEndpointUrlValidator urlValidator,
        ILogger<WebhookDispatcher> logger)
    {
        _httpClient = httpClient;
        _urlValidator = urlValidator;
        _logger = logger;
    }

    public async Task<bool> DispatchWebhookAsync(string webhookUrl, string secretKey, WebhookAlertPayload payload, CancellationToken ct = default)
    {
        // 1. SSRF Guard on Webhook destination URL
        var validation = _urlValidator.ValidateUrl(webhookUrl);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Webhook dispatch blocked by SSRF filter for destination '{Url}': {Error}",
                webhookUrl, validation.ErrorMessage);
            return false;
        }

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var signatureHex = HmacSecurityHelper.ComputeHmacSha256(json, secretKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Signature-SHA256", $"sha256={signatureHex}");
        request.Headers.Add("X-Timestamp", payload.TimestampUtc.ToString("O"));
        request.Headers.Add("X-Event-Type", payload.EventType);

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Webhook alert '{EventId}' dispatched successfully to {Url} (Status: {StatusCode})",
                    payload.EventId, webhookUrl, (int)response.StatusCode);
                return true;
            }

            _logger.LogWarning("Webhook destination {Url} responded with non-success status code {StatusCode}",
                webhookUrl, (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch webhook alert '{EventId}' to {Url}",
                payload.EventId, webhookUrl);
            return false;
        }
    }
}
