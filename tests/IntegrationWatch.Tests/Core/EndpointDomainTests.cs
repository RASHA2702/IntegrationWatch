using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;
using IntegrationWatch.Core.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationWatch.Tests.Core;

[TestClass]
public class EndpointDomainTests
{
    [TestMethod]
    public void Create_ValidParameters_InstantiatesCorrectly()
    {
        var ep = MonitoredEndpoint.Create("Stripe Webhooks", "https://api.stripe.com/health", HttpMethodType.GET, 200, 30, 3000, 3);

        Assert.AreNotEqual(Guid.Empty, ep.Id);
        Assert.AreEqual("Stripe Webhooks", ep.Name);
        Assert.AreEqual("https://api.stripe.com/health", ep.TargetUrl);
        Assert.AreEqual(EndpointHealthStatus.Healthy, ep.Status);
        Assert.AreEqual(0, ep.ConsecutiveFailures);
    }

    [TestMethod]
    [ExpectedException(typeof(EndpointValidationException))]
    public void Create_InvalidUrl_ThrowsEndpointValidationException()
    {
        MonitoredEndpoint.Create("Invalid URL", "not-a-valid-url");
    }

    [TestMethod]
    [ExpectedException(typeof(EndpointValidationException))]
    public void Create_IntervalTooLow_ThrowsEndpointValidationException()
    {
        MonitoredEndpoint.Create("Low Interval", "https://api.test.com", intervalSeconds: 1);
    }

    [TestMethod]
    public void RecordFailure_IncrementsConsecutiveFailuresAndTransitionsToUnhealthyWhenThresholdReached()
    {
        var ep = MonitoredEndpoint.Create("Target API", "https://api.example.com", failureThreshold: 3);

        // Attempt 1: Degraded
        var trigger1 = ep.RecordFailure("500 Internal Server Error", 500, 200);
        Assert.IsFalse(trigger1);
        Assert.AreEqual(1, ep.ConsecutiveFailures);
        Assert.AreEqual(EndpointHealthStatus.Degraded, ep.Status);

        // Attempt 2: Degraded
        var trigger2 = ep.RecordFailure("500 Internal Server Error", 500, 250);
        Assert.IsFalse(trigger2);
        Assert.AreEqual(2, ep.ConsecutiveFailures);
        Assert.AreEqual(EndpointHealthStatus.Degraded, ep.Status);

        // Attempt 3: Threshold reached -> Unhealthy & triggers incident
        var trigger3 = ep.RecordFailure("500 Internal Server Error", 500, 210);
        Assert.IsTrue(trigger3);
        Assert.AreEqual(3, ep.ConsecutiveFailures);
        Assert.AreEqual(EndpointHealthStatus.Unhealthy, ep.Status);

        // Subsequent recovery
        ep.RecordSuccess(45);
        Assert.AreEqual(0, ep.ConsecutiveFailures);
        Assert.AreEqual(EndpointHealthStatus.Healthy, ep.Status);
        Assert.AreEqual(45, ep.LastLatencyMs);
    }
}
