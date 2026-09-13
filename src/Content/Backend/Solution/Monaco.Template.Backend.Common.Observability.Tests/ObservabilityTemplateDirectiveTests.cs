using AwesomeAssertions;

namespace Monaco.Template.Backend.Common.Observability.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Application Services", "Observability")]
public sealed class ObservabilityTemplateDirectiveTests
{
	private static readonly string[] ProducerFiles =
	[
		"Monaco.Template.Backend.Common.Observability.Tests/ObservabilityPackageGraphTests.cs",
		"Monaco.Template.Backend.Common.Observability.Tests/ObservabilityTemplateDirectiveTests.cs",
		"Monaco.Template.Backend.Common.Observability.Tests/ObservabilityGuideTokenTests.cs",
		"Monaco.Template.Backend.Common.Observability.Tests/ParsedSolutionXml.cs",
		"Monaco.Template.Backend.Common.Observability.Tests/TemplateDirectiveSource.cs"
	];

	[Fact(DisplayName = "API and Worker tracing and metrics gate native MassTransit on massTransitIntegration")]
	public void ApiAndWorkerMassTransitSubscriptionsAreGatedByMassTransitIntegration() =>
		TemplateDirectiveSource.AssertGatedInvocations(Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityHostBuilderExtensions.cs"),
													   "massTransitIntegration",
													   [
														   "builder.AddSource(\"MassTransit\")", "builder.AddMeter(\"MassTransit\")"
													   ],
													   [
														   "Api", "Worker"
													   ],
													   [
														   "Gateway"
													   ]);

	[Fact(DisplayName = "Producer observability oracles are unconditionally excluded without excluding the test project")]
	public void ProducerObservabilityOraclesAreUnconditionallyExcludedWithoutExcludingTheTestProject()
	{
		var unconditional = TemplateDirectiveSource.UnconditionalExcludes();
		unconditional.Should()
					 .Contain(ProducerFiles);
		unconditional.Should()
					 .NotContain(path => path.Contains("Monaco.Template.Backend.Common.Observability.Tests/**/*", StringComparison.Ordinal) ||
										 path.Equals("Monaco.Template.Backend.Common.Observability.Tests/", StringComparison.Ordinal));
		unconditional.Should()
					 .NotContain("Monaco.Template.Backend.ArchitectureTests/ObservabilityPackageGraphTests.cs");
		unconditional.Should()
					 .NotContain("Monaco.Template.Backend.ArchitectureTests/ObservabilityResourceTests.cs");
		unconditional.Should()
					 .NotContain("Monaco.Template.Backend.ArchitectureTests/ObservabilityConfigurationTests.cs");

		var commonLibraries = TemplateDirectiveSource.ConditionalExcludes("(!commonLibraries)");
		commonLibraries.Should()
					   .Contain("Monaco.Template.Backend.Common.Observability.Tests/**/*");
		commonLibraries.Should()
					   .Contain("Monaco.Template.Backend.Common.Observability/**/*");

		var noHost = TemplateDirectiveSource.ConditionalExcludes("(!commonLibraries || (!apiService && !workerService && !apiGateway))");
		noHost.Should()
			  .Contain("Monaco.Template.Backend.Common.Observability.Tests/**/*");
		noHost.Should()
			  .Contain("Monaco.Template.Backend.Common.Observability/**/*");
		noHost.Should()
			  .NotContain("Monaco.Template.Backend.ArchitectureTests/ObservabilityResourceTests.cs");
		noHost.Should()
			  .NotContain("Monaco.Template.Backend.ArchitectureTests/ObservabilityConfigurationTests.cs");

		TemplateDirectiveSource.HasConditionalModifier("(!commonLibraries || !apiService || !workerService || !apiGateway)")
							   .Should()
							   .BeFalse();
	}

