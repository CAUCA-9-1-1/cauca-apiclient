# ADR Records Overview

This index tracks Architecture Decision Records (ADRs) stored in this folder.

## How to Use

1. Add one markdown file per architectural decision under `docs/adr/records/`.
2. Follow the ADR template in `docs/adr/template.md`.
3. Use a clear, sortable naming convention: `YYYY-MM-DD-short-title.md`.
4. Update this overview with each new ADR, keeping its status column current.

## Records

| Title | Status | ADR Record |
|---|---|---|
| Fluent Request-Builder As The Primary Client Surface | accepted | [2026-07-07-fluent-request-builder-client-surface.md](2026-07-07-fluent-request-builder-client-surface.md) |
| Deprecate Legacy Verb-Helper Base Services In Favour Of The Fluent API | accepted | [2026-07-07-deprecate-legacy-verb-helper-base-services.md](2026-07-07-deprecate-legacy-verb-helper-base-services.md) |
| Drop Flurl For System.Net.Http In The Fluent Stack | accepted | [2026-07-07-drop-flurl-for-system-net-http.md](2026-07-07-drop-flurl-for-system-net-http.md) |
| Reactive Token Lifecycle For Secure Services | accepted | [2026-07-07-reactive-secure-service-token-lifecycle.md](2026-07-07-reactive-secure-service-token-lifecycle.md) |
| Self-Authenticating External-System Delegating Handler With Single-Flight Re-Auth | accepted | [2026-07-07-external-system-self-auth-handler.md](2026-07-07-external-system-self-auth-handler.md) |
| IHttpClientFactory-Based Typed Client Registration | accepted | [2026-07-07-httpclientfactory-typed-client-registration.md](2026-07-07-httpclientfactory-typed-client-registration.md) |
| Polly Transient-Failure Retry Policy | accepted | [2026-07-07-polly-transient-failure-retry-policy.md](2026-07-07-polly-transient-failure-retry-policy.md) |
| Centralized Status-Code To Typed-Exception Translation | accepted | [2026-07-07-centralized-status-code-exception-translation.md](2026-07-07-centralized-status-code-exception-translation.md) |
| Single-Package Release Governance For Cauca.ApiClient | accepted | [2026-07-07-single-package-release-governance.md](2026-07-07-single-package-release-governance.md) |
| Backward Compatibility Is The Default For The Public API And Target Frameworks | accepted | [2026-07-14-public-api-compatibility-policy.md](2026-07-14-public-api-compatibility-policy.md) |

## Review Protocol

Before proposing or implementing any major architectural change:

1. Review all relevant ADR records in this folder.
2. Reference the specific ADR files consulted.
3. Explain alignment with existing decisions or justify intentional deviations.
