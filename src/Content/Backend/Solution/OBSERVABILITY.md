# Local observability

This is the authoritative guide for local observability in this solution. Every generated Runtime Host receives the same shared observability composition whether Common libraries are delivered as source or compatible packages. No Signal, resource identity, configuration, persistence, messaging, identity, or Metric capability is claimed beyond what this guide documents.

## Prerequisites

The Aspire CLI is the only telemetry-specific prerequisite introduced by this guide. SQL Server, RabbitMQ, Blob/Azurite, identity/Keycloak, migrations, and any other selected-feature setup remain Monaco prerequisites; provision and verify them separately from observability verification.

With Monaco's normal application prerequisites in place, install the current stable Aspire CLI using the command for your shell:

macOS/Linux:

```bash
curl -sSL https://aspire.dev/install.sh | bash
```

Windows PowerShell:

```powershell
irm https://aspire.dev/install.ps1 | iex
```

If your `PATH` has not refreshed, open a new shell. Then verify the installed CLI:

```text
aspire --version
```

### Reproducible reference baseline

Use this optional procedure only when reproducing Monaco's reference baseline. Consumers are not hard-pinned: a later stable core becomes supported only after the compatibility smoke has passed and Monaco deliberately updates the tested version.

macOS/Linux:

```bash
curl -sSL https://aspire.dev/install.sh | bash -s -- --version "13.4.0"
```

Windows PowerShell:

```powershell
iex "& { $(irm https://aspire.dev/install.ps1) } -Version '13.4.0'"
```

Parse the `aspire --version` output as SemVer. The stable core `13.4.0` exactly is required; reject every prerelease component and accept optional `+buildMetadata`. Preserve the full, unmodified version output as evidence.

## Start the local viewer

Start the local viewer only with:

```text
aspire dashboard run --otlp-grpc-url http://localhost:4317
```

The OTLP/gRPC receiver remains loopback-only. Access the viewer in a browser only through the tokenized URL emitted by that command; do not guess or use a bare Dashboard URL. Treat the token as a secret: never retain, persist, log, upload, or copy it into evidence. Dashboard telemetry is in-memory and lost on restart.

## Common-library delivery

Every Runtime Host uses the same complete Common dependency contract. `--commonLibraries true` generates those projects as source; `--commonLibraries false` restores the declared package set from a compatible feed you own. The Boolean changes dependency delivery only: it does not change observability composition, configuration, host capability, this guide, or the manual post-action. No feed is configured for you, and no feed credentials, tokens, or secrets are retained.

The declared package set is versioned `0.0.1-alpha1` and its package IDs embed this solution's name (`<SolutionName>.Common.*`), so it cannot come pre-published. To stock your feed, generate a source-mode solution with the same `--name`, run `dotnet pack` on its `Common.*` projects, and publish the resulting packages to your feed. Package-mode output then restores from that feed; the generated `nuget.config` does not clear ambient sources, so an organization-level feed works without editing generated files.

## Future-owned guide sections

The following deterministic insertion points reserve sequence and ownership for later stories. They are intentionally capability-neutral until their owning story supplies content.

| Placeholder | Owning story |
|---|---|
| Runtime host profiles below | 1.4 |
| Configuration and routing below | 1.5 |
| Successful company creations below | 1.6 |
| Local first run below | 1.7 |
| Failure isolation below | 2.1 |
| High-fidelity telemetry boundary below | 2.6 |
| Production routing below | 2.4 |
| `<!-- OBSERVABILITY: 3.1-3.3 CAUSAL-DIAGNOSIS -->` | 3.1-3.3 |
| `<!-- OBSERVABILITY: 3.4 HOST-METRICS -->` | 3.4 |
| `<!-- OBSERVABILITY: 3.5 INSTRUMENTATION-GOVERNANCE -->` | 3.5 |
| `<!-- OBSERVABILITY: 4.5 PERSISTENCE-ATTRIBUTION -->` | 4.5 |
| `<!-- OBSERVABILITY: 5.1 MIGRATIONS -->` | 5.1 |
| `<!-- OBSERVABILITY: 5.3 REDUCED-API-VERIFICATION -->` | 5.3 |
| `<!-- OBSERVABILITY: 5.4 STATIC-SHAPE-BOUNDARIES -->` | 5.4 |
| `<!-- OBSERVABILITY: 5.5 CONSOLIDATION -->` | 5.5 |

<!--#if (apiService && workerService) -->
## Create a Company and inspect local observability

Use this walkthrough when your generated solution includes both an API and a Worker. It needs no application-code edits and no developer-defined local environment variables.

1. Provision only the prerequisites selected for this solution. Apply the generated `Init` migration using this solution's normal migration procedure, then start the standalone viewer as described above.
2. Start every participating Runtime Host with its checked-in Development configuration. Confirm the fallback `service.name` of each emitted host in the Dashboard:
   - `Monaco.Template.Backend.Api`
   - `Monaco.Template.Backend.Worker`