	[Fact(DisplayName = "Template separates Common delivery from host observability")]
	public void TemplateSeparatesCommonDeliveryFromHostObservability()
	{
		using var document = TemplateDirectiveSource.TemplateDocument();
		var template = document.RootElement.GetRawText();

		template.Should()
				.Contain("\"commonLibraries\"");
		template.Should()
				.Contain("\"datatype\": \"bool\"");
		template.Should()
				.Contain("\"defaultValue\": \"true\"");
		TemplateDirectiveSource.HasConditionalModifier("(!commonLibraries)")
							   .Should()
							   .BeTrue();
		TemplateDirectiveSource.HasConditionalModifier("(!commonLibraries || (!apiService && !workerService && !apiGateway))")
							   .Should()
							   .BeTrue();
		TemplateDirectiveSource.HasConditionalModifier("(!apiService && !apiGateway)")
							   .Should()
							   .BeTrue();
		template.Should()
				.NotContain("Monaco.Template.Backend.*.Api*/**/*");

		foreach (var hostProject in new[] { "Monaco.Template.Backend.Api", "Monaco.Template.Backend.Worker", "Monaco.Template.Backend.Common.ApiGateway" })
			TemplateDirectiveSource.AssertDoesNotContainDirective(Path.Combine(hostProject, "Program.cs"), "commonLibraries");
	}

	[Fact(DisplayName = "Template postAction and no-host exclude keep the observability guide discoverable")]
	public void TemplatePostActionAndNoHostExcludeKeepTheObservabilityGuideDiscoverable()
	{
		using var document = TemplateDirectiveSource.TemplateDocument();
		var root = document.RootElement;

		Assert.Contains(root.GetProperty("sources")[0]
							.GetProperty("modifiers")
							.EnumerateArray(),
						modifier => modifier.TryGetProperty("condition", out var condition) &&
									condition.GetString() == "(!apiService && !workerService && !apiGateway)" &&
									modifier.GetProperty("exclude")
											.EnumerateArray()
											.Any(excluded => excluded.GetString() == "OBSERVABILITY.md"));

		var postAction = Assert.Single(root.GetProperty("postActions")
										   .EnumerateArray());
		Assert.Equal("AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C",
					 postAction.GetProperty("actionId")
							   .GetString());
		Assert.Equal("(apiService || workerService || apiGateway)",
					 postAction.GetProperty("condition")
							   .GetString());

		var primaryOutput = Assert.Single(root.GetProperty("primaryOutputs")
											  .EnumerateArray());
		Assert.Equal("Monaco.Template.Backend.slnx",
					 primaryOutput.GetProperty("path")
								  .GetString());

		TemplateDirectiveSource.AssertPathGatedBy("Monaco.Template.Backend.slnx",
												  "apiService || workerService || apiGateway",
												  "<File Path=\"OBSERVABILITY.md\" />");
	}

	[Fact(DisplayName = "Generated Observability.Tests sits under Common/Tests with the library host gate")]
	public void GeneratedObservabilityTestsSitsUnderCommonTestsWithTheLibraryHostGate()
	{
		TemplateDirectiveSource.AssertPathGatedBy("Monaco.Template.Backend.slnx",
												  "apiService || workerService || apiGateway",
												  "Monaco.Template.Backend.Common.Observability.Tests/Monaco.Template.Backend.Common.Observability.Tests.csproj");

		var slnx = TemplateDirectiveSource.Read("Monaco.Template.Backend.slnx");
		var testsFolder = slnx.IndexOf("<Folder Name=\"/Common/Tests/\">", StringComparison.Ordinal);
		Assert.True(testsFolder >= 0, "slnx must contain /Common/Tests/.");
		var testsProject = slnx.IndexOf("Monaco.Template.Backend.Common.Observability.Tests/Monaco.Template.Backend.Common.Observability.Tests.csproj", StringComparison.Ordinal);
		var testsFolderEnd = slnx.IndexOf("</Folder>", testsFolder, StringComparison.Ordinal);
		Assert.True(testsProject > testsFolder && testsProject < testsFolderEnd,
					"Observability.Tests must be a member of /Common/Tests/.");
	}

