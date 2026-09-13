using AwesomeAssertions;

namespace Monaco.Template.Backend.Common.Observability.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Application Services", "Observability")]
public sealed class ObservabilityGuideTokenTests
{
	private static readonly string IfDirective = "#" + "if";
	private static readonly string EndIfDirective = "#" + "endif";

	[Fact(DisplayName = "Generated guide documents the high-fidelity producer and ordinary HTTP exception boundary")]
	public void GeneratedGuideDocumentsHighFidelityProducerAndOrdinaryHttpExceptionBoundary()
	{
		var guide = ReadGuide();

		Assert.DoesNotContain("OBSERVABILITY: 2.6 HIGH-FIDELITY-BOUNDARY", guide, StringComparison.Ordinal);
		Assert.Contains("## High-fidelity telemetry boundary", guide, StringComparison.Ordinal);
		Assert.Contains("does not filter, redact, sanitize, transform, sample, or bound", guide, StringComparison.Ordinal);
		Assert.Contains("consumer-owned Collector", guide, StringComparison.Ordinal);
		Assert.Contains("filtering, redaction, sanitization, identity treatment, transformation, sampling, cardinality controls, routing, transport, storage, access, retention, and alerting", guide, StringComparison.Ordinal);
		Assert.Contains("does not certify consumer outcomes", guide, StringComparison.Ordinal);
		Assert.Contains("compliance requirements", guide, StringComparison.Ordinal);
		Assert.Contains("Gateway has no SQL instrumentation", guide, StringComparison.Ordinal);
		guide.Should().Contain("<!--" + IfDirective + " (apiService || apiGateway) -->\n## HTTP exception boundaries");
		guide.Should().Contain("ordinary structured error log");
		guide.Should().Contain("normal ASP.NET Core and selected instrumentation behavior");
	}