<!--#if (apiGateway) -->
   - `Monaco.Template.Backend.Common.ApiGateway`
<!--#endif -->

   Do not look for or infer an unselected host.
3. Authenticate in Scalar, then create a Company through the API. This is the only interactive business action in this task.
4. In the Dashboard's Logs view, find an Operational Log from a participating host and open its correlated Trace. In Traces, verify the same trace identifies that host's fallback `service.name`. In Metrics, find one applicable automatic Metric from the selected Runtime Hosts.
5. After the Company commit, find `company.successful_creations` in Metrics. It is an aggregate Counter with no attributes and must show exactly one increment for the committed Company. For a validation or persistence failure through an existing normal path, confirm no Company is committed and the Counter has no increment; do not add an endpoint, fixture, request, or Worker workload just to demonstrate this check.

The Dashboard only retains volatile in-memory history. A visible signal is viewer evidence, while the Company commit and the post-commit Counter semantics remain the application facts.
<!--#endif -->

<!--#if (workerService && !apiService && !apiGateway && !tests) -->
## Worker-only solutions

This Worker-only solution has no interactive API/Scalar Company walkthrough and no generated sample fixture or walkthrough task.
<!--#endif -->

## Configuration and routing

Configuration is resolved once during startup from the normal .NET provider order: `appsettings.json`, environment-specific JSON, Development User Secrets, environment variables, then command-line arguments. For the same key, the last provider wins; an empty winning value is missing and does not reveal a lower-priority value. Providers are not reloaded and Monaco does not rebuild telemetry after startup.

Use only the neutral `Observability` section for Monaco-owned controls:

| Key | Default | Accepted value | Malformed behavior |
|---|---|---|---|
| `Observability:Signals:{Traces,Metrics,Logs}:Enabled` | `true` | `true` or `false` (case-insensitive) | Startup fails |

Standard OpenTelemetry controls remain root keys, not `Monaco:*` aliases. `OTEL_SDK_DISABLED=true` dominates every Signal/exporter setting and creates no OpenTelemetry providers; Console and DI `ILogger` remain available. Other values and settings follow the selected OpenTelemetry SDK's behavior without Monaco validation or reinterpretation.

The common OTLP controls include `OTEL_EXPORTER_OTLP_{ENDPOINT,PROTOCOL,HEADERS,TIMEOUT,COMPRESSION}`. Signal-specific `OTEL_EXPORTER_OTLP_TRACES_*`, `..._METRICS_*`, and `..._LOGS_*` settings retain their normal SDK precedence. Development defaults use `http://localhost:4317` and `grpc`.

Resource, sampling, batching, retry, timeout, header, transport, logging, and other standard settings use the pinned SDK and instrumentation packages directly. Monaco does not publish an exhaustive supported-key list, impose fixed ranges, validate destination security, or certify a resulting telemetry stream for any consumer.

## Telemetry failure isolation

Operational Telemetry is best-effort diagnostic evidence. An unavailable or impaired OTLP receiver does not control Company commit, in-process business Counter recording, readiness, or host startup. Connection refusal, receiver timeout, authorization or throttling responses, malformed receiver responses, and saturation can lose telemetry, while Console logging remains available through the normal DI `ILogger` route.

Queueing, batching, retry, timeout, export, and shutdown behavior come from the selected OpenTelemetry SDK and its standard configuration. Monaco adds no preflight, retry or queue policy, exporter diagnostic throttle, hosted policy service, or custom flush coordinator. Graceful shutdown uses the ordinary SDK lifecycle, and abrupt termination can lose in-flight telemetry.

When the receiver recovers, normal SDK exporting resumes without restarting the Runtime Host. Telemetry receipt and exact recovery timing are not business correctness conditions.

## High-fidelity telemetry boundary

Monaco publishes applicable native instrumentation, ordinary structured log state and scopes, formatted messages, exception information, and application instruments without a Monaco content-governance layer. It does not filter, redact, sanitize, transform, sample, or bound telemetry through processors, SQL modes, identity modes, Metric Views, attribute policies, or cardinality ceilings. High-fidelity telemetry can therefore contain information that is sensitive in a consumer's environment.

A consumer-owned Collector or compatible downstream pipeline is the enforcement point for filtering, redaction, transformation, sampling, cardinality controls, routing, storage, access, retention, and alerting. Assess and configure that pipeline for its recipients and compliance requirements; Monaco does not certify consumer outcomes.

<!--#if (apiService || workerService) -->
API and Worker use the selected native SQL Client instrumentation without Monaco query-text rewriting or suppression. Monaco does not enable EF Core sensitive-data logging; consumers remain responsible for evaluating the telemetry their selected SDK and runtime configuration produce.
<!--#endif -->

<!--#if (apiGateway) -->
Gateway has no SQL instrumentation.
<!--#endif -->

<!--#if (apiService || apiGateway) -->
## HTTP exception boundaries

