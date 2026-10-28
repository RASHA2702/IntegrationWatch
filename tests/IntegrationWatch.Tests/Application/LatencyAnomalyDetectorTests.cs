using IntegrationWatch.Application.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntegrationWatch.Tests.Application;

[TestClass]
public class LatencyAnomalyDetectorTests
{
    private readonly LatencyAnomalyDetector _detector = new();

    [TestMethod]
    public void DetectAnomaly_NormalDistribution_ReturnsNormal()
    {
        // Baseline: around 45-55ms
        var history = new List<long> { 48, 52, 50, 49, 51, 47, 53, 50, 52, 48 };
        var result = _detector.DetectAnomaly(history, 51);

        Assert.IsFalse(result.IsAnomaly);
        Assert.AreEqual("Normal", result.Severity);
        Assert.IsTrue(result.ZScore < 2.0);
    }

    [TestMethod]
    public void DetectAnomaly_ExtremeSpike_ReturnsCriticalAnomaly()
    {
        // Baseline: around 50ms with tight variance
        var history = new List<long> { 50, 51, 49, 50, 52, 48, 50, 51, 49, 50 };
        // Sudden spike to 550ms
        var result = _detector.DetectAnomaly(history, 550);

        Assert.IsTrue(result.IsAnomaly);
        Assert.AreEqual("Critical", result.Severity);
        Assert.IsTrue(result.ZScore > 3.0);
        Assert.IsTrue(result.Description.Contains("Critical latency degradation"));
    }

    [TestMethod]
    public void DetectAnomaly_InsufficientSamples_ReturnsNormalWithInfo()
    {
        var smallHistory = new List<long> { 50, 60 };
        var result = _detector.DetectAnomaly(smallHistory, 200);

        Assert.IsFalse(result.IsAnomaly);
        Assert.IsTrue(result.Description.Contains("Insufficient historical baseline"));
    }
}
