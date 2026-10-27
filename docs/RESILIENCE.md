# Resilience & Fault Mitigation Design

## 1. The Need for Probing Resilience
When synthetic monitoring services perform automated health probes against third-party endpoints (e.g. Stripe, Twilio, external ERPs), transient network blips (such as DNS re-resolutions, TCP handshake delays, or packet drops) can easily trigger false-positive downtime alerts.

IntegrationWatch solves this via a multi-tier resilience architecture:

### A. Failure Thresholds
Endpoints specify a `FailureThreshold` (default: 3). A single failed probe transitions the endpoint to `Degraded`, but will **not** raise an incident or dispatch an on-call alert. Only when consecutive failures meet or exceed the threshold does the system trigger an `Incident` and notify external channels.

### B. Exponential Backoff with Jitter
During each probe, if a failure occurs, the client does not spam the target API immediately. It performs retries spaced by an exponential delay plus random jitter:
$$\text{delay} = (2^{\text{attempt}} \times 100) + \text{Random}(10, 50)\text{ ms}$$
This prevents thundering-herd problems against struggling microservices.

### C. Linked CancellationToken Timeouts
Every probe creates a linked `CancellationTokenSource` that enforces a strict per-endpoint timeout (e.g., 5000ms), ensuring that slow or hung connections cannot exhaust the worker's thread pool.

### D. Automatic Incident Recovery
When a previously failing endpoint responds with the expected HTTP status code, the background worker automatically updates the open incident to `Resolved`, timestamps the resolution, and dispatches a recovery notification.
