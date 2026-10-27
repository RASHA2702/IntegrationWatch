using System.Diagnostics;
using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;
using Microsoft.Extensions.Logging;

namespace IntegrationWatch.Infrastructure.Resilience;

public interface IResilientHttpClient
{
    Task<ProbeExecution> ExecuteProbeAsync(MonitoredEndpoint endpoint, string correlationId, CancellationToken ct = default);
}

public class ResilientHttpClient : IResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResilientHttpClient> _logger;

    public ResilientHttpClient(HttpClient httpClient, ILogger<ResilientHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ProbeExecution> ExecuteProbeAsync(MonitoredEndpoint endpoint, string correlationId, CancellationToken ct = default)
    {
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
                }
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                totalElapsedMs = sw.ElapsedMilliseconds;
                lastError = $"Probe timed out after {endpoint.TimeoutMs} ms.";
            }
            catch (Exception ex)
            {
                sw.Stop();
                totalElapsedMs = sw.ElapsedMilliseconds;
                lastError = $"Network exception: {ex.Message}";
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
}
