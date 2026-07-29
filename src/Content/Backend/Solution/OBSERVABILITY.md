# Local observability

This is Monaco's authoritative generated guide for local observability. It is intentionally a scaffold: this generated shape does not yet claim any Signal, resource identity, configuration, persistence, messaging, identity, or Metric capability beyond the guide itself.

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

<!--#if (!commonLibraries) -->
## Static non-turnkey boundary

This output omits Monaco's shared observability package. The guide remains discoverable for this Runtime Host, but it is a static external-observability handoff only: it does not provide a substitute package, direct OpenTelemetry reference, profile registration, root `OTEL` configuration, or runnable local-observability verification.
<!--#endif -->

## Future-owned guide sections

The following deterministic insertion points reserve sequence and ownership for later stories. They are intentionally capability-neutral until their owning story supplies content.

| Placeholder | Owning story |
|---|---|
| Runtime host profiles below | 1.4 |
| `<!-- OBSERVABILITY: 1.5 CONFIGURATION -->` | 1.5 |
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

<!--#if (commonLibraries) -->
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
<!--#endif -->