	[Fact(DisplayName = "Generated local observability guide documents Story 2.4 production routing and preserves later placeholders")]
	public void GeneratedLocalObservabilityGuideDocumentsStory24ProductionRoutingAndPreservesLaterPlaceholders()
	{
		var guide = ReadGuide();

		Assert.Contains("| Production routing below | 2.4 |", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("OBSERVABILITY: 2.4 PRODUCTION-ROUTING", guide, StringComparison.Ordinal);

		var sectionStart = guide.IndexOf("## Production signal routing", StringComparison.Ordinal);
		Assert.True(sectionStart >= 0, "Expected the production-routing section in the guide.");
		var nextSection = guide.IndexOf("\n## ", sectionStart + 1, StringComparison.Ordinal);
		var section = guide[sectionStart..(nextSection >= 0 ? nextSection : guide.Length)];

		Assert.Contains("configuration-only", section, StringComparison.Ordinal);
		Assert.Contains("unchanged binaries", section, StringComparison.Ordinal);
		Assert.Contains("OpenTelemetry Collector or compatible OTLP receiver", section, StringComparison.Ordinal);
		Assert.Contains("selected-SDK precedence", section, StringComparison.Ordinal);
		Assert.Contains("/v1/traces", section, StringComparison.Ordinal);
		Assert.Contains("route, filter, transform, authenticate, or forward", section, StringComparison.Ordinal);
		Assert.Contains("divergent destinations", section, StringComparison.Ordinal);
		Assert.Contains("does not deploy or configure a production Collector", section, StringComparison.Ordinal);
		Assert.Contains("Consumers own production transport security", section, StringComparison.Ordinal);
		Assert.DoesNotContain("localhost", section, StringComparison.Ordinal);
		Assert.DoesNotContain("User Secrets", section, StringComparison.Ordinal);

		Assert.Contains("## Authenticated request identity", guide, StringComparison.Ordinal);
		Assert.Contains("OBSERVABILITY: 3.1-3.3 CAUSAL-DIAGNOSIS", guide, StringComparison.Ordinal);
		Assert.Contains("OBSERVABILITY: 3.4 HOST-METRICS", guide, StringComparison.Ordinal);
		Assert.Contains("OBSERVABILITY: 3.5 INSTRUMENTATION-GOVERNANCE", guide, StringComparison.Ordinal);
		Assert.Contains("OBSERVABILITY: 4.5 PERSISTENCE-ATTRIBUTION", guide, StringComparison.Ordinal);
		Assert.Contains("OBSERVABILITY: 5.1 MIGRATIONS", guide, StringComparison.Ordinal);
		Assert.Contains("OBSERVABILITY: 5.3 REDUCED-API-VERIFICATION", guide, StringComparison.Ordinal);
		Assert.Contains("OBSERVABILITY: 5.4 STATIC-SHAPE-BOUNDARIES", guide, StringComparison.Ordinal);
		Assert.Contains("OBSERVABILITY: 5.5 CONSOLIDATION", guide, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Generated local observability guide truthfully documents Story 2.1 failure isolation")]
	public void GeneratedLocalObservabilityGuideTruthfullyDocumentsStory21FailureIsolation()
	{
		var guide = TemplateDirectiveSource.Read("OBSERVABILITY.md");

		Assert.Contains("## Telemetry failure isolation", guide, StringComparison.Ordinal);
		Assert.Contains("best-effort diagnostic evidence", guide, StringComparison.Ordinal);
		Assert.Contains("selected OpenTelemetry SDK", guide, StringComparison.Ordinal);
		Assert.Contains("ordinary SDK lifecycle", guide, StringComparison.Ordinal);
		Assert.Contains("without restarting the Runtime Host", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("OBSERVABILITY: 2.1 FAILURE-ISOLATION", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("OBSERVABILITY: 2.6 HIGH-FIDELITY-BOUNDARY", guide, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Generated local observability guide is host-gated, safe, and discoverable")]
	public void GeneratedLocalObservabilityGuideIsHostGatedSafeAndDiscoverable()
	{
		var guide = ReadGuide();

		Assert.Contains("curl -sSL https://aspire.dev/install.sh | bash\n", guide, StringComparison.Ordinal);
		Assert.Contains("irm https://aspire.dev/install.ps1 | iex", guide, StringComparison.Ordinal);
		Assert.Contains("curl -sSL https://aspire.dev/install.sh | bash -s -- --version \"13.4.0\"", guide, StringComparison.Ordinal);
		Assert.Contains("iex \"& { $(irm https://aspire.dev/install.ps1) } -Version '13.4.0'\"", guide, StringComparison.Ordinal);
		Assert.Contains("aspire --version", guide, StringComparison.Ordinal);
		Assert.Contains("stable core `13.4.0` exactly", guide, StringComparison.Ordinal);
		Assert.Contains("prerelease", guide, StringComparison.Ordinal);
		Assert.Contains("+buildMetadata", guide, StringComparison.Ordinal);
		Assert.Contains("Preserve the full, unmodified version output as evidence", guide, StringComparison.Ordinal);
		Assert.Contains("Consumers are not hard-pinned", guide, StringComparison.Ordinal);
		Assert.Contains("aspire dashboard run --otlp-grpc-url http://localhost:4317", guide, StringComparison.Ordinal);
		Assert.Contains("tokenized URL", guide, StringComparison.Ordinal);
		Assert.Contains("loopback-only", guide, StringComparison.Ordinal);
		Assert.Contains("do not guess or use a bare Dashboard URL", guide, StringComparison.Ordinal);
		Assert.Contains("never retain, persist, log, upload, or copy it into evidence", guide, StringComparison.Ordinal);
		Assert.Contains("in-memory and lost on restart", guide, StringComparison.Ordinal);
		Assert.Contains("only telemetry-specific prerequisite", guide, StringComparison.Ordinal);
		Assert.Contains("SQL Server, RabbitMQ, Blob/Azurite, identity/Keycloak, migrations", guide, StringComparison.Ordinal);
		Assert.Contains("provision and verify them separately", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("<!--" + IfDirective + " (commonLibraries)", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("<!--" + IfDirective + " (!commonLibraries)", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("static external-observability handoff", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("--allow-anonymous", guide, StringComparison.Ordinal);
		Assert.Equal(CountOccurrences(guide, "<!--" + IfDirective), CountOccurrences(guide, "<!--" + EndIfDirective + " -->"));
	}

	[Fact(DisplayName = "Generated local observability guide documents only applicable Story 1.4 host profiles")]
	public void GeneratedLocalObservabilityGuideDocumentsOnlyApplicableStory14HostProfiles()
	{
		var guide = ReadGuide();

		Assert.Contains("## Runtime host profiles and resource identity", guide, StringComparison.Ordinal);
		Assert.Contains("<!--" + IfDirective + " (apiService) -->\n### API profile", guide, StringComparison.Ordinal);
		Assert.Contains("<!--" + IfDirective + " (workerService) -->\n### Worker profile", guide, StringComparison.Ordinal);
		Assert.Contains("<!--" + IfDirective + " (apiGateway) -->\n### Gateway profile", guide, StringComparison.Ordinal);
		Assert.Contains("generated fallbacks, then `OTEL_RESOURCE_ATTRIBUTES` collisions, then `OTEL_SERVICE_NAME`", guide, StringComparison.Ordinal);
		Assert.Contains("selected OpenTelemetry SDK detector without Monaco validation or content filtering", guide, StringComparison.Ordinal);
		Assert.Contains("Consumer deployment is responsible", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("static external-observability handoff", guide, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Generated local observability guide documents the successful-company counter only for API output")]
	public void GeneratedLocalObservabilityGuideDocumentsSuccessfulCompanyCounterOnlyForApiOutput()
	{
		var guide = ReadGuide();

		Assert.Contains("<!--" + IfDirective + " (apiService) -->\n## Successful company creations", guide, StringComparison.Ordinal);
		Assert.Contains("company.successful_creations", guide, StringComparison.Ordinal);
		Assert.Contains("only after a Company unit of work commits successfully", guide, StringComparison.Ordinal);
		Assert.Contains("Counter has no dimensions", guide, StringComparison.Ordinal);
		Assert.Contains("aggregate operational evidence", guide, StringComparison.Ordinal);
		Assert.Contains("Dashboard's Metrics view", guide, StringComparison.Ordinal);
		Assert.Contains("configuration-only Metrics routing", guide, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Generated local observability guide documents raw validated claim correlation for auth-enabled shapes")]
	public void GeneratedLocalObservabilityGuideDocumentsRawValidatedClaimCorrelationForAuthEnabledShapes()
	{
		var guide = ReadGuide();

		Assert.DoesNotContain("<!-- OBSERVABILITY: 2.5 IDENTITY -->", guide, StringComparison.Ordinal);
		Assert.Contains("## Authenticated request identity", guide, StringComparison.Ordinal);
		Assert.Contains("first non-empty raw validated `sub`", guide, StringComparison.Ordinal);
		Assert.Contains("raw validated `sub`", guide, StringComparison.Ordinal);
		Assert.Contains("`user.claim`", guide, StringComparison.Ordinal);
		Assert.Contains("`user.claims`", guide, StringComparison.Ordinal);
		Assert.Contains("native ASP.NET Core entry Activity", guide, StringComparison.Ordinal);
		Assert.Contains("Consumer infrastructure owns filtering", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("HmacSha256", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("Observability:Identity", guide, StringComparison.Ordinal);
	}

	private static string ReadGuide() =>
		TemplateDirectiveSource.Read("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);

	private static int CountOccurrences(string value, string searchValue) => value.Split(searchValue, StringSplitOptions.None).Length - 1;
}
