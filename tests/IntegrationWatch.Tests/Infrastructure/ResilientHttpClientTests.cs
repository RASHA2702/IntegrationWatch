using System.Net;
using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;
using IntegrationWatch.Core.Interfaces;
using IntegrationWatch.Infrastructure.Resilience;
using IntegrationWatch.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace IntegrationWatch.Tests.Infrastructure;

[TestClass]
public class ResilientHttpClientTests
{
    [TestMethod]
    public async Task ExecuteProbeAsync_WithRestrictedSsrfUrl_RejectsImmediatelyWithoutHttpCall()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object);
        var validator = new EndpointUrlValidator();
        var client = new ResilientHttpClient(httpClient, validator, NullLogger<ResilientHttpClient>.Instance);

        var ep = MonitoredEndpoint.Create("AWS Metadata Probe", "http://169.254.169.254/latest/meta-data/");

        // Act
        var result = await client.ExecuteProbeAsync(ep, "ssrf-test-corr");

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(400, result.StatusCode);
        Assert.AreEqual(0, result.RetryAttempts);
        Assert.IsTrue(result.ErrorMessage!.Contains("SSRF prevention") || result.ErrorMessage.Contains("restricted"));

        // Verify no actual HTTP request was sent across the wire
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [TestMethod]
    public async Task ExecuteProbeAsync_OnNonTransient4xxError_DoesNotRetry()
    {
        // Arrange
        int sendCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                sendCount++;
                return new HttpResponseMessage(HttpStatusCode.NotFound); // 404 is non-transient
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var validatorMock = new Mock<IEndpointUrlValidator>();
        validatorMock.Setup(v => v.ValidateUrl(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(UrlValidationResult.Success());

        var client = new ResilientHttpClient(httpClient, validatorMock.Object, NullLogger<ResilientHttpClient>.Instance);
        var ep = MonitoredEndpoint.Create("Public API", "https://api.external.com/v1/missing", expectedStatusCode: 200);

        // Act
        var result = await client.ExecuteProbeAsync(ep, "non-transient-corr");

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(404, result.StatusCode);
        Assert.AreEqual(0, result.RetryAttempts); // Did not attempt retries on 404
        Assert.AreEqual(1, sendCount); // Sent exactly once
    }

    [TestMethod]
    public async Task ExecuteProbeAsync_OnTransient503Error_RetriesUpToMaxAttempts()
    {
        // Arrange
        int sendCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                sendCount++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); // 503 is transient
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var validatorMock = new Mock<IEndpointUrlValidator>();
        validatorMock.Setup(v => v.ValidateUrl(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(UrlValidationResult.Success());

        var client = new ResilientHttpClient(httpClient, validatorMock.Object, NullLogger<ResilientHttpClient>.Instance);
        var ep = MonitoredEndpoint.Create("Public API", "https://api.external.com/v1/flaky", expectedStatusCode: 200);

        // Act
        var result = await client.ExecuteProbeAsync(ep, "transient-corr");

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(503, result.StatusCode);
        Assert.AreEqual(2, result.RetryAttempts); // 2 retries (3 total attempts)
        Assert.AreEqual(3, sendCount);
    }
}
