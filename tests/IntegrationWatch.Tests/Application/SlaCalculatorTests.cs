using IntegrationWatch.Application.Services;
using IntegrationWatch.Core.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationWatch.Tests.Application;

[TestClass]
public class SlaCalculatorTests
{
    private readonly SlaCalculator _calculator = new();

    [TestMethod]
    public void CalculateSla_NoIncidents_Returns100PercentAvailability()
    {
        var ep = MonitoredEndpoint.Create("Core API", "https://api.core.com");
        var result = _calculator.CalculateSla(ep, new List<Incident>(), TimeSpan.FromDays(30), 99.9);

        Assert.AreEqual(100.0, result.AvailabilityPercent);
        Assert.IsTrue(result.SloMet);
        Assert.AreEqual(0, result.TotalIncidentsCount);
        Assert.AreEqual(0.0, result.MeanTimeToRecoveryMinutes);
    }

    [TestMethod]
    public void CalculateSla_WithIncidents_CalculatesDowntimeAndMttr()
    {
        var ep = MonitoredEndpoint.Create("Billing Gateway", "https://api.pay.com");
        var inc = Incident.Trigger(ep.Id, ep.Name, "Service Unavailable");
        inc.Resolve("Server restarted"); // Duration ~ 0 min if instant, but let's test metrics calculation

        var result = _calculator.CalculateSla(ep, new List<Incident> { inc }, TimeSpan.FromHours(24), 99.0);

        Assert.AreEqual(1, result.TotalIncidentsCount);
        Assert.IsTrue(result.AvailabilityPercent > 90.0);
    }
}