	[Fact(DisplayName = "Hosts select one applicable profile and HTTP hosts install the shared boundary handler")]
	public void HostsSelectOneApplicableProfileAndHttpHostsInstallTheSharedBoundaryHandler()
	{
		AssertHostProfile("Monaco.Template.Backend.Api", "AddApiObservability()");
		AssertHostProfile("Monaco.Template.Backend.Worker", "AddWorkerObservability()");
		AssertHostProfile("Monaco.Template.Backend.Common.ApiGateway", "AddGatewayObservability()");

		var apiProgram = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Api", "Program.cs"));
		var gatewayProgram = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Common.ApiGateway", "Program.cs"));
		var workerProgram = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Worker", "Program.cs"));

		apiProgram.Should()
				  .Contain("AddExceptionHandler<BoundaryExceptionHandler>()");
		gatewayProgram.Should()
					  .Contain("AddExceptionHandler<BoundaryExceptionHandler>()");
		apiProgram.Should()
				  .Contain("app.UseExceptionHandler()");
		gatewayProgram.Should()
					  .Contain("app.UseExceptionHandler()");
		apiProgram.Should()
				  .NotContain("UseDeveloperExceptionPage");
		gatewayProgram.Should()
					  .NotContain("UseDeveloperExceptionPage");
		workerProgram.Should()
					 .NotContain("BoundaryExceptionHandler");
		gatewayProgram.Should()
					  .NotContain("MassTransit");
	}

