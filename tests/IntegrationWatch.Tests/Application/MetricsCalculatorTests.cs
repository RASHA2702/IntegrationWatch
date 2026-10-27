using IntegrationWatch.Application.Services;
using IntegrationWatch.Core.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationWatch.Tests.Application;

[TestClass]
public class MetricsCalculatorTests
{
    [TestMethod]
    public void CalculateMetrics_CalculatesPercentilesAndUptimeAccurately()
    {
        var calculator = new MetricsCalculator();
        var ep = MonitoredEndpoint.Create("Latency Test", "https://api.test.com");

        var executions = new List<ProbeExecution>();
        // 90 successes at 50ms, 10 failures at 1000ms
        for (int i = 0; i < 90; i++)
        {
            executions.Add(ProbeExecution.Create(ep.Id, 200, 50, true, null, 0, "corr1"));
        }
        for (int i = 0; i < 10; i++)
        {
            executions.Add(ProbeExecution.Create(ep.Id, 504, 1000, false, "Timeout", 2, "corr2"));
        }

        var metrics = calculator.CalculateMetrics(ep, executions);

        Assert.AreEqual(90.0, metrics.UptimePercentage);
        Assert.AreEqual(100, metrics.TotalProbes);
        Assert.AreEqual(10, metrics.FailedProbes);
        Assert.AreEqual(50, metrics.P50LatencyMs);
        Assert.AreEqual(1000, metrics.P95LatencyMs);
        Assert.AreEqual(1000, metrics.P99LatencyMs);
    }

    [TestMethod]
    public void CalculatePercentile_EdgeCases_ReturnsSafely()
    {
        var empty = new List<long>();
        Assert.AreEqual(0, MetricsCalculator.CalculatePercentile(empty, 0.5));

        var single = new List<long> { 120 };
        Assert.AreEqual(120, MetricsCalculator.CalculatePercentile(single, 0.95));

        var multiple = new List<long> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        Assert.AreEqual(50, MetricsCalculator.CalculatePercentile(multiple, 0.5));
        Assert.AreEqual(100, MetricsCalculator.CalculatePercentile(multiple, 0.95));
    }
}