For an unexpected API or Gateway request failure, Monaco writes an ordinary structured error log with the original exception and returns HTTP 500. Span status, exception events, and exported exception fields follow normal ASP.NET Core and selected instrumentation behavior; Monaco does not reshape or suppress them.
<!--#endif -->

## Production signal routing

Every enabled Signal leaves each Runtime Host through the same single unified OTLP exporter path, and production routing is configuration-only: unchanged binaries send Traces, Metrics, and Logs to any OpenTelemetry Collector or compatible OTLP receiver supplied through the standard common and per-Signal OTLP keys above. No exporter selection, package change, or code change redirects Signals.

Per-Signal settings and common settings follow their selected-SDK precedence after .NET configuration providers resolve each key. For `http/protobuf`, the SDK expands a common base endpoint to `/v1/traces`, `/v1/metrics`, and `/v1/logs`; a signal-specific endpoint is used according to SDK behavior.

A single compatible receiver that accepts every enabled Signal is sufficient: one Collector hop may independently route, filter, transform, authenticate, or forward each Signal onward. When enabled Signals must reach divergent destinations, provide consumer-owned routing such as your own Collector pipeline or equivalent infrastructure. Monaco does not deploy or configure a production Collector, backend, dashboard, retention policy, alerting system, or vendor-specific topology; destination clarity and production receiver infrastructure remain consumer-owned.

Consumers own production transport security, receiver authentication, credentials, topology, and destination validation.

<!--#if (auth) -->
## Authenticated request identity

When authentication is enabled, the API and Gateway attach the first non-empty raw validated `sub` claim as `user.id`, using the native ASP.NET Core entry Activity and correlated request-scoped Operational Logs. For every validated claim, in enumeration order and including duplicates, they emit one `user.claim` event with raw `user.claim.type`, `user.claim.value`, `user.claim.value_type`, `user.claim.issuer`, and `user.claim.original_issuer`. The same ordered immutable sequence of raw claim records is carried as `user.claims` in the request log scope. Monaco enriches the native entry Activity rather than creating a wrapper span.

Enrichment reads only the post-authentication validated principal; it does not inspect raw tokens, headers, cookies, queries, bodies, authentication tickets, client credentials, or client certificates. An authenticated principal with no non-empty `sub` still contributes its raw claim events and `user.claims` scope without `user.id`; unauthenticated requests have no identity enrichment. The Worker profile does not perform request identity enrichment.

`user.id` and raw claims can contain linkable or otherwise sensitive data. Consumer infrastructure owns filtering, access, retention, deletion, transport, storage, and other governance controls. Telemetry enrichment does not alter authentication, authorization, Metric recording, or persistence attribution.
<!--#endif -->

<!--#if (apiService) -->
## Successful company creations

The API records one `company.successful_creations` measurement only after a Company unit of work commits successfully. Validation failures, persistence failures, rollbacks, retries before a successful commit, and unsuccessful requests record no measurement.

The Counter has no dimensions: it never includes Company, user, request, trace, span, message, identity, payload, or any other attribute. It is aggregate operational evidence, not a trace-owned event or an authoritative business ledger.

After a committed Company creation, open the Dashboard's Metrics view and locate `company.successful_creations`. The counter uses the same configuration-only Metrics routing as every other Metric: configure the supported OTLP Metric endpoint and protocol settings without source changes. Trace sampling and OTLP delivery do not alter the in-process post-commit recording or the committed business result.

<!--#endif -->
## Runtime host profiles and resource identity

Each generated Runtime Host emits Operational Logs, Traces, and Metrics through Monaco's shared OpenTelemetry composition and one unified OTLP exporter path. Ordinary application logging stays on the built-in DI `ILogger` route: Console logging remains enabled, and OpenTelemetry receives the same event without a second logger, custom correlation fields, or manual spans.

<!--#if (apiService) -->
### API profile

The API profile registers Runtime, ASP.NET Core, HttpClient, and SQL Client instrumentation.
<!--#endif -->

<!--#if (workerService) -->
### Worker profile

The Worker profile registers Runtime, HttpClient, and SQL Client instrumentation.
<!--#endif -->

<!--#if (apiGateway) -->
### Gateway profile

The Gateway profile registers Runtime, ASP.NET Core, and HttpClient instrumentation.
<!--#endif -->

### Resource metadata

Every enabled Signal in one process uses the same startup-immutable resource. Its generated `service.name` fallback is `<solution>.Api`, `<solution>.Worker`, or `<solution>.Common.ApiGateway` for the applicable host, and `service.namespace` falls back to `<solution>`. Precedence is exact: generated fallbacks, then `OTEL_RESOURCE_ATTRIBUTES` collisions, then `OTEL_SERVICE_NAME` for final `service.name` precedence.

Resource environment overrides use the selected OpenTelemetry SDK detector without Monaco validation or content filtering. Consumer deployment is responsible for the attributes it supplies and for downstream governance.
