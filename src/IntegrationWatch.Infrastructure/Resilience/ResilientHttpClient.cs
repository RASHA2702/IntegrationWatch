using System.Diagnostics;
using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;
using IntegrationWatch.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace IntegrationWatch.Infrastructure.Resilience;

public interface IResilientHttpClient
{
    Task<ProbeExecution> ExecuteProbeAsync(MonitoredEndpoint endpoint, string correlationId, CancellationToken ct = default);
}

public class ResilientHttpClient : IResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IEndpointUrlValidator _urlValidator;
    private readonly ILogger<ResilientHttpClient> _logger;

    public ResilientHttpClient(
        HttpClient httpClient,
        IEndpointUrlValidator urlValidator,
        ILogger<ResilientHttpClient> logger)
    {
        _httpClient = httpClient;
        _urlValidator = urlValidator;
        _logger = logger;
    }

    public async Task<ProbeExecution> ExecuteProbeAsync(MonitoredEndpoint endpoint, string correlationId, CancellationToken ct = default)
    {
        // 1. SSRF Guard: Validate target URL against private/internal/cloud metadata addresses
        var validation = _urlValidator.ValidateUrl(endpoint.TargetUrl);
        if (!validation.IsValid)
        {
            _logger.LogWarning("[Correlation: {CorrelationId}] Blocked target URL for endpoint {EndpointName}: {Error}",
                correlationId, endpoint.Name, validation.ErrorMessage);

            return ProbeExecution.Create(
                endpoint.Id,
                statusCode: 400,
                latencyMs: 0,
                isSuccess: false,
                errorMessage: validation.ErrorMessage,
                retryAttempts: 0,
                correlationId: correlationId
            );
        }

        const int maxRetries = 2;
        int attempts = 0;
        long totalElapsedMs = 0;
        int? lastStatusCode = null;
        string? lastError = null;
        bool isSuccess = false;

        var sw = new Stopwatch();

        while (attempts <= maxRetries && !ct.IsCancellationRequested)
        {
            attempts++;
            sw.Restart();

            bool isTransientFailure = false;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromMilliseconds(endpoint.TimeoutMs));

                var request = new HttpRequestMessage(
                    endpoint.HttpMethod == HttpMethodType.HEAD ? HttpMethod.Head :
                    endpoint.HttpMethod == HttpMethodType.POST ? HttpMethod.Post : HttpMethod.Get,
                    endpoint.TargetUrl
                );
                request.Headers.Add("X-Correlation-ID", correlationId);
                request.Headers.Add("User-Agent", "IntegrationWatch-SyntheticProbe/1.0");

                var response = await _httpClient.SendAsync(request, cts.Token);
                sw.Stop();
                totalElapsedMs = sw.ElapsedMilliseconds;
                lastStatusCode = (int)response.StatusCode;

                if (lastStatusCode == endpoint.ExpectedStatusCode)
                {
                    isSuccess = true;
                    lastError = null;
                    break;
                }
                else
                {
                    lastError = $"HTTP status code {lastStatusCode} did not match expected {endpoint.ExpectedStatusCode}.";
                    isTransientFailure = IsTransientStatusCode(lastStatusCode.Value);
                }
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                totalElapsedMs = sw.ElapsedMilliseconds;
                lastError = $"Probe timed out after {endpoint.TimeoutMs} ms.";
                isTransientFailure = true; // Timeouts are transient
            }
            catch (Exception ex)
            {
                sw.Stop();
                totalElapsedMs = sw.ElapsedMilliseconds;
                lastError = $"Network exception: {ex.Message}";
                isTransientFailure = true; // Network errors are potentially transient
            }

            // Do not retry non-transient client errors (e.g., 400, 401, 403, 404, 405, 422)
            // or non-idempotent POST requests without explicit retry configuration
            if (!isTransientFailure || endpoint.HttpMethod == HttpMethodType.POST)
            {
                break;
            }

            if (attempts <= maxRetries)
            {
                // Exponential backoff with jitter
                var backoffMs = (int)(Math.Pow(2, attempts) * 100) + Random.Shared.Next(10, 50);
                await Task.Delay(backoffMs, ct);
            }
        }

        return ProbeExecution.Create(
            endpoint.Id,
            lastStatusCode,
            totalElapsedMs,
            isSuccess,
            lastError,
            attempts - 1,
            correlationId
        );
    }

    public static bool IsTransientStatusCode(int statusCode)
    {
        return statusCode switch
        {
            408 => true, // Request Timeout
            429 => true, // Too Many Requests
            500 => true, // Internal Server Error
            502 => true, // Bad Gateway
            503 => true, // Service Unavailable
            504 => true, // Gateway Timeout
            _ => false
        };
    }
}