	[Fact(DisplayName = "API and Gateway Program.cs wire identity enrichment between authentication and authorization")]
	public void ApiAndGatewayProgramWireIdentityEnrichmentBetweenAuthenticationAndAuthorization()
	{
		var apiProgram = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Api", "Program.cs"));
		var gatewayProgram = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Common.ApiGateway", "Program.cs"));
		var workerProgram = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Worker", "Program.cs"));

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

	[Fact(DisplayName = "Application owns the BCL successful-company counter without host or OpenTelemetry coupling")]
	public void ApplicationOwnsBclSuccessfulCompanyCounterWithoutHostOrOpenTelemetryCoupling()
	{
		var diagnostics = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Application", "Diagnostics", "ApplicationDiagnostics.cs"));
		var createCompany = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Application", "Features", "Company", "CreateCompany.cs"));

		Assert.Contains("using System.Diagnostics.Metrics;", diagnostics, StringComparison.Ordinal);
		Assert.Contains("internal const string MeterName = \"Monaco.Template.Backend.Application\"", diagnostics, StringComparison.Ordinal);
		Assert.Contains("private static readonly Meter Meter = new(MeterName)", diagnostics, StringComparison.Ordinal);
		Assert.Contains("Counter<long>", diagnostics, StringComparison.Ordinal);
		Assert.Contains("company.successful_creations", diagnostics, StringComparison.Ordinal);
		Assert.Contains("{company}", diagnostics, StringComparison.Ordinal);
		Assert.DoesNotContain("OpenTelemetry", diagnostics, StringComparison.Ordinal);
		Assert.DoesNotContain("Common.Observability", diagnostics, StringComparison.Ordinal);
		Assert.Contains("ApplicationDiagnostics.SuccessfulCompanyCreations.Add(1);", createCompany, StringComparison.Ordinal);
		TemplateDirectiveSource.AssertDoesNotContainDirective(Path.Combine("Monaco.Template.Backend.Application", "Features", "Company", "CreateCompany.cs"), "apiService");
		TemplateDirectiveSource.UnconditionalExcludes()
							   .Should()
							   .NotContain("Monaco.Template.Backend.Application/Diagnostics/ApplicationDiagnostics.cs");
		TemplateDirectiveSource.ConditionalExcludes("(!commonLibraries)")
							   .Should()
							   .NotContain("Monaco.Template.Backend.Application/Diagnostics/ApplicationDiagnostics.cs");
	}

	[Fact(DisplayName = "Directory.Packages.props gates Common package versions on !commonLibraries")]
	public void DirectoryPackagesPropsGatesCommonPackageVersionsOnNotCommonLibraries() =>
		TemplateDirectiveSource.AssertPathGatedBy("Directory.Packages.props",
												  "!commonLibraries",
												  "Monaco.Template.Backend.Common.Observability");

	[Fact(DisplayName = "Shared composition source keeps unified OTLP and HTTP identity boundary tokens")]
	public void SharedCompositionSourceKeepsUnifiedOtlpAndHttpIdentityBoundaryTokens()
	{
		var profiles = Path.Combine("Monaco.Template.Backend.Common.Observability", "ObservabilityHostBuilderExtensions.cs");
		var source = TemplateDirectiveSource.Read(profiles);

		Assert.Equal(1,
					 source.Split("UseOtlpExporter", StringSplitOptions.None)
						   .Length -
					 1);
		TemplateDirectiveSource.AssertDoesNotContainDirective(profiles, "auth");
		Assert.DoesNotContain("AddMeter(\"Microsoft.AspNetCore.Authentication\")", source, StringComparison.Ordinal);
		Assert.DoesNotContain("AddMeter(\"Microsoft.AspNetCore.Authorization\")", source, StringComparison.Ordinal);

		var middleware = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Common.Observability", "IdentityEnrichmentMiddleware.cs"));
		foreach (var token in new[] { "Request.Headers", "Cookies", "Query", "Body", "Authorization", "Bearer", "Jwt", "JWT" })
			Assert.DoesNotContain(token, middleware, StringComparison.Ordinal);

		var handler = TemplateDirectiveSource.Read(Path.Combine("Monaco.Template.Backend.Common.Observability", "BoundaryExceptionHandler.cs"));
		Assert.Contains("LogError(exception, \"Unhandled HTTP request failure.\")", handler, StringComparison.Ordinal);
		Assert.Contains("ValueTask.FromResult(true)", handler, StringComparison.Ordinal);
	}

	[Fact(DisplayName = "Host and application surfaces omit telemetry governance tokens")]
	public void HostAndApplicationSurfacesOmitTelemetryGovernanceTokens()
	{
		string[] surfaces =
		[
			Path.Combine("Monaco.Template.Backend.Application", "DependencyInjection", "ApplicationOptions.cs"),
			Path.Combine("Monaco.Template.Backend.Application", "DependencyInjection", "ServiceCollectionExtensions.cs"),
			Path.Combine("Monaco.Template.Backend.Api", "Program.cs"),
			Path.Combine("Monaco.Template.Backend.Worker", "Program.cs"),
			Path.Combine("Monaco.Template.Backend.Common.ApiGateway", "Program.cs"),
			Path.Combine("Monaco.Template.Backend.Api", "appsettings.Development.json"),
			Path.Combine("Monaco.Template.Backend.Worker", "appsettings.Development.json"),
			Path.Combine("Monaco.Template.Backend.Common.ApiGateway", "appsettings.Development.json")
		];

		foreach (var relativePath in surfaces)
		{
			var source = TemplateDirectiveSource.Read(relativePath);
			source.Contains("AddView", StringComparison.Ordinal)
				  .Should()
				  .BeFalse();
			source.Contains("MetricStreamConfiguration", StringComparison.Ordinal)
				  .Should()
				  .BeFalse();
			source.Contains("Cardinality", StringComparison.OrdinalIgnoreCase)
				  .Should()
				  .BeFalse();
			source.Contains("FlushTimeout", StringComparison.Ordinal)
				  .Should()
				  .BeFalse();
			source.Contains("Preflight", StringComparison.OrdinalIgnoreCase)
				  .Should()
				  .BeFalse();
			source.Contains("EnableSensitiveDataLogging", StringComparison.Ordinal)
				  .Should()
				  .BeFalse();
			source.Contains("EnableEFSensitiveLogging", StringComparison.Ordinal)
				  .Should()
				  .BeFalse();
		}
	}

	private static void AssertHostProfile(string hostProject, string profileMethod)
	{
		var program = TemplateDirectiveSource.Read(Path.Combine(hostProject, "Program.cs"));
		Assert.Equal(1, program.Split(profileMethod).Length - 1);
	}
}