using System.Net;
using IntegrationWatch.Core.Interfaces;
using IntegrationWatch.Infrastructure.Alerts;
using IntegrationWatch.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace IntegrationWatch.Tests.Infrastructure;

[TestClass]
public class WebhookDispatcherTests
{
    [TestMethod]
    public async Task DispatchWebhookAsync_RestrictedSsrfDestination_BlocksImmediately()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object);
        var validator = new EndpointUrlValidator();
        var dispatcher = new WebhookDispatcher(httpClient, validator, NullLogger<WebhookDispatcher>.Instance);

        var payload = new WebhookAlertPayload(
            EventId: "evt_1",
            EventType: "incident.triggered",
            TimestampUtc: DateTime.UtcNow,
            EndpointId: Guid.NewGuid(),
            EndpointName: "Stripe",
            StatusCode: 500,
            LatencyMs: 350,
            Message: "500 Error"
        );

        // Target cloud metadata SSRF
        bool success = await dispatcher.DispatchWebhookAsync("http://169.254.169.254/webhook", "secret", payload);

        Assert.IsFalse(success);
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [TestMethod]
    public async Task DispatchWebhookAsync_ValidDestination_SignsWithHmacHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(handlerMock.Object);
        var validatorMock = new Mock<IEndpointUrlValidator>();
        validatorMock.Setup(v => v.ValidateUrl(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(UrlValidationResult.Success());

        var dispatcher = new WebhookDispatcher(httpClient, validatorMock.Object, NullLogger<WebhookDispatcher>.Instance);

        var payload = new WebhookAlertPayload(
            EventId: "evt_2",
            EventType: "incident.resolved",
            TimestampUtc: DateTime.UtcNow,
            EndpointId: Guid.NewGuid(),
            EndpointName: "Stripe",
            StatusCode: 200,
            LatencyMs: 45,
            Message: "Endpoint recovered"
        );

        bool success = await dispatcher.DispatchWebhookAsync("https://hooks.slack.com/services/test", "webhook-secret-key", payload);

        Assert.IsTrue(success);
        Assert.IsNotNull(capturedRequest);
        Assert.IsTrue(capturedRequest.Headers.Contains("X-Signature-SHA256"));
        Assert.IsTrue(capturedRequest.Headers.Contains("X-Event-Type"));

        var sigHeader = capturedRequest.Headers.GetValues("X-Signature-SHA256").First();
        Assert.IsTrue(sigHeader.StartsWith("sha256="));
    }
}
