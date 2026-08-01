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
| `<!-- OBSERVABILITY: 1.6 SUCCESSFUL-COMPANIES -->` | 1.6 |
| `<!-- OBSERVABILITY: 1.7 LOCAL-FIRST-RUN -->` | 1.7 |
| `<!-- OBSERVABILITY: 2.1 FAILURE-ISOLATION -->` | 2.1 |
| `<!-- OBSERVABILITY: 2.2-2.3 DATA-BOUNDARIES -->` | 2.2-2.3 |
| `<!-- OBSERVABILITY: 2.4 PRODUCTION-ROUTING -->` | 2.4 |
| `<!-- OBSERVABILITY: 2.5 IDENTITY -->` | 2.5 |
| `<!-- OBSERVABILITY: 3.1-3.3 CAUSAL-DIAGNOSIS -->` | 3.1-3.3 |
| `<!-- OBSERVABILITY: 3.4 HOST-METRICS -->` | 3.4 |
| `<!-- OBSERVABILITY: 3.5 INSTRUMENTATION-GOVERNANCE -->` | 3.5 |
| `<!-- OBSERVABILITY: 4.5 PERSISTENCE-ATTRIBUTION -->` | 4.5 |
| `<!-- OBSERVABILITY: 5.1 MIGRATIONS -->` | 5.1 |
| `<!-- OBSERVABILITY: 5.3 REDUCED-API-VERIFICATION -->` | 5.3 |
| `<!-- OBSERVABILITY: 5.4 STATIC-SHAPE-BOUNDARIES -->` | 5.4 |
| `<!-- OBSERVABILITY: 5.5 CONSOLIDATION -->` | 5.5 |

## Configuration and routing

Configuration is resolved once during startup from the normal .NET provider order: `appsettings.json`, environment-specific JSON, Development User Secrets, environment variables, then command-line arguments. For the same key, the last provider wins; an empty winning value is missing and does not reveal a lower-priority value. Providers are not reloaded and Monaco does not rebuild telemetry after startup.

Use only the neutral `Observability` section for Monaco-owned controls:

| Key | Default | Accepted value | Malformed behavior |
|---|---|---|---|
| `Observability:Signals:{Traces,Metrics,Logs}:Enabled` | `true` | `true` or `false` (case-insensitive) | Startup fails |
| `Observability:SqlClient:QueryTextMode` | `SanitizedText` | `SanitizedText` or `SummaryOnly` | Safely resolves to `SummaryOnly` |
| `Observability:Identity:Mode` | `Subject` | `Subject`, `HmacSha256`, or `Disabled` | Safely resolves to `Disabled` |
| `Observability:Shutdown:FlushTimeout` | `00:00:05` | constant-format `TimeSpan`, greater than zero and at most five seconds | Startup fails |

Standard OpenTelemetry controls remain root keys, not `Monaco:*` aliases. `OTEL_SDK_DISABLED=true` dominates every Signal/exporter setting and creates no OpenTelemetry providers; Console and DI `ILogger` remain available. Any other non-empty value does not disable the SDK and emits a safe startup diagnostic.

The common OTLP controls are `OTEL_EXPORTER_OTLP_{ENDPOINT,PROTOCOL,HEADERS,TIMEOUT,COMPRESSION}`. Each Signal may override its common value through `OTEL_EXPORTER_OTLP_TRACES_*`, `..._METRICS_*`, or `..._LOGS_*`; signal-specific values win after each key has resolved normal .NET-provider precedence. Development defaults use `http://localhost:4317` and `grpc`.

Supported operational root keys are `OTEL_SERVICE_NAME`, `OTEL_RESOURCE_ATTRIBUTES`, `OTEL_TRACES_SAMPLER`, `OTEL_TRACES_SAMPLER_ARG`, `OTEL_BSP_*`, `OTEL_BLRP_*`, `OTEL_METRIC_EXPORT_{INTERVAL,TIMEOUT}`, and applicable `Logging:LogLevel` categories. Logs and Traces default to queue `2048`, batch `512`, delay/timeout `5000` ms; Metrics default to interval `60000` ms and timeout `5000` ms. Explicit values are validated at startup, including `batch <= queue`.

Do not configure exporter selection, propagators, auto-instrumentation, YAML, retry/disk buffering, certificates/client keys, temporality/exemplars, attribute limits, or provider-specific logging switches. Use environment variables or Development User Secrets for any future secret-capable setting; never commit headers, credentials, authorization values, certificates, or HMAC material. The complete spelling, range, malformed-value, and characterization contract is `CONFIGURATION-MATRIX.md` in Monaco's source repository; it is not copied into generated output.

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

Use only low-cardinality, non-secret operational metadata. Standard `service.version`, `service.instance.id`, `service.namespace`, and `deployment.environment.name` attributes are supported. Never provide credentials, secrets, personal data, tenant or customer identifiers, request or payload data, or user identity. Passing resource validation is not a privacy certification.