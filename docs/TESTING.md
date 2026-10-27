# Automated Testing Report & Methodology

## 1. Test Suite Summary
The test suite in `tests/IntegrationWatch.Tests` verifies the resilience engine, mathematical metric calculations, and the background probe worker.

### Verified Test Cases:
1. `EndpointDomainTests.Create_ValidParameters_InstantiatesCorrectly`: Confirms default property initializations.
2. `EndpointDomainTests.Create_InvalidUrl_ThrowsEndpointValidationException`: Enforces valid HTTP/HTTPS URI schemes.
3. `EndpointDomainTests.Create_IntervalTooLow_ThrowsEndpointValidationException`: Protects targets from abusive probe intervals (<5s).
4. `EndpointDomainTests.RecordFailure_IncrementsConsecutiveFailuresAndTransitionsToUnhealthyWhenThresholdReached`: Validates multi-step status transitions (`Healthy` → `Degraded` → `Unhealthy`).
5. `MetricsCalculatorTests.CalculateMetrics_CalculatesPercentilesAndUptimeAccurately`: Verifies P50, P95, and P99 calculation accuracy and uptime percentage math.
6. `MetricsCalculatorTests.CalculatePercentile_EdgeCases_ReturnsSafely`: Confirms safety on empty and single-element collections.
7. `ProbeWorkerTests.ProbeEndpointAsync_WhenThresholdBreached_TriggersIncidentAndAlert`: Uses Moq to verify that threshold breaches instantiate an `Incident` and trigger `IAlertDispatcher`.
8. `ProbeWorkerTests.ProbeEndpointAsync_WhenEndpointRecovers_ResolvesOpenIncidentAndDispatchesResolution`: Verifies that recovering endpoints auto-resolve incidents.

---

## 2. Actual Verification Results
```text
Test run for IntegrationWatch.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 453 ms - IntegrationWatch.Tests.dll (net8.0)
```
- **Total Tests:** 8
- **Passed:** 8 (100%)
- **Failed:** 0
- **Duration:** 453 ms
