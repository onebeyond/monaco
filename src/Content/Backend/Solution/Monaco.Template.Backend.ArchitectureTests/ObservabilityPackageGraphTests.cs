using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Monaco.Template.Backend.Common.Observability;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Observability")]
public sealed class ObservabilityPackageGraphTests
{
	private const string TemplateDirectiveIf = "#" + "if";
	private const string TemplateDirectiveEndIf = "#" + "endif";

	private static readonly string[] OpenTelemetryPackages =
	[
		"OpenTelemetry.Extensions.Hosting",
		"OpenTelemetry.Exporter.OpenTelemetryProtocol",
		"OpenTelemetry.Instrumentation.Runtime",
		"OpenTelemetry.Instrumentation.Http",
		"OpenTelemetry.Instrumentation.AspNetCore",
		"OpenTelemetry.Instrumentation.SqlClient"
	];

	[Fact(DisplayName = "OpenTelemetry package versions are exact and centrally managed")]
	public void OpenTelemetryPackageVersionsAreExactAndCentrallyManaged()
	{
		var centralPackages = ReadSolutionFile("Directory.Packages.props");

		foreach (var package in OpenTelemetryPackages)
			Assert.Contains($"<PackageVersion Include=\"{package}\" Version=\"1.17.0\" />", centralPackages, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Only Common.Observability owns direct OpenTelemetry package references")]
	public void OnlyCommonObservabilityOwnsDirectOpenTelemetryPackageReferences()
	{
		var solutionDirectory = FindSolutionDirectory();
		var packageOwners = Directory.EnumerateFiles(solutionDirectory, "*.csproj", SearchOption.AllDirectories)
									 .Where(path => !IsBuildArtifact(path))
									 .Where(path => File.ReadAllText(path).Contains("PackageReference Include=\"OpenTelemetry.", StringComparison.Ordinal))
									 .Select(path => Path.GetFileName(path)!)
									 .ToArray();

		Assert.Equal(["Monaco.Template.Backend.Common.Observability.csproj"], packageOwners);
	}

	[Fact(DisplayName = "Common.Observability has no project dependencies or concealed package assets")]
	public void CommonObservabilityHasNoProjectDependenciesOrConcealedPackageAssets()
	{
		var project = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "Monaco.Template.Backend.Common.Observability.csproj"));

		Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
		Assert.DoesNotContain("PrivateAssets", project, StringComparison.Ordinal);
		Assert.Contains("<Project Sdk=\"Microsoft.NET.Sdk\">", project, StringComparison.Ordinal);
		Assert.DoesNotContain("PackageReference Include=\"Microsoft.AspNetCore", project, StringComparison.Ordinal);
		Assert.DoesNotContain("FrameworkReference Include=\"Microsoft.AspNetCore.App\"", project, StringComparison.Ordinal);
		Assert.Equal(OpenTelemetryPackages.Length, CountOccurrences(project, "PackageReference Include=\"OpenTelemetry."));
	}

	[Fact(DisplayName = "Application and domain projects remain independent from observability")]
	public void ApplicationAndDomainProjectsRemainIndependentFromObservability()
	{
		foreach (var projectName in new[] { "Monaco.Template.Backend.Application", "Monaco.Template.Backend.Domain", "Monaco.Template.Backend.Common.Domain" })
		{
			var project = ReadSolutionFile(Path.Combine(projectName, $"{projectName}.csproj"));

			Assert.DoesNotContain("OpenTelemetry", project, StringComparison.Ordinal);
			Assert.DoesNotContain("Common.Observability", project, StringComparison.Ordinal);
		}
	}

	[Fact(DisplayName = "Every eligible host references Common.Observability once and selects one applicable profile")]
	public void EligibleHostsReferenceThePackageOnceAndSelectOneApplicableProfile()
	{
		AssertHostUsesSingleProfile("Monaco.Template.Backend.Api", "AddApiObservability()");
		AssertHostUsesSingleProfile("Monaco.Template.Backend.Worker", "AddWorkerObservability()");
		AssertHostUsesSingleProfile("Monaco.Template.Backend.Common.ApiGateway", "AddGatewayObservability()");
	}

