# Polly Transient-Failure Retry Policy

* Status: accepted
* Date: 2026-07-07
* Deciders: Cauca.ApiClient maintainers
* Technical Story: Repository architecture baseline

## Context and Problem Statement

Calls to remote APIs occasionally fail for transient reasons — gateway timeouts, temporarily unavailable services, or no response at all. Retrying these blindly is unsafe (business `4xx` errors must not be retried), so the library needs a curated, configurable retry policy applied uniformly to every request without leaking into call sites.

## Decision Drivers

* Automatic recovery from transient upstream failures.
* Never retry non-transient responses (client/business `4xx`).
* Configurable attempt count and backoff, and injectable for testing.

## Considered Options

* **Option A**: A Polly `WaitAndRetryAsync` policy (`FluentRetryPolicyBuilder`) that retries only a curated set of transient status codes plus no-response, with exponential backoff, injected through `IRetryPolicyBuilder`.
* **Option B**: No retry at all, or a retry-everything policy that also re-sends genuine client errors.

## Decision Outcome

Chosen option: **Option A**, because retrying only a known transient set with exponential backoff recovers from real transient faults while leaving business errors to surface immediately, and injection through `IRetryPolicyBuilder` keeps the policy testable and overridable.

### Consequences

* Good: Transient failures recover automatically; each send is wrapped by the policy.
* Good: Backoff is exponential (`2^retryAttempt` seconds); attempt count is overridable per service.
* Bad: On a genuine outage, retries add latency before the failure surfaces.
* Bad: Retry logging uses `Console.WriteLine`, which is coarse and not structured.

## Maintenance Invariants
<!-- Behaviors to preserve; this decision is implemented -->
- Retry only when `IsTransientOrTimeOut` holds: status `408`, `502`, `503`, `504`, or a no-response `ApiHttpException`.
- Keep exponential backoff `2^retryAttempt`; `MaxRetryAttemptOnTransientFailure` defaults to 3 and stays overridable by derived services.
- Keep the policy injectable via `IRetryPolicyBuilder` (`FluentRetryPolicyBuilder` is the default) so tests can substitute it.
- The retry policy wraps every send inside `FluentBaseService`; do not retry inside individual verb terminals.

## Amendment 2026-11-26: No retry once a status was received

* Status: accepted
* Date: 2026-11-26
* Deciders: Cauca.ApiClient maintainers
* Technical Story: https://github.com/CAUCA-9-1-1/cauca-apiclient/issues/12

### Context

The request timeout now covers reading the response body. Retrying after a failure that happens once a status was received would resend a request the server may already have processed, duplicating non-idempotent calls such as a POST.

### Decision

`IsTransientOrTimeOut` does not retry an `ApiHttpException` flagged as `IsResponseBodyNotReceived`. Only failures before a status was received (and the curated transient status codes) are retried.

### Consequences

* Good: A body-read failure never duplicates a request.
* Bad: A transient fault while reading a body surfaces immediately instead of recovering.

