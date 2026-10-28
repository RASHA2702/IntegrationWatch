namespace IntegrationWatch.Application.Services;

public record AnomalyDetectionResult(
    bool IsAnomaly,
    string Severity, // "Normal", "Warning", "Critical"
    double BaselineMeanMs,
    double StandardDeviationMs,
    long CurrentLatencyMs,
    double ZScore,
    string Description
);

public interface ILatencyAnomalyDetector
{
    AnomalyDetectionResult DetectAnomaly(IReadOnlyList<long> historicalLatencies, long currentLatencyMs);
}

public class LatencyAnomalyDetector : ILatencyAnomalyDetector
{
    public AnomalyDetectionResult DetectAnomaly(IReadOnlyList<long> historicalLatencies, long currentLatencyMs)
    {
        if (historicalLatencies == null || historicalLatencies.Count < 5)
        {
            return new AnomalyDetectionResult(
                IsAnomaly: false,
                Severity: "Normal",
                BaselineMeanMs: currentLatencyMs,
                StandardDeviationMs: 0,
                CurrentLatencyMs: currentLatencyMs,
                ZScore: 0,
                Description: "Insufficient historical baseline samples (< 5 probes)."
            );
        }

        // Calculate sample mean
        double mean = historicalLatencies.Average();

        // Calculate sample variance & standard deviation
        double sumSquares = historicalLatencies.Sum(l => Math.Pow(l - mean, 2));
        double variance = sumSquares / (historicalLatencies.Count - 1);
        double stdDev = Math.Sqrt(variance);

        // Z-score calculation (how many std deviations from mean)
        double divisor = stdDev > 1.0 ? stdDev : 1.0;
        double zScore = Math.Round((currentLatencyMs - mean) / divisor, 2);

        // Anomaly categorization
        if (zScore >= 3.0 && currentLatencyMs > 100)
        {
            return new AnomalyDetectionResult(
                IsAnomaly: true,
                Severity: "Critical",
                BaselineMeanMs: Math.Round(mean, 1),
                StandardDeviationMs: Math.Round(stdDev, 1),
                CurrentLatencyMs: currentLatencyMs,
                ZScore: zScore,
                Description: $"Critical latency degradation detected: {currentLatencyMs}ms is {zScore} standard deviations above baseline ({mean:F1}ms)."
            );
        }

        if (zScore >= 2.0 && currentLatencyMs > 60)
        {
            return new AnomalyDetectionResult(
                IsAnomaly: true,
                Severity: "Warning",
                BaselineMeanMs: Math.Round(mean, 1),
                StandardDeviationMs: Math.Round(stdDev, 1),
                CurrentLatencyMs: currentLatencyMs,
                ZScore: zScore,
                Description: $"Elevated latency detected: {currentLatencyMs}ms is {zScore} standard deviations above baseline ({mean:F1}ms)."
            );
        }

        return new AnomalyDetectionResult(
            IsAnomaly: false,
            Severity: "Normal",
            BaselineMeanMs: Math.Round(mean, 1),
            StandardDeviationMs: Math.Round(stdDev, 1),
            CurrentLatencyMs: currentLatencyMs,
            ZScore: zScore,
            Description: "Latency performance is within normal statistical thresholds."
        );
    }
}