	[Fact(DisplayName = "API and Gateway install one ordinary HTTP exception handler while Worker remains outside the boundary")]
	public void HttpHostsInstallTheSharedBoundaryExceptionHandler()
	{
		var boundaryHandler = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "BoundaryExceptionHandler.cs"));
		var apiProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Api", "Program.cs"));
		var gatewayProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.ApiGateway", "Program.cs"));
		var workerProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Worker", "Program.cs"));

		apiProgram.Should().Contain("AddExceptionHandler<BoundaryExceptionHandler>()");
		gatewayProgram.Should().Contain("AddExceptionHandler<BoundaryExceptionHandler>()");
		apiProgram.Should().Contain("app.UseExceptionHandler()");
		gatewayProgram.Should().Contain("app.UseExceptionHandler()");
		boundaryHandler.Should().Contain("public sealed class BoundaryExceptionHandler");
		boundaryHandler.Should().Contain("ValueTask.FromResult(true)");
		boundaryHandler.Should().Contain("LogError(exception, \"Unhandled HTTP request failure.\")");
		boundaryHandler.Should().NotContain("Activity");
		File.Exists(Path.Combine(FindSolutionDirectory(), "Monaco.Template.Backend.Api", "BoundaryExceptionHandler.cs")).Should().BeFalse();
		File.Exists(Path.Combine(FindSolutionDirectory(), "Monaco.Template.Backend.Common.ApiGateway", "BoundaryExceptionHandler.cs")).Should().BeFalse();
		apiProgram.Should().NotContain("UseDeveloperExceptionPage");
		gatewayProgram.Should().NotContain("UseDeveloperExceptionPage");
		workerProgram.Should().NotContain("BoundaryExceptionHandler");
	}

	[Fact(DisplayName = "Application owns the BCL successful-company counter and only the API Metric profile subscribes to it")]
	public void ApplicationOwnsBclSuccessfulCompanyCounterAndOnlyApiMetricProfileSubscribes()
	{
		var diagnostics = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Application", "Diagnostics", "ApplicationDiagnostics.cs"));
		var createCompany = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Application", "Features", "Company", "CreateCompany.cs"));
		var profiles = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityHostBuilderExtensions.cs"));
		var template = ReadSolutionFile(Path.Combine(".template.config", "template.json"));

		Assert.Contains("using System.Diagnostics.Metrics;", diagnostics, StringComparison.Ordinal);
		Assert.Contains("internal const string MeterName = \"Monaco.Template.Backend.Application\"", diagnostics, StringComparison.Ordinal);
		Assert.Contains("private static readonly Meter Meter = new(MeterName)", diagnostics, StringComparison.Ordinal);
		Assert.Contains("Counter<long>", diagnostics, StringComparison.Ordinal);
		Assert.Contains("company.successful_creations", diagnostics, StringComparison.Ordinal);
		Assert.Contains("{company}", diagnostics, StringComparison.Ordinal);
		Assert.DoesNotContain("OpenTelemetry", diagnostics, StringComparison.Ordinal);
		Assert.DoesNotContain("Common.Observability", diagnostics, StringComparison.Ordinal);
		Assert.Contains("ApplicationDiagnostics.SuccessfulCompanyCreations.Add(1);", createCompany, StringComparison.Ordinal);
		Assert.DoesNotContain(TemplateDirectiveIf + " (apiService)", createCompany, StringComparison.Ordinal);
		Assert.DoesNotContain("Monaco.Template.Backend.Application/Diagnostics/ApplicationDiagnostics.cs", template, StringComparison.Ordinal);

		var metricsStart = profiles.IndexOf("private static void ConfigureMetrics", StringComparison.Ordinal);
		var metricsEnd = profiles.IndexOf("internal enum ObservabilityHostProfile", metricsStart, StringComparison.Ordinal);
		var metrics = profiles[metricsStart..metricsEnd];

		Assert.Contains("if (profile is ObservabilityHostProfile.Api)", metrics, StringComparison.Ordinal);
		Assert.Contains("builder.AddMeter(ApplicationMeterName);", metrics, StringComparison.Ordinal);
		Assert.Equal(1, CountOccurrences(metrics, "AddMeter("));
	}

	[Fact(DisplayName = "Public observability composition API exposes only host-builder entry points")]
	public void PublicObservabilityCompositionApiExposesOnlyHostBuilderEntryPoints()
	{
		var methods = typeof(ObservabilityHostBuilderExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
																.Where(method => method.Name.StartsWith("Add", StringComparison.Ordinal) && method.Name.EndsWith("Observability", StringComparison.Ordinal))
																.OrderBy(method => method.Name)
																.ToArray();

		Assert.Equal(["AddApiObservability", "AddGatewayObservability", "AddWorkerObservability"], methods.Select(method => method.Name));
		Assert.All(methods, method =>
							{
								Assert.Single(method.GetParameters());
								Assert.Equal(typeof(IHostApplicationBuilder), method.GetParameters()[0].ParameterType);
								Assert.DoesNotContain(method.GetParameters(), parameter => parameter.ParameterType == typeof(IConfiguration));
							});
	}

	[Fact(DisplayName = "Profile definitions contain only their applicable instrumentation")]
	public void ProfileDefinitionsContainOnlyTheirApplicableInstrumentation()
	{
		var profiles = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityHostBuilderExtensions.cs"));

		AssertProfileContainsOnly(profiles, "ApiInstrumentations", "WorkerInstrumentations", "Runtime", "AspNetCore", "Http", "SqlClient");
		AssertProfileContainsOnly(profiles, "WorkerInstrumentations", "GatewayInstrumentations", "Runtime", "Http", "SqlClient");
		AssertProfileContainsOnly(profiles, "GatewayInstrumentations", "internal enum ObservabilityInstrumentation", "Runtime", "AspNetCore", "Http");
	}

	[Fact(DisplayName = "Shared observability composition uses one unified OTLP exporter and applicable signal instrumentation")]
	public void SharedObservabilityCompositionUsesOneUnifiedOtlpExporterAndApplicableSignalInstrumentation()
	{
		var profiles = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityHostBuilderExtensions.cs"));

		Assert.Equal(1, CountOccurrences(profiles, "UseOtlpExporter"));
		Assert.DoesNotContain("AddOtlpExporter", profiles, StringComparison.Ordinal);
		Assert.Contains("WithLogging", profiles, StringComparison.Ordinal);
		Assert.Contains("WithTracing", profiles, StringComparison.Ordinal);
		Assert.Contains("WithMetrics", profiles, StringComparison.Ordinal);
		Assert.Contains("IncludeFormattedMessage = true", profiles, StringComparison.Ordinal);
		Assert.Contains("IncludeScopes = true", profiles, StringComparison.Ordinal);
		Assert.Contains("ParseStateValues = true", profiles, StringComparison.Ordinal);
		Assert.DoesNotContain("AddProcessor", profiles, StringComparison.Ordinal);
		Assert.Contains("ConfigureTracing", profiles, StringComparison.Ordinal);
		Assert.Contains("ConfigureMetrics", profiles, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Routing keeps no Collector or vendor artifacts, no duplicate exporter, and no secret-provider trust seam")]
	public void RoutingKeepsNoCollectorVendorArtifactsDuplicateExporterOrSecretProviderTrustSeam()
	{
		var solutionDirectory = FindSolutionDirectory();
		var observabilitySources = string.Concat(Directory.EnumerateFiles(Path.Combine(solutionDirectory, "Monaco.Template.Backend.Common.Observability"), "*.cs")
														  .Select(File.ReadAllText));
		var centralPackages = ReadSolutionFile("Directory.Packages.props");

		Assert.DoesNotContain("IObservabilitySecretProviderTrust", observabilitySources, StringComparison.Ordinal);
		Assert.DoesNotContain("OBS_CONFIG_SECRET_SOURCE_REJECTED", observabilitySources, StringComparison.Ordinal);
		Assert.DoesNotContain("SecretProvider", observabilitySources, StringComparison.Ordinal);
		Assert.DoesNotContain("GetProviders", observabilitySources, StringComparison.Ordinal);

		Assert.Equal(OpenTelemetryPackages.Length, CountOccurrences(centralPackages, "PackageVersion Include=\"OpenTelemetry."));
		Assert.DoesNotContain("OpenTelemetry.Exporter.Jaeger", centralPackages, StringComparison.Ordinal);
		Assert.DoesNotContain("OpenTelemetry.Exporter.Prometheus", centralPackages, StringComparison.Ordinal);
		Assert.DoesNotContain("OpenTelemetry.Exporter.InMemory", centralPackages, StringComparison.Ordinal);

		Assert.DoesNotContain(Directory.EnumerateFiles(solutionDirectory, "*", SearchOption.AllDirectories).Where(path => !IsBuildArtifact(path)),
							  path =>
							  {
								  var fileName = Path.GetFileName(path);
								  return fileName.Contains("collector", StringComparison.OrdinalIgnoreCase) ||
										 fileName.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase) ||
										 fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
										 fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
										 fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);
							  });
	}

	[Fact(DisplayName = "Shared observability composition contains no Monaco telemetry governance surface")]
	public void SharedObservabilityCompositionContainsNoMonacoTelemetryGovernanceSurface()
	{
		var solutionDirectory = FindSolutionDirectory();
		var observabilityDirectory = Path.Combine(solutionDirectory, "Monaco.Template.Backend.Common.Observability");
		var profiles = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityHostBuilderExtensions.cs"));
		var sources = string.Concat(Directory.EnumerateFiles(observabilityDirectory, "*.cs").Select(File.ReadAllText));
		var applicationOptions = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Application", "DependencyInjection", "ApplicationOptions.cs"));
		var applicationServices = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Application", "DependencyInjection", "ServiceCollectionExtensions.cs"));
		var apiProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Api", "Program.cs"));
		var workerProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Worker", "Program.cs"));
		var apiSettings = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Api", "appsettings.Development.json"));
		var workerSettings = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Worker", "appsettings.Development.json"));

		Assert.Contains("AddHttpClientInstrumentation()", profiles, StringComparison.Ordinal);
		Assert.DoesNotContain("FilterHttpRequestMessage", profiles, StringComparison.Ordinal);
		Assert.DoesNotContain("BaseProcessor", sources, StringComparison.Ordinal);
		Assert.DoesNotContain("AddProcessor", sources, StringComparison.Ordinal);
		Assert.DoesNotContain("Sanitiz", sources, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("Redact", sources, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("Hmac", sources, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("QueryTextMode", sources, StringComparison.Ordinal);
		Assert.DoesNotContain("FlushTimeout", sources, StringComparison.Ordinal);
		Assert.DoesNotContain("Preflight", sources, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("EnableSensitiveDataLogging", sources, StringComparison.Ordinal);
		Assert.DoesNotContain("EnableEFSensitiveLogging", string.Concat(applicationOptions, applicationServices, apiProgram, workerProgram, apiSettings, workerSettings), StringComparison.Ordinal);
		Assert.DoesNotContain("EnableSensitiveDataLogging", string.Concat(applicationOptions, applicationServices, apiProgram, workerProgram, apiSettings, workerSettings), StringComparison.Ordinal);

		foreach (var policyUnit in new[]
								   {
									   "BoundaryExceptionDiagnostics.cs",
									   "HmacSha256Identity.cs",
									   "HttpTelemetryPrivacyProcessor.cs",
									   "LogTelemetryPrivacyProcessor.cs",
									   "ObservabilityConfigurationDiagnostics.cs",
									   "ObservabilityExporterDiagnosticListener.cs",
									   "ObservabilityShutdownFlushService.cs",
									   "OtelConfigurationPreflightValidator.cs",
									   "SqlClientTelemetryPrivacyProcessor.cs"
								   })
			Assert.False(File.Exists(Path.Combine(observabilityDirectory, policyUnit)), $"{policyUnit} must remain absent.");
	}

	[Fact(DisplayName = "Generated guide documents the high-fidelity producer and ordinary HTTP exception boundary")]
	public void GeneratedGuideDocumentsHighFidelityProducerAndOrdinaryHttpExceptionBoundary()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);

		Assert.DoesNotContain("OBSERVABILITY: 2.6 HIGH-FIDELITY-BOUNDARY", guide, StringComparison.Ordinal);
		Assert.Contains("## High-fidelity telemetry boundary", guide, StringComparison.Ordinal);
		Assert.Contains("does not filter, redact, sanitize, transform, sample, or bound", guide, StringComparison.Ordinal);
		Assert.Contains("consumer-owned Collector", guide, StringComparison.Ordinal);
		Assert.Contains("filtering, redaction, transformation, sampling, cardinality controls, routing, storage, access, retention, and alerting", guide, StringComparison.Ordinal);
		Assert.Contains("Gateway has no SQL instrumentation", guide, StringComparison.Ordinal);
		guide.Should().Contain("<!--" + TemplateDirectiveIf + " (apiService || apiGateway) -->\n## HTTP exception boundaries");
		guide.Should().Contain("ordinary structured error log");
		guide.Should().Contain("normal ASP.NET Core and selected instrumentation behavior");
	}

	[Fact(DisplayName = "Generated local observability guide documents Story 2.4 production routing and preserves later placeholders")]
	public void GeneratedLocalObservabilityGuideDocumentsStory24ProductionRoutingAndPreservesLaterPlaceholders()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);

		Assert.Contains("| Production routing below | 2.4 |", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("OBSERVABILITY: 2.4 PRODUCTION-ROUTING", guide, StringComparison.Ordinal);

		var sectionStart = guide.IndexOf("## Production signal routing", StringComparison.Ordinal);
		Assert.True(sectionStart >= 0, "Expected the Story 2.4 production-routing section in the guide.");
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

	[Fact(DisplayName = "Failure isolation uses the ordinary SDK lifecycle without Monaco policy services")]
	public void FailureIsolationUsesOrdinarySdkLifecycleWithoutMonacoPolicyServices()
	{
		var observabilityDirectory = Path.Combine(FindSolutionDirectory(), "Monaco.Template.Backend.Common.Observability");
		var profiles = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityHostBuilderExtensions.cs"));

		Assert.DoesNotContain("IHostedService", profiles, StringComparison.Ordinal);
		Assert.DoesNotContain("ForceFlush", profiles, StringComparison.Ordinal);
		Assert.DoesNotContain("SetMissing", profiles, StringComparison.Ordinal);
		Assert.DoesNotContain("ApplyAdoptedDefaults", profiles, StringComparison.Ordinal);
		Assert.False(File.Exists(Path.Combine(observabilityDirectory, "ObservabilityShutdownFlushService.cs")));
		Assert.False(File.Exists(Path.Combine(observabilityDirectory, "ObservabilityExporterDiagnosticListener.cs")));
		Assert.False(File.Exists(Path.Combine(observabilityDirectory, "ObservabilityConfigurationDiagnostics.cs")));
	}

	[Fact(DisplayName = "Generated local observability guide truthfully documents Story 2.1 failure isolation")]
	public void GeneratedLocalObservabilityGuideTruthfullyDocumentsStory21FailureIsolation()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md");

		Assert.Contains("## Telemetry failure isolation", guide, StringComparison.Ordinal);
		Assert.Contains("best-effort diagnostic evidence", guide, StringComparison.Ordinal);
		Assert.Contains("selected OpenTelemetry SDK", guide, StringComparison.Ordinal);
		Assert.Contains("ordinary SDK lifecycle", guide, StringComparison.Ordinal);
		Assert.Contains("without restarting the Runtime Host", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("OBSERVABILITY: 2.1 FAILURE-ISOLATION", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("OBSERVABILITY: 2.6 HIGH-FIDELITY-BOUNDARY", guide, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Template separates Common delivery from host observability")]
	public void TemplateSeparatesCommonDeliveryFromHostObservability()
	{
		var template = ReadSolutionFile(Path.Combine(".template.config", "template.json"));

		Assert.Contains("\"commonLibraries\"", template, StringComparison.Ordinal);
		Assert.Contains("\"datatype\": \"bool\"", template, StringComparison.Ordinal);
		Assert.Contains("\"defaultValue\": \"true\"", template, StringComparison.Ordinal);
		Assert.Contains("\"condition\": \"(!commonLibraries)\"", template, StringComparison.Ordinal);
		Assert.Contains("(!commonLibraries || (!apiService && !workerService && !apiGateway))", template, StringComparison.Ordinal);
		Assert.Contains("(!commonLibraries || !apiService || !workerService || !apiGateway)", template, StringComparison.Ordinal);
		Assert.Contains("Monaco.Template.Backend.Common.Observability/**/*", template, StringComparison.Ordinal);
		Assert.Contains("Monaco.Template.Backend.ArchitectureTests/ObservabilityPackageGraphTests.cs", template, StringComparison.Ordinal);
		Assert.Contains("Monaco.Template.Backend.ArchitectureTests/ObservabilityResourceTests.cs", template, StringComparison.Ordinal);
		Assert.DoesNotContain("Monaco.Template.Backend.*.Api*/**/*", template, StringComparison.Ordinal);
		Assert.Contains("(!apiService && !apiGateway)", template, StringComparison.Ordinal);

		foreach (var hostProject in new[] { "Monaco.Template.Backend.Api", "Monaco.Template.Backend.Worker", "Monaco.Template.Backend.Common.ApiGateway" })
		{
			var program = ReadSolutionFile(Path.Combine(hostProject, "Program.cs"));

			Assert.DoesNotContain(TemplateDirectiveIf + " (commonLibraries)", program, StringComparison.Ordinal);
			Assert.DoesNotContain(TemplateDirectiveIf + " (!commonLibraries)", program, StringComparison.Ordinal);
		}
	}

	[Fact(DisplayName = "Generated local observability guide is host-gated, safe, and discoverable")]
	public void GeneratedLocalObservabilityGuideIsHostGatedSafeAndDiscoverable()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);
		var template = ReadSolutionFile(Path.Combine(".template.config", "template.json"));
		var solution = ReadSolutionFile("Monaco.Template.Backend.slnx");

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
		Assert.DoesNotContain("<!--" + TemplateDirectiveIf + " (commonLibraries)", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("<!--" + TemplateDirectiveIf + " (!commonLibraries)", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("static external-observability handoff", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("--allow-anonymous", guide, StringComparison.Ordinal);
		Assert.Equal(CountOccurrences(guide, "<!--" + TemplateDirectiveIf), CountOccurrences(guide, "<!--" + TemplateDirectiveEndIf + " -->"));

		using var templateDocument = JsonDocument.Parse(template);
		var root = templateDocument.RootElement;

		Assert.Contains(root.GetProperty("sources")[0]
							.GetProperty("modifiers")
							.EnumerateArray(),
						modifier => modifier.TryGetProperty("condition", out var condition) &&
									condition.GetString() == "(!apiService && !workerService && !apiGateway)" &&
									modifier.GetProperty("exclude").EnumerateArray().Any(excluded => excluded.GetString() == "OBSERVABILITY.md"));

		var postAction = Assert.Single(root.GetProperty("postActions").EnumerateArray());
		Assert.Equal("AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C", postAction.GetProperty("actionId").GetString());
		Assert.Equal("(apiService || workerService || apiGateway)", postAction.GetProperty("condition").GetString());

		var primaryOutput = Assert.Single(root.GetProperty("primaryOutputs").EnumerateArray());
		Assert.Equal("Monaco.Template.Backend.slnx", primaryOutput.GetProperty("path").GetString());

		var solutionItems = solution.IndexOf("<Folder Name=\"/Solution Items/\">", StringComparison.Ordinal);
		var guideGate = solution.IndexOf("<!--" + TemplateDirectiveIf + " (apiService || workerService || apiGateway) -->", solutionItems, StringComparison.Ordinal);
		var guideEntry = solution.IndexOf("<File Path=\"OBSERVABILITY.md\" />", solutionItems, StringComparison.Ordinal);
		var guideGateEnd = solution.IndexOf("<!--" + TemplateDirectiveEndIf + " -->", guideGate, StringComparison.Ordinal);
		Assert.True(solutionItems >= 0 && guideGate > solutionItems && guideEntry > guideGate && guideGateEnd > guideEntry,
					"The OBSERVABILITY.md Solution Item must sit inside its own Runtime Host conditional within /Solution Items/.");
	}

	[Fact(DisplayName = "Common delivery keeps source references and emits package references for every runtime edge")]
	public void CommonDeliveryKeepsSourceReferencesAndEmitsPackageReferencesForEveryRuntimeEdge()
	{
		var centralPackages = ReadSolutionFile("Directory.Packages.props");

		Assert.Contains("<!--" + TemplateDirectiveIf + " (!commonLibraries)-->", centralPackages, StringComparison.Ordinal);

		AssertDeliveryEdge("Monaco.Template.Backend.Domain", "Monaco.Template.Backend.Common.Domain");
		AssertDeliveryEdge("Monaco.Template.Backend.Application", "Monaco.Template.Backend.Common.Application");
		AssertDeliveryEdge("Monaco.Template.Backend.Application", "Monaco.Template.Backend.Common.Infrastructure");
		AssertDeliveryEdge("Monaco.Template.Backend.Application", "Monaco.Template.Backend.Common.BlobStorage");
		AssertDeliveryEdge("Monaco.Template.Backend.Api", "Monaco.Template.Backend.Common.Api.Application");
		AssertDeliveryEdge("Monaco.Template.Backend.Api", "Monaco.Template.Backend.Common.Api");
		AssertDeliveryEdge("Monaco.Template.Backend.Api", "Monaco.Template.Backend.Common.Observability");
		AssertDeliveryEdge("Monaco.Template.Backend.Worker", "Monaco.Template.Backend.Common.Observability");
		AssertDeliveryEdge("Monaco.Template.Backend.Common.ApiGateway", "Monaco.Template.Backend.Common.Api");
		AssertDeliveryEdge("Monaco.Template.Backend.Common.ApiGateway", "Monaco.Template.Backend.Common.Observability");
		AssertDeliveryEdge("Monaco.Template.Backend.Application.Tests", "Monaco.Template.Backend.Common.Tests");
		AssertDeliveryEdge("Monaco.Template.Backend.Domain.Tests", "Monaco.Template.Backend.Common.Tests");

		foreach (var package in new[]
								{
									"Monaco.Template.Backend.Common.Domain",
									"Monaco.Template.Backend.Common.Application",
									"Monaco.Template.Backend.Common.Infrastructure",
									"Monaco.Template.Backend.Common.BlobStorage",
									"Monaco.Template.Backend.Common.Api.Application",
									"Monaco.Template.Backend.Common.Api",
									"Monaco.Template.Backend.Common.Observability",
									"Monaco.Template.Backend.Common.Tests"
								})
			Assert.Contains($"<PackageVersion Include=\"{package}\" Version=\"0.0.1-alpha1\" />", centralPackages, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Generated local observability guide documents only applicable Story 1.4 host profiles")]
	public void GeneratedLocalObservabilityGuideDocumentsOnlyApplicableStory14HostProfiles()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);

		Assert.Contains("## Runtime host profiles and resource identity", guide, StringComparison.Ordinal);
		Assert.Contains("<!--" + TemplateDirectiveIf + " (apiService) -->\n### API profile", guide, StringComparison.Ordinal);
		Assert.Contains("<!--" + TemplateDirectiveIf + " (workerService) -->\n### Worker profile", guide, StringComparison.Ordinal);
		Assert.Contains("<!--" + TemplateDirectiveIf + " (apiGateway) -->\n### Gateway profile", guide, StringComparison.Ordinal);
		Assert.Contains("generated fallbacks, then `OTEL_RESOURCE_ATTRIBUTES` collisions, then `OTEL_SERVICE_NAME`", guide, StringComparison.Ordinal);
		Assert.Contains("selected OpenTelemetry SDK detector without Monaco validation or content filtering", guide, StringComparison.Ordinal);
		Assert.Contains("Consumer deployment is responsible", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("static external-observability handoff", guide, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Generated local observability guide documents the successful-company counter only for API output")]
	public void GeneratedLocalObservabilityGuideDocumentsSuccessfulCompanyCounterOnlyForApiOutput()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);

		Assert.Contains("<!--" + TemplateDirectiveIf + " (apiService) -->\n## Successful company creations", guide, StringComparison.Ordinal);
		Assert.Contains("company.successful_creations", guide, StringComparison.Ordinal);
		Assert.Contains("only after a Company unit of work commits successfully", guide, StringComparison.Ordinal);
		Assert.Contains("Counter has no dimensions", guide, StringComparison.Ordinal);
		Assert.Contains("aggregate operational evidence", guide, StringComparison.Ordinal);
		Assert.Contains("Dashboard's Metrics view", guide, StringComparison.Ordinal);
		Assert.Contains("configuration-only Metrics routing", guide, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Identity enrichment retains raw validated claims without Monaco policy")]
	public void IdentityEnrichmentRetainsRawValidatedClaimsWithoutMonacoPolicy()
	{
		var centralPackages = ReadSolutionFile("Directory.Packages.props");
		var middleware = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "IdentityEnrichmentMiddleware.cs"));

		Assert.Equal(OpenTelemetryPackages.Length, CountOccurrences(centralPackages, "PackageVersion Include=\"OpenTelemetry."));
		Assert.Contains("System.Collections.Immutable", middleware, StringComparison.Ordinal);
		Assert.Contains("System.Diagnostics", middleware, StringComparison.Ordinal);
		Assert.Contains(".ToImmutableArray()", middleware, StringComparison.Ordinal);
		Assert.Contains("ValidatedClaimContext(string Type", middleware, StringComparison.Ordinal);
		Assert.Contains("FirstOrDefault(claim => claim.Type == SubClaimType && !string.IsNullOrEmpty(claim.Value))?.Value", middleware, StringComparison.Ordinal);
		Assert.Contains("if (!string.IsNullOrEmpty(sub))", middleware, StringComparison.Ordinal);
		Assert.Contains("SetTag(UserIdTag, sub)", middleware, StringComparison.Ordinal);
		Assert.Contains("AddEvent(CreateClaimEvent(claim))", middleware, StringComparison.Ordinal);
		Assert.Contains("BeginScope", middleware, StringComparison.Ordinal);
		Assert.Contains("user.claims", middleware, StringComparison.Ordinal);

		foreach (var attribute in new[] { "user.claim.type", "user.claim.value", "user.claim.value_type", "user.claim.issuer", "user.claim.original_issuer" })
			Assert.Contains(attribute, middleware, StringComparison.Ordinal);

		Assert.DoesNotContain("Hmac", middleware, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("IdentityMode", middleware, StringComparison.Ordinal);
		Assert.False(File.Exists(Path.Combine(FindSolutionDirectory(), "Monaco.Template.Backend.Common.Observability", "HmacSha256Identity.cs")));
	}

	[Fact(DisplayName = "API and Gateway Program.cs wire identity enrichment between authentication and authorization")]
	public void ApiAndGatewayProgramWireIdentityEnrichmentBetweenAuthenticationAndAuthorization()
	{
		var apiProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Api", "Program.cs"));
		var gatewayProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.ApiGateway", "Program.cs"));
		var workerProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Worker", "Program.cs"));

		Assert.Contains("UseAuthentication()", apiProgram, StringComparison.Ordinal);
		Assert.Contains("UseIdentityEnrichment()", apiProgram, StringComparison.Ordinal);
		Assert.Contains("UseAuthentication()", gatewayProgram, StringComparison.Ordinal);
		Assert.Contains("UseIdentityEnrichment()", gatewayProgram, StringComparison.Ordinal);
		Assert.DoesNotContain("UseIdentityEnrichment", workerProgram, StringComparison.Ordinal);
		Assert.DoesNotContain("UseAuthentication", workerProgram, StringComparison.Ordinal);

		var apiAuthIndex = apiProgram.IndexOf("UseAuthentication()", StringComparison.Ordinal);
		var apiIdentityIndex = apiProgram.IndexOf("UseIdentityEnrichment()", StringComparison.Ordinal);
		var apiAuthzIndex = apiProgram.IndexOf("UseAuthorization()", StringComparison.Ordinal);
		Assert.True(apiAuthIndex < apiIdentityIndex && apiIdentityIndex < apiAuthzIndex,
					"API middleware order must be: UseAuthentication → UseIdentityEnrichment → UseAuthorization.");

		var gwAuthIndex = gatewayProgram.IndexOf("UseAuthentication()", StringComparison.Ordinal);
		var gwIdentityIndex = gatewayProgram.IndexOf("UseIdentityEnrichment()", StringComparison.Ordinal);
		var gwAuthzIndex = gatewayProgram.IndexOf("UseAuthorization()", StringComparison.Ordinal);
		Assert.True(gwAuthIndex < gwIdentityIndex && gwIdentityIndex < gwAuthzIndex,
					"Gateway middleware order must be: UseAuthentication → UseIdentityEnrichment → UseAuthorization.");
	}

	[Fact(DisplayName = "Generated local observability guide documents raw validated claim correlation for auth-enabled shapes")]
	public void GeneratedLocalObservabilityGuideDocumentsRawValidatedClaimCorrelationForAuthEnabledShapes()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);

		Assert.DoesNotContain("<!-- OBSERVABILITY: 2.5 IDENTITY -->", guide, StringComparison.Ordinal);
		Assert.Contains("## Authenticated request identity", guide, StringComparison.Ordinal);
		Assert.Contains("first non-empty raw validated `sub`", guide, StringComparison.Ordinal);
		Assert.Contains("raw validated `sub`", guide, StringComparison.Ordinal);
		Assert.Contains("`user.claim`", guide, StringComparison.Ordinal);
		Assert.Contains("`user.claims`", guide, StringComparison.Ordinal);
		Assert.Contains("native ASP.NET Core entry Activity", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("HmacSha256", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("Observability:Identity", guide, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Identity enrichment middleware emits claim events only to the entry span and logging scope")]
	public void IdentityEnrichmentEmitsClaimEventsOnlyToEntrySpanAndLoggingScope()
	{
		var middleware = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "IdentityEnrichmentMiddleware.cs"));

		Assert.Contains("Activity.Current", middleware, StringComparison.Ordinal);
		Assert.Contains("AddEvent", middleware, StringComparison.Ordinal);
		Assert.Contains("BeginScope", middleware, StringComparison.Ordinal);
		Assert.DoesNotContain("Baggage", middleware, StringComparison.Ordinal);
		Assert.DoesNotContain("AddBaggage", middleware, StringComparison.Ordinal);
		Assert.DoesNotContain("RecordObservable", middleware, StringComparison.Ordinal);
		Assert.DoesNotContain("StartActivity", middleware, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Observability resource attributes do not include user.id")]
	public void ObservabilityResourceAttributesDoNotIncludeUserId()
	{
		var resource = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityResource.cs"));

		Assert.DoesNotContain("user.id", resource, StringComparison.Ordinal);
	}

	private static void AssertHostUsesSingleProfile(string hostProject, string profileMethod)
	{
		var project = ReadSolutionFile(Path.Combine(hostProject, $"{hostProject}.csproj"));
		var program = ReadSolutionFile(Path.Combine(hostProject, "Program.cs"));

		Assert.Equal(1, CountOccurrences(project, "Monaco.Template.Backend.Common.Observability.csproj"));
		Assert.Equal(1, CountOccurrences(project, "PackageReference Include=\"Monaco.Template.Backend.Common.Observability\""));
		Assert.Equal(1, CountOccurrences(program, profileMethod));
	}

	private static void AssertDeliveryEdge(string projectName, string commonProjectName)
	{
		var project = ReadSolutionFile(Path.Combine(projectName, $"{projectName}.csproj"));

		Assert.Contains($"ProjectReference Include=\"..\\{commonProjectName}\\{commonProjectName}.csproj\"", project, StringComparison.Ordinal);
		Assert.Contains($"PackageReference Include=\"{commonProjectName}\"", project, StringComparison.Ordinal);
	}

	private static string ReadSolutionFile(string relativePath) => File.ReadAllText(Path.Combine(FindSolutionDirectory(), relativePath));

	private static int CountOccurrences(string value, string searchValue) => value.Split(searchValue, StringSplitOptions.None).Length - 1;

	private static void AssertProfileContainsOnly(string profiles, string profileName, string nextProfileName, params string[] expectedInstrumentations)
	{
		var profileStart = profiles.IndexOf(profileName, StringComparison.Ordinal);
		var profileEnd = profiles.IndexOf(nextProfileName, profileStart, StringComparison.Ordinal);
		var profile = profiles[profileStart..profileEnd];

		foreach (var instrumentation in expectedInstrumentations)
			Assert.Contains($"ObservabilityInstrumentation.{instrumentation}", profile, StringComparison.Ordinal);

		Assert.Equal(expectedInstrumentations.Length, CountOccurrences(profile, "ObservabilityInstrumentation."));
	}

	private static bool IsBuildArtifact(string path) => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
														path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

	private static string FindSolutionDirectory()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
			if (File.Exists(Path.Combine(directory.FullName, "Monaco.Template.Backend.slnx")))
				return directory.FullName;

		throw new DirectoryNotFoundException("Could not locate Monaco.Template.Backend.slnx from the test output directory.");
	}
}