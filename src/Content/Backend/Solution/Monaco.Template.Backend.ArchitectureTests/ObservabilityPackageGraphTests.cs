using System.Text.Json;
using System.Reflection;
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

	[Fact(DisplayName = "API and Gateway install one shared HTTP boundary diagnostics handler while Worker remains outside the boundary")]
	public void HttpHostsInstallTheSharedBoundaryDiagnosticsHandler()
	{
		var boundaryDiagnostics = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "BoundaryExceptionDiagnostics.cs"));
		var boundaryHandler = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "BoundaryExceptionHandler.cs"));
		var apiProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Api", "Program.cs"));
		var gatewayProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.ApiGateway", "Program.cs"));
		var workerProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Worker", "Program.cs"));

		boundaryDiagnostics.Should().Contain("EventId = new(1000, \"UnhandledBoundaryException\")");
		boundaryDiagnostics.Should().Contain("Unhandled {BoundaryKind} failure ({ExceptionType}); TraceId={TraceId}; SpanId={SpanId}");
		boundaryDiagnostics.Should().Contain("HttpRequestBoundaryKind = \"http.request\"");
		boundaryDiagnostics.Should().Contain("SetStatus(ActivityStatusCode.Error, string.Empty)");
		boundaryDiagnostics.Should().Contain("error.category");
		boundaryDiagnostics.Should().Contain("error.type");
		apiProgram.Should().Contain("AddExceptionHandler<BoundaryExceptionHandler>()");
		gatewayProgram.Should().Contain("AddExceptionHandler<BoundaryExceptionHandler>()");
		apiProgram.Should().Contain("app.UseExceptionHandler()");
		gatewayProgram.Should().Contain("app.UseExceptionHandler()");
		boundaryHandler.Should().Contain("public sealed class BoundaryExceptionHandler");
		boundaryHandler.Should().Contain("ValueTask.FromResult(true)");
		boundaryHandler.Should().Contain("BoundaryExceptionDiagnostics.Record");
		File.Exists(Path.Combine(FindSolutionDirectory(), "Monaco.Template.Backend.Api", "BoundaryExceptionHandler.cs")).Should().BeFalse();
		File.Exists(Path.Combine(FindSolutionDirectory(), "Monaco.Template.Backend.Common.ApiGateway", "BoundaryExceptionHandler.cs")).Should().BeFalse();
		apiProgram.Should().NotContain("UseDeveloperExceptionPage");
		gatewayProgram.Should().NotContain("UseDeveloperExceptionPage");
		workerProgram.Should().NotContain("BoundaryExceptionDiagnostics.Record");
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
		Assert.Contains("IncludeFormattedMessage = false", profiles, StringComparison.Ordinal);
		Assert.Contains("IncludeScopes = false", profiles, StringComparison.Ordinal);
		Assert.Contains("ParseStateValues = false", profiles, StringComparison.Ordinal);
		Assert.Contains("LogTelemetryPrivacyProcessor", profiles, StringComparison.Ordinal);
		Assert.Contains("ConfigureTracing", profiles, StringComparison.Ordinal);
		Assert.Contains("ConfigureMetrics", profiles, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Shared observability composition fixes privacy safeguards and removes the EF sensitive-data escape hatch")]
	public void SharedObservabilityCompositionFixesPrivacySafeguardsAndRemovesTheEfSensitiveDataEscapeHatch()
	{
		var profiles = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityHostBuilderExtensions.cs"));
		var sqlPrivacy = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "SqlClientTelemetryPrivacyProcessor.cs"));
		var httpPrivacy = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "HttpTelemetryPrivacyProcessor.cs"));
		var logPrivacy = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "LogTelemetryPrivacyProcessor.cs"));
		var preflight = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "OtelConfigurationPreflightValidator.cs"));
		var diagnostics = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityConfigurationDiagnostics.cs"));
		var applicationOptions = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Application", "DependencyInjection", "ApplicationOptions.cs"));
		var applicationServices = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Application", "DependencyInjection", "ServiceCollectionExtensions.cs"));
		var apiProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Api", "Program.cs"));
		var workerProgram = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Worker", "Program.cs"));
		var apiSettings = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Api", "appsettings.Development.json"));
		var workerSettings = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Worker", "appsettings.Development.json"));

		Assert.Contains("FilterHttpRequestMessage", profiles, StringComparison.Ordinal);
		Assert.Contains("HttpTelemetryPrivacyProcessor", profiles, StringComparison.Ordinal);
		Assert.Contains("SqlClientTelemetryPrivacyProcessor", profiles, StringComparison.Ordinal);
		Assert.Contains("RecordException = false", profiles, StringComparison.Ordinal);
		Assert.DoesNotContain("EnrichWithSqlCommand", profiles, StringComparison.Ordinal);
		Assert.Contains("db.query.parameter.", sqlPrivacy, StringComparison.Ordinal);
		Assert.Contains("db.system", sqlPrivacy, StringComparison.Ordinal);
		Assert.Contains("db.connection_string", sqlPrivacy, StringComparison.Ordinal);
		Assert.Contains("MaximumQueryTextLength", sqlPrivacy, StringComparison.Ordinal);
		Assert.Contains("SqlSanitizationRejected", sqlPrivacy, StringComparison.Ordinal);
		Assert.Contains("OBS_SQL_SANITIZATION_REJECTED", diagnostics, StringComparison.Ordinal);
		Assert.Contains("http.url", httpPrivacy, StringComparison.Ordinal);
		Assert.Contains("http.target", httpPrivacy, StringComparison.Ordinal);
		Assert.Contains("data.FormattedMessage = null", logPrivacy, StringComparison.Ordinal);
		Assert.Contains("data.Attributes = null", logPrivacy, StringComparison.Ordinal);
		Assert.Contains("OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS", preflight, StringComparison.Ordinal);
		Assert.Contains("OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_CONTEXT_PROPAGATION", preflight, StringComparison.Ordinal);
		Assert.DoesNotContain("EnableEFSensitiveLogging", string.Concat(applicationOptions, applicationServices, apiProgram, workerProgram, apiSettings, workerSettings), StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Generated local observability guide documents Story 2.2 data boundaries and Story 2.3 HTTP boundaries only for applicable hosts")]
	public void GeneratedLocalObservabilityGuideDocumentsStory22DataBoundariesAndStory23HttpBoundariesOnlyForApplicableHosts()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md").Replace("\r\n", "\n", StringComparison.Ordinal);

		Assert.Contains("<!-- OBSERVABILITY: 2.2-2.3 DATA-BOUNDARIES -->\n## Data-minimizing telemetry", guide, StringComparison.Ordinal);
		Assert.Contains("request or response bodies", guide, StringComparison.Ordinal);
		Assert.Contains("authorization or cookie headers", guide, StringComparison.Ordinal);
		Assert.Contains("connection strings", guide, StringComparison.Ordinal);
		Assert.Contains("Company email or address data", guide, StringComparison.Ordinal);
		Assert.Contains("trace `url.path`", guide, StringComparison.Ordinal);
		Assert.Contains("bounded `http.route`", guide, StringComparison.Ordinal);
		Assert.Contains("SanitizedText", guide, StringComparison.Ordinal);
		Assert.Contains("4 KiB", guide, StringComparison.Ordinal);
		Assert.Contains("rather than emitting or hashing raw SQL", guide, StringComparison.Ordinal);
		Assert.Contains("<!--" + TemplateDirectiveIf + " (apiService || workerService) -->", guide, StringComparison.Ordinal);
		Assert.Contains("Gateway has no SQL instrumentation", guide, StringComparison.Ordinal);
		guide.Should().Contain("<!--" + TemplateDirectiveIf + " (apiService || apiGateway) -->\n## HTTP exception boundaries");
		guide.Should().Contain("exactly one authoritative Operational Log");
		guide.Should().Contain("exception message, stack, and inner chain remain only on that single boundary log");
		guide.Should().Contain("opaque third-party exception prose");
	}

	[Fact(DisplayName = "Failure isolation keeps bounded queues, one coordinated flush, and no retry or disk storage path")]
	public void FailureIsolationKeepsBoundedQueuesOneCoordinatedFlushAndNoRetryOrDiskStoragePath()
	{
		var profiles = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityHostBuilderExtensions.cs"));
		var lifecycle = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityShutdownFlushService.cs"));
		var preflight = ReadSolutionFile(Path.Combine("Monaco.Template.Backend.Common.Observability", "OtelConfigurationPreflightValidator.cs"));

		Assert.Contains("2048", profiles, StringComparison.Ordinal);
		Assert.Contains("512", profiles, StringComparison.Ordinal);
		Assert.Contains("5000", profiles, StringComparison.Ordinal);
		Assert.Contains("60000", profiles, StringComparison.Ordinal);
		Assert.Contains("Task.WhenAll", lifecycle, StringComparison.Ordinal);
		Assert.Contains("options.FlushTimeout", lifecycle, StringComparison.Ordinal);
		Assert.Contains("services.Insert(0", profiles, StringComparison.Ordinal);
		Assert.Contains("traceBatch > traceQueue", preflight, StringComparison.Ordinal);
		Assert.Contains("logBatch > logQueue", preflight, StringComparison.Ordinal);
		Assert.Contains("OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY", preflight, StringComparison.Ordinal);
		Assert.Contains("OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH", preflight, StringComparison.Ordinal);
		Assert.DoesNotContain("Retry", profiles + lifecycle, StringComparison.Ordinal);
		Assert.DoesNotContain("Disk", profiles + lifecycle, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Generated local observability guide truthfully documents Story 2.1 failure isolation")]
	public void GeneratedLocalObservabilityGuideTruthfullyDocumentsStory21FailureIsolation()
	{
		var guide = ReadSolutionFile("OBSERVABILITY.md");

		Assert.Contains("## Telemetry failure isolation", guide, StringComparison.Ordinal);
		Assert.Contains("best-effort diagnostic evidence", guide, StringComparison.Ordinal);
		Assert.Contains("drops new telemetry", guide, StringComparison.Ordinal);
		Assert.Contains("one concurrent flush", guide, StringComparison.Ordinal);
		Assert.Contains("without restarting the Runtime Host", guide, StringComparison.Ordinal);
		Assert.Contains("environment-sensitive hosted characterization only", guide, StringComparison.Ordinal);
		Assert.DoesNotContain("OBSERVABILITY: 2.1 FAILURE-ISOLATION", guide, StringComparison.Ordinal);
		Assert.Contains("OBSERVABILITY: 2.2-2.3 DATA-BOUNDARIES", guide, StringComparison.Ordinal);
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

		Assert.Contains(root.GetProperty("sources")[0].GetProperty("modifiers").EnumerateArray(), modifier =>
																									  modifier.TryGetProperty("condition", out var condition) &&
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
		Assert.Contains("low-cardinality, non-secret operational metadata", guide, StringComparison.Ordinal);
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