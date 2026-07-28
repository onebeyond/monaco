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
| `<!-- OBSERVABILITY: 1.4 HOST-PROFILES -->` | 1.4 |
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