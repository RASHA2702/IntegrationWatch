# IntegrationWatch — API & Webhook Resilience Monitoring Platform

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](#)
[![Tests](https://img.shields.io/badge/tests-27%20passed-success)](#)
[![Framework](https://img.shields.io/badge/.NET-8.0%20LTS-purple)](#)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A high-performance synthetic monitoring and integration resilience service designed for modern cloud architectures. Engineered with **C# / .NET 8**, **resilience patterns (transient retries, jittered backoff)**, **SSRF protection**, **Background Services (`IHostedService`)**, and **RFC 9457 ProblemDetails**.

---

## 1. Business Problem
Modern distributed SaaS applications depend heavily on external webhooks and third-party APIs (Stripe, SendGrid, Twilio, ERPs). When external APIs degrade or fail silently:
- Customers report checkout errors before engineering teams notice.
- Transient network glitches cause false-alarm alerts and on-call burnout.
- Engineering leads lack visibility into true latency distributions (P95 and P99 percentiles).
- Vulnerable probing engines expose internal cloud environments to Server-Side Request Forgery (SSRF) and metadata credential theft.

**IntegrationWatch** continuously runs synthetic probes, mitigates transient blips via exponential backoff retries on transient codes, protects internal infrastructure from SSRF, calculates statistical latency percentiles using the NIST Nearest-Rank method, and manages incident state transitions with automated resolution upon service recovery.

---

## 2. Intended Users
- **Backend & Platform Engineers:** Needing synthetic telemetry across critical external microservices and payment gateways.
- **Site Reliability Engineers (SREs):** Monitoring SLA/SLO uptime and P95/P99 latency trends.
- **DevOps & Engineering Teams:** Managing external integrations, webhooks, and third-party dependencies.

---

## 3. Major Capabilities

- **Synthetic Probe Scheduler:** Background worker running non-blocking scheduled HTTP probes with controlled concurrency (`Parallel.ForEachAsync`).
- **Resilience Engine:** Exponential backoff with jitter and linked cancellation token timeouts, selectively retrying only transient errors (408, 429, 500, 502, 503, 504) while failing fast on non-transient 4xx errors.
- **SSRF Defense:** Automated validation blocking loopback (`127.0.0.1`, `localhost`), link-local / cloud metadata (`169.254.169.254`), and RFC 1918 private subnets (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`).
- **Consecutive Failure Thresholds:** 3-stage health progression (`Healthy` → `Degraded` → `Unhealthy`).
- **Statistical Latency Telemetry:** Real-time computation of P50 (median), P95, and P99 response time percentiles using NIST Nearest-Rank algorithm.
- **Incident Lifecycle Management:** Automatically triggers incidents upon threshold breach and auto-resolves when the endpoint recovers.
- **Distributed Correlation Tracking:** Injects `X-Correlation-ID` across every probe request and error response.
- **RFC 9457 ProblemDetails:** Uniform JSON error formats with sanitized internal exceptions (CWE-209 defense).

---

## 4. Architecture Overview

```mermaid
flowchart TD
    subgraph Engine ["Background Worker (IHostedService)"]
        Worker["EndpointProbeWorker"]
        ParallelPool["Parallel.ForEachAsync (Degree: 5)"]
    end

    subgraph Security ["Security & Validation"]
        Validator["EndpointUrlValidator (SSRF Guard)"]
    end

    subgraph Res ["Resilience Pipeline"]
        Client["ResilientHttpClient"]
        Retry["Exponential Backoff + Jitter (Transient Only)"]
        Timeout["Linked CancellationToken Timeout"]
    end

    subgraph Core ["Domain Aggregates"]
        Endpoints["MonitoredEndpoint"]
        Incidents["Incident Lifecycle"]
        Executions["ProbeExecution Stream"]
    end

    subgraph Metrics ["Analytics & Alerting"]
        Calculator["MetricsCalculator (Nearest-Rank P50, P95, P99)"]
        Dispatcher["AlertDispatcher (Webhooks / Logs)"]
    end

    subgraph API ["REST API Layer"]
        EndpointsCtrl["EndpointsController"]
        HealthCtrl["SystemHealthController"]
        IncidentsCtrl["IncidentsController"]
        Middleware["GlobalErrorMiddleware (RFC 9457)"]
    end

    Worker --> ParallelPool
    ParallelPool --> Validator
    Validator --> Client
    Client --> Retry
    Client --> Timeout
    Client --> Executions
    ParallelPool --> Incidents
    Incidents --> Dispatcher
    API --> Middleware
    Middleware --> EndpointsCtrl
```

---

## 5. Technology Stack
- **Runtime:** .NET 8 LTS (C# 12)
- **Web API:** ASP.NET Core Web API with Controllers & Minimal APIs
- **Background Processing:** `BackgroundService` (`IHostedService`) with `Parallel.ForEachAsync` and `IServiceScopeFactory` scoped resolution
- **HTTP Client:** `IHttpClientFactory` with high-precision Stopwatch timing and SSRF validation
- **Testing:** MSTest 3.1, Moq 4.18
- **Containerization:** Docker multi-stage build & Docker Compose
- **Standards:** RFC 9457 (ProblemDetails), ISO 8601 (UTC Timestamps)

---

## 6. Prerequisites
- .NET 8 SDK or newer (`dotnet --version`)
- Docker (optional)

---

## 7. Quickstart & Local Execution

### Step 1: Run Automated Tests
```powershell
dotnet test tests/IntegrationWatch.Tests/IntegrationWatch.Tests.csproj
```
*(All 27 unit, resilience, and security tests pass in ~1 second).*

### Step 2: Run the API & Background Worker
```powershell
cd src/IntegrationWatch.Api
dotnet run --launch-profile "http"
```
The service will start listening on `http://localhost:5050`.

### Step 3: Inspect Telemetry
- System Summary: `http://localhost:5050/api/health-summary`
- Monitored Endpoints: `http://localhost:5050/api/endpoints`
- Active Incidents: `http://localhost:5050/api/incidents/active`

---

## 8. Automated Testing Results
```text
Test run for IntegrationWatch.Tests.dll (.NETCoreApp,Version=v8.0)
Passed!  - Failed: 0, Passed: 27, Skipped: 0, Total: 27, Duration: 1 s
```
- Verified failure threshold transitions (`Healthy` → `Degraded` → `Unhealthy`).
- Verified percentile mathematics using NIST Nearest-Rank (P50, P95, P99) and boundary conditions.
- Verified automatic incident creation and alert dispatch on threshold breach.
- Verified automatic incident recovery when endpoints heal.
- Verified SSRF validation blocking private subnets, loopbacks, and cloud metadata (169.254.169.254).
- Verified resilient client fast-failure on non-transient 4xx errors without redundant retries.
- Verified transient error retries (503, 429, timeouts) up to max attempts with exponential backoff.

---

## 9. Security Considerations
- **SSRF Defense:** Strict URL validation checks scheme (HTTP/HTTPS only) and blocks loopback, private IPv4/IPv6 subnets, and cloud instance metadata services.
- **Information Disclosure Prevention (CWE-209):** Global error middleware sanitizes internal exception messages on 500 status codes, returning generic problem titles and traceable correlation IDs.
- **Idempotency Protection:** Prevents blind retries of non-idempotent HTTP methods (`POST`).

---

## 10. License & Attribution
Designed and built by **Rashad** ([@RASHA2702](https://github.com/RASHA2702)). Licensed under the **MIT License**.
