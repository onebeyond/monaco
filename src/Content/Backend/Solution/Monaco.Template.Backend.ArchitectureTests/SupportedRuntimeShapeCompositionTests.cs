using System.Diagnostics;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Observability")]
public sealed class SupportedRuntimeShapeCompositionTests : IClassFixture<SupportedRuntimeShapeCompositionTests.RuntimeShapeHiveFixture>
{
	private static readonly Regex ProjectReferenceInclude = new(@"<ProjectReference\s+Include=""([^""]+)""",
																RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex PackageReferenceInclude = new(@"<PackageReference\s+Include=""([^""]+)""",
																RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private readonly RuntimeShapeHiveFixture _hive;

	public SupportedRuntimeShapeCompositionTests(RuntimeShapeHiveFixture hive) =>
		_hive = hive;

	[Fact(DisplayName = "COMP-01 R1 project/package graph")]
	public void COMP01_R1_ProjectPackageGraph() =>
		AssertProjectPackageGraph(_hive.R1);

	[Fact(DisplayName = "COMP-01 R2 project/package graph")]
	public void COMP01_R2_ProjectPackageGraph() =>
		AssertProjectPackageGraph(_hive.R2);

	[Fact(DisplayName = "COMP-01 R3 project/package graph")]
	public void COMP01_R3_ProjectPackageGraph() =>
		AssertProjectPackageGraph(_hive.R3);

	[Fact(DisplayName = "COMP-01 R4 project/package graph")]
	public void COMP01_R4_ProjectPackageGraph() =>
		AssertProjectPackageGraph(_hive.R4);

	[Fact(DisplayName = "COMP-01 R5 project/package graph")]
	public void COMP01_R5_ProjectPackageGraph() =>
		AssertProjectPackageGraph(_hive.R5);

	[Fact(DisplayName = "COMP-01 R6 project/package graph")]
	public void COMP01_R6_ProjectPackageGraph() =>
		AssertProjectPackageGraph(_hive.R6);

	[Fact(DisplayName = "COMP-01 R7 project/package graph")]
	public void COMP01_R7_ProjectPackageGraph() =>
		AssertProjectPackageGraph(_hive.R7);

	[Fact(DisplayName = "COMP-02 R1 host observability composition")]
	public void COMP02_R1_HostObservability() =>
		AssertHostObservability(_hive.R1);

	[Fact(DisplayName = "COMP-02 R2 host observability composition")]
	public void COMP02_R2_HostObservability() =>
		AssertHostObservability(_hive.R2);

	[Fact(DisplayName = "COMP-02 R3 host observability composition")]
	public void COMP02_R3_HostObservability() =>
		AssertHostObservability(_hive.R3);

	[Fact(DisplayName = "COMP-02 R4 host observability composition")]
	public void COMP02_R4_HostObservability() =>
		AssertHostObservability(_hive.R4);

	[Fact(DisplayName = "COMP-02 R5 host observability composition")]
	public void COMP02_R5_HostObservability() =>
		AssertHostObservability(_hive.R5);

	[Fact(DisplayName = "COMP-02 R6 host observability composition")]
	public void COMP02_R6_HostObservability() =>
		AssertHostObservability(_hive.R6);

	[Fact(DisplayName = "COMP-02 R7 host observability composition")]
	public void COMP02_R7_HostObservability() =>
		AssertHostObservability(_hive.R7);

	[Fact(DisplayName = "COMP-03 R1 persistence attribution composition")]
	public void COMP03_R1_PersistenceComposition() =>
		AssertPersistenceComposition(_hive.R1);

	[Fact(DisplayName = "COMP-03 R2 persistence attribution composition")]
	public void COMP03_R2_PersistenceComposition() =>
		AssertPersistenceComposition(_hive.R2);

	[Fact(DisplayName = "COMP-03 R3 persistence attribution composition")]
	public void COMP03_R3_PersistenceComposition() =>
		AssertPersistenceComposition(_hive.R3);

	[Fact(DisplayName = "COMP-03 R4 persistence attribution composition")]
	public void COMP03_R4_PersistenceComposition() =>
		AssertPersistenceComposition(_hive.R4);

	[Fact(DisplayName = "COMP-03 R5 persistence attribution composition")]
	public void COMP03_R5_PersistenceComposition() =>
		AssertPersistenceComposition(_hive.R5);

	[Fact(DisplayName = "COMP-03 R6 persistence attribution composition")]
	public void COMP03_R6_PersistenceComposition() =>
		AssertPersistenceComposition(_hive.R6);

	[Fact(DisplayName = "COMP-03 R7 persistence attribution composition")]
	public void COMP03_R7_PersistenceComposition() =>
		AssertPersistenceComposition(_hive.R7);

	[Fact(DisplayName = "COMP-04 messaging removed")]
	public void COMP04_MessagingRemoved()
	{
		foreach (var shape in new[] { _hive.R3, _hive.R6 })
		{
			shape.ProjectDirectoryExists("Messages")
				 .Should()
				 .BeFalse(shape.Id);
			AssertSolutionMembership(shape, "Messages", present: false);
			shape.PackageVersions
				 .Should()
				 .NotContain(id => id.Equals("MassTransit", StringComparison.Ordinal) || id.StartsWith("MassTransit.", StringComparison.Ordinal), Because(shape));
			shape.FileExists("Messages", "V1", "ProductCreated.cs")
				 .Should()
				 .BeFalse(shape.Id);
			Directory.Exists(Path.Combine(shape.OutputDirectory, $"{shape.Name}.Worker", "Consumers"))
					 .Should()
					 .BeFalse(shape.Id);
			shape.FileExists("Application", "Features", "Product", "LongRunningProcess.cs")
				 .Should()
				 .BeFalse(shape.Id);
			shape.FileExists("IntegrationTests", "Tests", "ProductMessagingCorrelationTests.cs")
				 .Should()
				 .BeFalse(shape.Id);

			var observability = shape.ReadText("Common.Observability", "ObservabilityHostBuilderExtensions.cs");
			CSharpCompositionSyntax.InvokesWithStringArgument(observability, "AddSource", "MassTransit")
								   .Should()
								   .BeFalse(Because(shape));
			CSharpCompositionSyntax.InvokesWithStringArgument(observability, "AddMeter", "MassTransit")
								   .Should()
								   .BeFalse(Because(shape));

			shape.Guide
				 .Should()
				 .NotContain("ProductCreated", Because(shape));

			if (shape.Options.Api)
			{
				var apiProgram = shape.ReadText("Api", "Program.cs");
				CSharpCompositionSyntax.Invokes(apiProgram, "AddMassTransit")
									   .Should()
									   .BeFalse(Because(shape));
				shape.ReadText("Api", "appsettings.json")
					 .Should()
					 .NotContain("MessageBus", Because(shape));
				PackageReferences(shape.ReadText("Api", $"{shape.Name}.Api.csproj"))
					.Should()
					.NotContain(name => name.StartsWith("MassTransit", StringComparison.Ordinal), Because(shape));
			}

			if (shape.Options.Worker)
			{
				var workerProgram = shape.ReadText("Worker", "Program.cs");
				CSharpCompositionSyntax.Invokes(workerProgram, "AddMassTransit")
									   .Should()
									   .BeFalse(Because(shape));
				shape.ReadText("Worker", "appsettings.json")
					 .Should()
					 .NotContain("MessageBus", Because(shape));
				var workerCsproj = shape.ReadText("Worker", $"{shape.Name}.Worker.csproj");
				PackageReferences(workerCsproj)
					.Should()
					.NotContain(name => name.StartsWith("MassTransit", StringComparison.Ordinal), Because(shape));
				ProjectReferences(workerCsproj)
					.Should()
					.NotContain(path => path.Contains(".Messages", StringComparison.Ordinal), Because(shape));
			}

			var applicationCsproj = shape.ReadText("Application", $"{shape.Name}.Application.csproj");
			PackageReferences(applicationCsproj)
				.Should()
				.NotContain(name => name.StartsWith("MassTransit", StringComparison.Ordinal), Because(shape));
			ProjectReferences(applicationCsproj)
				.Should()
				.NotContain(path => path.Contains(".Messages", StringComparison.Ordinal), Because(shape));
		}
	}

	[Fact(DisplayName = "COMP-04 auth removed")]
	public void COMP04_AuthRemoved()
	{
		foreach (var shape in new[] { _hive.R3, _hive.R4, _hive.R5, _hive.R6 })
		{
			shape.PackageVersions
				 .Should()
				 .NotContain("Keycloak.Net.Core", Because(shape));
			shape.PackageVersions
				 .Should()
				 .NotContain("Testcontainers.Keycloak", Because(shape));
			shape.FileExists("realm-export-template.json")
				 .Should()
				 .BeFalse(shape.Id);
			shape.Guide
				 .Should()
				 .NotContain("## Authenticated request identity", Because(shape));
			Directory.Exists(Path.Combine(shape.OutputDirectory, $"{shape.Name}.IntegrationTests", "Auth"))
					 .Should()
					 .BeFalse(shape.Id);
			shape.FileExists("IntegrationTests", "Auth", "KeycloakService.cs")
				 .Should()
				 .BeFalse(shape.Id);

			if (shape.Options.Api)
			{
				Directory.Exists(Path.Combine(shape.OutputDirectory, $"{shape.Name}.Api", "Auth"))
						 .Should()
						 .BeFalse(shape.Id);
				var apiProgram = shape.ReadText("Api", "Program.cs");
				CSharpCompositionSyntax.Invokes(apiProgram, "AddJwtBearerAuthentication")
									   .Should()
									   .BeFalse(Because(shape));
				CSharpCompositionSyntax.Invokes(apiProgram, "AddAuthorizationWithPolicies")
									   .Should()
									   .BeFalse(Because(shape));
				var apiSettings = shape.ReadText("Api", "appsettings.json");
				apiSettings.Should()
						   .NotContain("\"SSO\"", Because(shape));
				apiSettings.Should()
						   .NotContain("AuthEndpoint", Because(shape));
				apiSettings.Should()
						   .NotContain("TokenEndpoint", Because(shape));
			}

			if (shape.Options.Gateway)
			{
				var gatewayProgram = shape.ReadText("Common.ApiGateway", "Program.cs");
				CSharpCompositionSyntax.Invokes(gatewayProgram, "AddJwtBearerAuthentication")
									   .Should()
									   .BeFalse(Because(shape));
				shape.ReadText("Common.ApiGateway", "appsettings.json")
					 .Should()
					 .NotContain("\"SSO\"", Because(shape));
			}
		}
	}

	[Fact(DisplayName = "COMP-04 files removed")]
	public void COMP04_FilesRemoved()
	{
		foreach (var shape in new[] { _hive.R3, _hive.R6 })
		{
			shape.ProjectDirectoryExists("Common.BlobStorage")
				 .Should()
				 .BeFalse(shape.Id);
			AssertSolutionMembership(shape, "Common.BlobStorage", present: false);
			shape.PackageVersions
				 .Should()
				 .NotContain("Azure.Storage.Blobs", Because(shape));
			shape.PackageVersions
				 .Should()
				 .NotContain("Testcontainers.Azurite", Because(shape));
			shape.FileExists("Domain", "Model", "Entities", "Product.cs")
				 .Should()
				 .BeFalse(shape.Id);
			shape.FileExists("Application", "Persistence", "EntityConfigurations", "ProductEntityConfiguration.cs")
				 .Should()
				 .BeFalse(shape.Id);
			shape.FileExists("Application", "Persistence", "EntityConfigurations", "FileEntityConfiguration.cs")
				 .Should()
				 .BeFalse(shape.Id);
			Directory.Exists(Path.Combine(shape.OutputDirectory, $"{shape.Name}.Application", "Features", "File"))
					 .Should()
					 .BeFalse(shape.Id);
			Directory.Exists(Path.Combine(shape.OutputDirectory, $"{shape.Name}.Application", "Features", "Product"))
					 .Should()
					 .BeFalse(shape.Id);
			shape.FileExists("IntegrationTests", "Tests", "FilesTests.cs")
				 .Should()
				 .BeFalse(shape.Id);
			shape.FileExists("IntegrationTests", "Apis", "IFilesApi.cs")
				 .Should()
				 .BeFalse(shape.Id);

			var applicationCsproj = shape.ReadText("Application", $"{shape.Name}.Application.csproj");
			ProjectReferences(applicationCsproj)
				.Should()
				.NotContain(path => path.Contains("BlobStorage", StringComparison.Ordinal), Because(shape));

			if (shape.Options.Api)
			{
				shape.FileExists("Api", "Endpoints", "Files.cs")
					 .Should()
					 .BeFalse(shape.Id);
				shape.FileExists("Api", "Endpoints", "Products.cs")
					 .Should()
					 .BeFalse(shape.Id);
				var apiProgram = shape.ReadText("Api", "Program.cs");
				CSharpCompositionSyntax.ContainsIdentifierFragment(apiProgram, "BlobStorage")
									   .Should()
									   .BeFalse(Because(shape));
				shape.ReadText("Api", "appsettings.json")
					 .Should()
					 .NotContain("BlobStorage", Because(shape));
			}

			if (shape.Options.Worker)
			{
				var workerProgram = shape.ReadText("Worker", "Program.cs");
				CSharpCompositionSyntax.ContainsIdentifierFragment(workerProgram, "BlobStorage")
									   .Should()
									   .BeFalse(Because(shape));
				shape.ReadText("Worker", "appsettings.json")
					 .Should()
					 .NotContain("BlobStorage", Because(shape));
			}
		}
	}

	[Fact(DisplayName = "COMP-04 host removed")]
	public void COMP04_HostRemoved()
	{
		AssertAbsentHost(_hive.R1, "Common.ApiGateway", "### Gateway profile");
		_hive.R1
			 .Guide
			 .Should()
			 .NotContain("Gateway has no SQL instrumentation.");

		AssertAbsentHost(_hive.R3, "Worker", "### Worker profile");
		_hive.R3
			 .Guide
			 .Should()
			 .NotContain("## Worker-only solutions");

		AssertAbsentHost(_hive.R4, "Worker", "### Worker profile");
		_hive.R4
			 .FileExists("Worker", "Properties", "launchSettings.json")
			 .Should()
			 .BeFalse();
		_hive.R4
			 .FileExists("IntegrationTests", "Factories", "WorkerServiceFactory.cs")
			 .Should()
			 .BeFalse();

		AssertAbsentHost(_hive.R5, "Api", "### API profile");
		_hive.R5
			 .Guide
			 .Should()
			 .NotContain("## Successful company creations");
		_hive.R5
			 .Guide
			 .Should()
			 .NotContain("## Create a Company and inspect local observability");

		AssertAbsentHost(_hive.R6, "Api", "### API profile");
		_hive.R6
			 .FileExists("Api", "Properties", "launchSettings.json")
			 .Should()
			 .BeFalse();
		_hive.R6
			 .Guide
			 .Should()
			 .NotContain("## Successful company creations");

		AssertAbsentHost(_hive.R7, "Common.ApiGateway", "### Gateway profile");
		_hive.R7
			 .Guide
			 .Should()
			 .NotContain("Gateway has no SQL instrumentation.");
	}

	[Fact(DisplayName = "COMP-04 tests removed")]
	public void COMP04_TestsRemoved()
	{
		foreach (var shape in new[] { _hive.R5, _hive.R6, _hive.R7 })
		{
			shape.Solution
				 .Should()
				 .NotContain("/Tests/", Because(shape));
			shape.ProjectDirectoryExists("Application.Tests")
				 .Should()
				 .BeFalse(shape.Id);
			shape.ProjectDirectoryExists("ArchitectureTests")
				 .Should()
				 .BeFalse(shape.Id);
			shape.ProjectDirectoryExists("Domain.Tests")
				 .Should()
				 .BeFalse(shape.Id);
			shape.ProjectDirectoryExists("IntegrationTests")
				 .Should()
				 .BeFalse(shape.Id);
			shape.ProjectDirectoryExists("Common.Observability.Tests")
				 .Should()
				 .BeFalse(shape.Id);
			shape.ProjectDirectoryExists("Common.Domain.Tests")
				 .Should()
				 .BeFalse(shape.Id);
			shape.ProjectDirectoryExists("Common.Infrastructure.Tests")
				 .Should()
				 .BeFalse(shape.Id);
			shape.ProjectDirectoryExists("Common.Tests")
				 .Should()
				 .BeFalse(shape.Id);
			shape.ProjectDirectoryExists("Common.BlobStorage.Tests")
				 .Should()
				 .BeFalse(shape.Id);
			AssertSolutionMembership(shape, "ArchitectureTests", present: false);
			AssertSolutionMembership(shape, "Domain.Tests", present: false);
			shape.PackageVersions
				 .Should()
				 .NotContain(id => id.Equals("xunit", StringComparison.Ordinal) || id.StartsWith("xunit.", StringComparison.Ordinal), Because(shape));
			shape.PackageVersions
				 .Should()
				 .NotContain("Microsoft.NET.Test.Sdk", Because(shape));
		}
	}

	[Fact(DisplayName = "COMP-05 R1 owns complete API-to-Worker journey")]
	public void COMP05_R1_OwnsCompleteJourney()
	{
		var r1 = _hive.R1;

		r1.FileExists("Messages", "V1", "ProductCreated.cs")
		  .Should()
		  .BeTrue();
		r1.FileExists("Worker", "Consumers", "OnProductCreatedThenLongRunningProcess.cs")
		  .Should()
		  .BeTrue();
		r1.FileExists("Application", "Features", "Product", "LongRunningProcess.cs")
		  .Should()
		  .BeTrue();
		r1.FileExists("IntegrationTests", "Tests", "ProductMessagingCorrelationTests.cs")
		  .Should()
		  .BeTrue();

		r1.Guide
		  .Should()
		  .Contain("## Create a Company and inspect local observability");
		r1.Guide
		  .Should()
		  .Contain("company.successful_creations");
		r1.Guide
		  .Should()
		  .Contain("## High-fidelity telemetry boundary");
		r1.Guide
		  .Should()
		  .Contain("## Successful company creations");
		r1.Guide
		  .Should()
		  .NotContain("## Worker-only solutions");
	}

	[Fact(DisplayName = "COMP-05 reduced profiles add no observability-only sample message")]
	public void COMP05_ReducedProfiles_NoObservabilityOnlySampleMessage()
	{
		AssertNoObservabilityOnlySampleMessage(_hive.R1);
		AssertNoObservabilityOnlySampleMessage(_hive.R2);

		foreach (var shape in new[] { _hive.R3, _hive.R4, _hive.R5, _hive.R6, _hive.R7 })
		{
			AssertNoObservabilityOnlySampleMessage(shape);

			shape.FileExists("IntegrationTests", "Tests", "ProductMessagingCorrelationTests.cs")
				 .Should()
				 .BeFalse($"{shape.Id} must not carry the complete R1 messaging correlation journey");
		}

		_hive.R3
			 .FileExists("Messages", "V1", "ProductCreated.cs")
			 .Should()
			 .BeFalse();
		_hive.R3
			 .Guide
			 .Should()
			 .NotContain("## Create a Company and inspect local observability");

		_hive.R4
			 .FileExists("Messages", "V1", "ProductCreated.cs")
			 .Should()
			 .BeTrue();
		_hive.R4
			 .FileExists("Worker", "Consumers", "OnProductCreatedThenLongRunningProcess.cs")
			 .Should()
			 .BeFalse();
		_hive.R4
			 .FileExists("Application", "Features", "Product", "LongRunningProcess.cs")
			 .Should()
			 .BeFalse();
		_hive.R4
			 .Guide
			 .Should()
			 .NotContain("## Create a Company and inspect local observability");

		_hive.R5
			 .FileExists("Messages", "V1", "ProductCreated.cs")
			 .Should()
			 .BeTrue();
		_hive.R5
			 .FileExists("Worker", "Consumers", "OnProductCreatedThenLongRunningProcess.cs")
			 .Should()
			 .BeTrue();
		_hive.R5
			 .Guide
			 .Should()
			 .Contain("## Worker-only solutions");
		_hive.R5
			 .Guide
			 .Should()
			 .NotContain("## Create a Company and inspect local observability");
		_hive.R5
			 .Guide
			 .Should()
			 .NotContain("company.successful_creations");

		_hive.R6
			 .FileExists("Messages", "V1", "ProductCreated.cs")
			 .Should()
			 .BeFalse();
		_hive.R6
			 .Guide
			 .Should()
			 .Contain("## Worker-only solutions");
		_hive.R6
			 .Guide
			 .Should()
			 .NotContain("## Create a Company and inspect local observability");

		_hive.R7
			 .FileExists("Messages", "V1", "ProductCreated.cs")
			 .Should()
			 .BeTrue();
		_hive.R7
			 .FileExists("Worker", "Consumers", "OnProductCreatedThenLongRunningProcess.cs")
			 .Should()
			 .BeTrue();
		_hive.R7
			 .FileExists("Application", "Features", "Product", "LongRunningProcess.cs")
			 .Should()
			 .BeTrue();
		_hive.R7
			 .Guide
			 .Should()
			 .Contain("## Create a Company and inspect local observability");
		_hive.R7
			 .Guide
			 .Should()
			 .NotContain("## Worker-only solutions");
	}

	private static void AssertProjectPackageGraph(GeneratedRuntimeShape shape)
	{
		var options = shape.Options;
		options.Common
			   .Should()
			   .BeTrue($"{shape.Id}: R1–R7 keep commonLibraries");

		shape.ProjectDirectoryExists("Application")
			 .Should()
			 .BeTrue(shape.Id);
		shape.ProjectDirectoryExists("Domain")
			 .Should()
			 .BeTrue(shape.Id);
		shape.ProjectDirectoryExists("Common.Observability")
			 .Should()
			 .BeTrue(shape.Id);
		shape.ProjectDirectoryExists("Common.Application")
			 .Should()
			 .BeTrue(shape.Id);
		shape.ProjectDirectoryExists("Common.Domain")
			 .Should()
			 .BeTrue(shape.Id);
		shape.ProjectDirectoryExists("Common.Infrastructure")
			 .Should()
			 .BeTrue(shape.Id);

		AssertSolutionMembership(shape, "Application", present: true);
		AssertSolutionMembership(shape, "Domain", present: true);
		AssertSolutionMembership(shape, "Common.Observability", present: true);
		AssertSolutionMembership(shape, "Common.Application", present: true);

		AssertProjectPresence(shape, "Api", options.Api);
		AssertProjectPresence(shape, "Worker", options.Worker);
		AssertProjectPresence(shape, "Common.ApiGateway", options.Gateway);
		AssertProjectPresence(shape, "Messages", options.MassTransit);
		AssertProjectPresence(shape, "Common.BlobStorage", options.Files);
		AssertProjectPresence(shape, "Common.Api", options.Api || options.Gateway);
		AssertProjectPresence(shape, "Common.Api.Application", options.Api);

		AssertSolutionMembership(shape, "Api", options.Api);
		AssertSolutionMembership(shape, "Worker", options.Worker);
		AssertSolutionMembership(shape, "Common.ApiGateway", options.Gateway);
		AssertSolutionMembership(shape, "Messages", options.MassTransit);
		AssertSolutionMembership(shape, "Common.BlobStorage", options.Files);
		AssertSolutionMembership(shape, "Common.Api", options.Api || options.Gateway);
		AssertSolutionMembership(shape, "Common.Api.Application", options.Api);

		if (options.Tests)
		{
			shape.ProjectDirectoryExists("Application.Tests")
				 .Should()
				 .BeTrue(shape.Id);
			shape.ProjectDirectoryExists("ArchitectureTests")
				 .Should()
				 .BeTrue(shape.Id);
			shape.ProjectDirectoryExists("Domain.Tests")
				 .Should()
				 .BeTrue(shape.Id);
			shape.ProjectDirectoryExists("Common.Observability.Tests")
				 .Should()
				 .BeTrue(shape.Id);
			AssertSolutionMembership(shape, "Application.Tests", present: true);
			AssertSolutionMembership(shape, "ArchitectureTests", present: true);
			AssertSolutionMembership(shape, "Domain.Tests", present: true);
			AssertSolutionMembership(shape, "Common.Observability.Tests", present: true);
			if (options.Api || options.Worker)
			{
				shape.ProjectDirectoryExists("IntegrationTests")
					 .Should()
					 .BeTrue(shape.Id);
				AssertSolutionMembership(shape, "IntegrationTests", present: true);
			}

			shape.FileExists("ArchitectureTests", "SupportedRuntimeShapeCompositionTests.cs")
				 .Should()
				 .BeFalse($"{shape.Id}: producer hive test must not ship in generated ArchitectureTests");
			shape.FileExists("ArchitectureTests", "CSharpCompositionSyntax.cs")
				 .Should()
				 .BeFalse($"{shape.Id}: producer syntax helper must not ship in generated ArchitectureTests");
			shape.FileExists("ArchitectureTests", "Directory.Build.props")
				 .Should()
				 .BeFalse($"{shape.Id}: producer Roslyn props must not ship in generated ArchitectureTests");
			PackageReferences(shape.ReadText("ArchitectureTests", $"{shape.Name}.ArchitectureTests.csproj"))
				.Should()
				.NotContain("Microsoft.CodeAnalysis.CSharp", Because(shape));
		}
		else
		{
			foreach (var testsProject in new[]
										 {
											 "Application.Tests", "ArchitectureTests", "Domain.Tests", "IntegrationTests", "Common.Observability.Tests", "Common.Domain.Tests", "Common.Infrastructure.Tests", "Common.Tests", "Common.BlobStorage.Tests"
										 })
			{
				shape.ProjectDirectoryExists(testsProject)
					 .Should()
					 .BeFalse($"{shape.Id}: unexpected {testsProject}");
				AssertSolutionMembership(shape, testsProject, present: false);
			}
		}

		shape.PackageVersions
			 .Should()
			 .Contain("OpenTelemetry.Exporter.OpenTelemetryProtocol", Because(shape));
		shape.PackageVersions
			 .Should()
			 .NotContain("Microsoft.CodeAnalysis.CSharp", Because(shape));
		shape.PackageVersions
			 .Should()
			 .NotContain(entry => entry.StartsWith($"{shape.Name}.Common.", StringComparison.Ordinal),
						 Because(shape));

		if (options.Files)
		{
			shape.PackageVersions
				 .Should()
				 .Contain("Azure.Storage.Blobs", Because(shape));
			PackageReferences(shape.ReadText("Common.BlobStorage", $"{shape.Name}.Common.BlobStorage.csproj"))
				.Should()
				.Contain("Azure.Storage.Blobs", Because(shape));
			ProjectReferences(shape.ReadText("Application", $"{shape.Name}.Application.csproj"))
				.Should()
				.Contain(path => path.Contains("BlobStorage", StringComparison.Ordinal), Because(shape));
		}
		else
		{
			shape.PackageVersions
				 .Should()
				 .NotContain("Azure.Storage.Blobs", Because(shape));
			ProjectReferences(shape.ReadText("Application", $"{shape.Name}.Application.csproj"))
				.Should()
				.NotContain(path => path.Contains("BlobStorage", StringComparison.Ordinal), Because(shape));
		}

		if (options.MassTransit)
		{
			shape.PackageVersions
				 .Should()
				 .Contain("MassTransit", Because(shape));
			ProjectReferences(shape.ReadText("Application", $"{shape.Name}.Application.csproj"))
				.Should()
				.Contain(path => path.Contains(".Messages", StringComparison.Ordinal), Because(shape));
		}
		else
		{
			shape.PackageVersions
				 .Should()
				 .NotContain(id => id.Equals("MassTransit", StringComparison.Ordinal) || id.StartsWith("MassTransit.", StringComparison.Ordinal), Because(shape));
			ProjectReferences(shape.ReadText("Application", $"{shape.Name}.Application.csproj"))
				.Should()
				.NotContain(path => path.Contains(".Messages", StringComparison.Ordinal), Because(shape));
		}

		if (options.Gateway)
			shape.PackageVersions
				 .Should()
				 .Contain("Yarp.ReverseProxy", Because(shape));
		else
			shape.PackageVersions
				 .Should()
				 .NotContain("Yarp.ReverseProxy", Because(shape));

		if (options.Auth)
			shape.PackageVersions
				 .Should()
				 .Contain("Keycloak.Net.Core", Because(shape));
		else
			shape.PackageVersions
				 .Should()
				 .NotContain("Keycloak.Net.Core", Because(shape));

		if (options.Keycloak)
			File.Exists(Path.Combine(shape.OutputDirectory, "realm-export-template.json"))
				.Should()
				.BeTrue(shape.Id);
		else
			File.Exists(Path.Combine(shape.OutputDirectory, "realm-export-template.json"))
				.Should()
				.BeFalse(shape.Id);

		if (options.Api)
			AssertHostReferenceGraph(shape, "Api", $"{shape.Name}.Api.csproj", options);

		if (options.Worker)
			AssertHostReferenceGraph(shape, "Worker", $"{shape.Name}.Worker.csproj", options);

		if (options.Gateway)
			AssertHostReferenceGraph(shape, "Common.ApiGateway", $"{shape.Name}.Common.ApiGateway.csproj", options);
	}

	private static void AssertHostReferenceGraph(GeneratedRuntimeShape shape,
												 string projectSuffix,
												 string csprojFileName,
												 ShapeOptions options)
	{
		var csproj = shape.ReadText(projectSuffix, csprojFileName);
		var projectRefs = ProjectReferences(csproj);
		var packageRefs = PackageReferences(csproj);

		projectRefs.Should()
				   .Contain(path => path.Contains("Common.Observability", StringComparison.Ordinal), Because(shape));

		if (projectSuffix is "Api" or "Worker")
			projectRefs.Should()
					   .Contain(path => path.Contains(".Application", StringComparison.Ordinal) && !path.Contains("Common.Api.Application", StringComparison.Ordinal),
								Because(shape));

		if (options.MassTransit && projectSuffix == "Worker")
			projectRefs.Should()
					   .Contain(path => path.Contains(".Messages", StringComparison.Ordinal), Because(shape));
		else if (projectSuffix == "Worker")
			projectRefs.Should()
					   .NotContain(path => path.Contains(".Messages", StringComparison.Ordinal), Because(shape));

		if (options.MassTransit && projectSuffix is "Api" or "Worker")
			packageRefs.Should()
					   .Contain(name => name.StartsWith("MassTransit", StringComparison.Ordinal), Because(shape));
		else
			packageRefs.Should()
					   .NotContain(name => name.StartsWith("MassTransit", StringComparison.Ordinal), Because(shape));

		if (projectSuffix == "Common.ApiGateway")
		{
			packageRefs.Should()
					   .Contain("Yarp.ReverseProxy", Because(shape));
			projectRefs.Should()
					   .NotContain(path => path.Contains(".Application", StringComparison.Ordinal) && !path.Contains("Common.Api", StringComparison.Ordinal),
								   Because(shape));
			csproj.Should()
				  .NotContain("EntityFrameworkCore", Because(shape));
		}
	}

	private static void AssertHostObservability(GeneratedRuntimeShape shape)
	{
		var options = shape.Options;

		File.Exists(shape.GuidePath)
			.Should()
			.BeTrue(shape.Id);
		shape.Solution
			 .Should()
			 .Contain("<File Path=\"OBSERVABILITY.md\" />", Because(shape));
		shape.Guide
			 .Should()
			 .Contain("## Runtime host profiles and resource identity", Because(shape));
		shape.Guide
			 .Should()
			 .Contain("## High-fidelity telemetry boundary", Because(shape));

		AssertHostArtifacts(shape, "Api", options.Api, "AddApiObservability", "### API profile");
		AssertHostArtifacts(shape, "Worker", options.Worker, "AddWorkerObservability", "### Worker profile");
		AssertHostArtifacts(shape, "Common.ApiGateway", options.Gateway, "AddGatewayObservability", "### Gateway profile");

		var observability = shape.ReadText("Common.Observability", "ObservabilityHostBuilderExtensions.cs");
		if (options.MassTransit)
		{
			CSharpCompositionSyntax.InvokesWithStringArgument(observability, "AddSource", "MassTransit")
								   .Should()
								   .BeTrue(Because(shape));
			CSharpCompositionSyntax.InvokesWithStringArgument(observability, "AddMeter", "MassTransit")
								   .Should()
								   .BeTrue(Because(shape));
		}

		if (options.Gateway)
			CSharpCompositionSyntax.InvokesWithStringArgument(observability, "AddSource", "Yarp.ReverseProxy")
								   .Should()
								   .BeTrue(Because(shape));

		if (options.Api || options.Worker)
			shape.Guide
				 .Should()
				 .Contain("native SQL Client instrumentation", Because(shape));
		else
			shape.Guide
				 .Should()
				 .NotContain("native SQL Client instrumentation", Because(shape));

		if (options.Gateway)
			shape.Guide
				 .Should()
				 .Contain("Gateway has no SQL instrumentation.", Because(shape));
		else
			shape.Guide
				 .Should()
				 .NotContain("Gateway has no SQL instrumentation.", Because(shape));

		if (options.Api || options.Gateway)
			shape.Guide
				 .Should()
				 .Contain("## HTTP exception boundaries", Because(shape));
		else
			shape.Guide
				 .Should()
				 .NotContain("## HTTP exception boundaries", Because(shape));

		if (options.Auth)
			shape.Guide
				 .Should()
				 .Contain("## Authenticated request identity", Because(shape));
		else
			shape.Guide
				 .Should()
				 .NotContain("## Authenticated request identity", Because(shape));

		if (options.Api)
			shape.Guide
				 .Should()
				 .Contain("## Successful company creations", Because(shape));
		else
			shape.Guide
				 .Should()
				 .NotContain("## Successful company creations", Because(shape));

		if (options is
			{
				Api: true, Worker: true
			})
			shape.Guide
				 .Should()
				 .Contain("## Create a Company and inspect local observability", Because(shape));
		else
			shape.Guide
				 .Should()
				 .NotContain("## Create a Company and inspect local observability", Because(shape));

		if (options is
			{
				Worker: true, Api: false, Gateway: false, Tests: false
			})
			shape.Guide
				 .Should()
				 .Contain("## Worker-only solutions", Because(shape));
		else
			shape.Guide
				 .Should()
				 .NotContain("## Worker-only solutions", Because(shape));

		if (options.Gateway)
			AssertGatewayClaimsNoDatabaseOrCompanyMetrics(shape);

		if (options.Tests)
		{
			shape.ProjectDirectoryExists("Common.Observability.Tests")
				 .Should()
				 .BeTrue(shape.Id);
			AssertHostDiagnosticsTest(shape,
									  "BoundaryExceptionDiagnosticsTests.cs",
									  "BoundaryExceptionDiagnosticsTests",
									  options.Api);
			AssertHostDiagnosticsTest(shape,
									  "GatewayBoundaryExceptionDiagnosticsTests.cs",
									  "GatewayBoundaryExceptionDiagnosticsTests",
									  options.Gateway);
		}
	}

	private static void AssertGatewayClaimsNoDatabaseOrCompanyMetrics(GeneratedRuntimeShape shape)
	{
		var program = shape.ReadText("Common.ApiGateway", "Program.cs");
		CSharpCompositionSyntax.ContainsIdentifier(program, "AppDbContext")
							   .Should()
							   .BeFalse(Because(shape));
		CSharpCompositionSyntax.Invokes(program, "AddDbContext")
							   .Should()
							   .BeFalse(Because(shape));
		CSharpCompositionSyntax.Invokes(program, "ConfigureAuditableFields")
							   .Should()
							   .BeFalse(Because(shape));
		program.Should()
			   .NotContain("company.successful_creations", Because(shape));

		var settings = shape.ReadText("Common.ApiGateway", "appsettings.json");
		settings.Should()
				.NotContain("ConnectionStrings", Because(shape));
		settings.Should()
				.NotContain("AppDbContext", Because(shape));
		settings.Should()
				.NotContain("company.successful_creations", Because(shape));

		var csproj = shape.ReadText("Common.ApiGateway", $"{shape.Name}.Common.ApiGateway.csproj");
		csproj.Should()
			  .NotContain("EntityFrameworkCore", Because(shape));
		csproj.Should()
			  .NotContain("SqlClient", Because(shape));

		var gatewayGuideSlice = ExtractGuideSection(shape.Guide, "### Gateway profile");
		gatewayGuideSlice.Should()
						 .NotContain("SQL Client", Because(shape));
		gatewayGuideSlice.Should()
						 .NotContain("company.successful_creations", Because(shape));
		gatewayGuideSlice.Should()
						 .NotContain("database", Because(shape));
	}

	private static void AssertPersistenceComposition(GeneratedRuntimeShape shape)
	{
		var options = shape.Options;
		var applicationDi = shape.ReadText("Application", "DependencyInjection", "ServiceCollectionExtensions.cs");
		CSharpCompositionSyntax.CountGenericInvocations(applicationDi, "AddScoped", "AuditableSaveChangesInterceptor")
							   .Should()
							   .Be(1, Because(shape));
		CSharpCompositionSyntax.CountAuditableInterceptorRegistrations(applicationDi)
							   .Should()
							   .Be(1, Because(shape));

		var companyConfig = shape.ReadText("Application", "Persistence", "EntityConfigurations", "CompanyEntityConfiguration.cs");
		CSharpCompositionSyntax.Invokes(companyConfig, "ConfigureAuditableFields")
							   .Should()
							   .BeTrue(Because(shape));

		if (options.Files)
		{
			CSharpCompositionSyntax.Invokes(shape.ReadText("Application", "Persistence", "EntityConfigurations", "ProductEntityConfiguration.cs"), "ConfigureAuditableFields")
								   .Should()
								   .BeTrue(Because(shape));
			CSharpCompositionSyntax.Invokes(shape.ReadText("Application", "Persistence", "EntityConfigurations", "FileEntityConfiguration.cs"), "ConfigureAuditableFields")
								   .Should()
								   .BeTrue(Because(shape));
		}

		var migrationsDir = Path.Combine(shape.OutputDirectory, $"{shape.Name}.Application", "Persistence", "Migrations");
		Directory.Exists(migrationsDir)
				 .Should()
				 .BeTrue(shape.Id);
		Directory.GetFiles(migrationsDir, "*_Init.cs")
				 .Should()
				 .ContainSingle(Because(shape));
		Directory.GetFiles(migrationsDir, "*_Init.Designer.cs")
				 .Should()
				 .ContainSingle(Because(shape));
		File.Exists(Path.Combine(migrationsDir, "AppDbContextModelSnapshot.cs"))
			.Should()
			.BeTrue(shape.Id);

		var expectedFingerprintId = (options.Files, options.MassTransit) switch
									{
										(true, true) => "20260915220138",
										(false, false) => "20260915223105",
										(true, false) => "20260915223146",
										(false, true) => "20260915223219"
									};
		Directory.GetFiles(migrationsDir, "*_Init.cs")
				 .Select(Path.GetFileName)
				 .Should()
				 .ContainSingle()
				 .Which
				 .Should()
				 .StartWith(expectedFingerprintId, Because(shape));

		if (options.Api)
		{
			var apiProgram = shape.ReadText("Api", "Program.cs");
			CSharpCompositionSyntax.CountGenericInvocations(apiProgram, "AddScoped", "IPersistenceActorProvider", "ApiPersistenceActorProvider")
								   .Should()
								   .Be(1, Because(shape));
			CSharpCompositionSyntax.ContainsIdentifier(apiProgram, "AuditableSaveChangesInterceptor")
								   .Should()
								   .BeFalse(Because(shape));
			shape.FileExists("Api", "Persistence", "ApiPersistenceActorProvider.cs")
				 .Should()
				 .BeTrue(shape.Id);
		}

		if (options.Worker)
		{
			var workerProgram = shape.ReadText("Worker", "Program.cs");
			CSharpCompositionSyntax.CountGenericInvocations(workerProgram, "AddScoped", "IPersistenceActorProvider", "WorkerPersistenceActorProvider")
								   .Should()
								   .Be(1, Because(shape));
			CSharpCompositionSyntax.ContainsIdentifier(workerProgram, "AuditableSaveChangesInterceptor")
								   .Should()
								   .BeFalse(Because(shape));
			shape.FileExists("Worker", "Persistence", "WorkerPersistenceActorProvider.cs")
				 .Should()
				 .BeTrue(shape.Id);
		}

		if (options.Gateway)
		{
			var gatewayProgram = shape.ReadText("Common.ApiGateway", "Program.cs");
			CSharpCompositionSyntax.ContainsIdentifier(gatewayProgram, "IPersistenceActorProvider")
								   .Should()
								   .BeFalse(Because(shape));
			CSharpCompositionSyntax.ContainsIdentifier(gatewayProgram, "AuditableSaveChangesInterceptor")
								   .Should()
								   .BeFalse(Because(shape));
			CSharpCompositionSyntax.Invokes(gatewayProgram, "AddDbContext")
								   .Should()
								   .BeFalse(Because(shape));
			CSharpCompositionSyntax.ContainsIdentifier(gatewayProgram, "AppDbContext")
								   .Should()
								   .BeFalse(Because(shape));
			CSharpCompositionSyntax.Invokes(gatewayProgram, "ConfigureAuditableFields")
								   .Should()
								   .BeFalse(Because(shape));
		}
	}

	private static void AssertHostArtifacts(GeneratedRuntimeShape shape,
											string projectSuffix,
											bool present,
											string observabilityCall,
											string guideProfileHeading)
	{
		if (present)
		{
			shape.ProjectDirectoryExists(projectSuffix)
				 .Should()
				 .BeTrue(shape.Id);
			CSharpCompositionSyntax.Invokes(shape.ReadText(projectSuffix, "Program.cs"), observabilityCall)
								   .Should()
								   .BeTrue(Because(shape));
			var settings = shape.ReadText(projectSuffix, "appsettings.json");
			settings.Should()
					.Contain("\"Observability\"", Because(shape));
			AssertSignalEnabled(settings, shape, "Traces");
			AssertSignalEnabled(settings, shape, "Metrics");
			AssertSignalEnabled(settings, shape, "Logs");
			shape.ReadText(projectSuffix, "appsettings.Development.json")
				 .Should()
				 .Contain("OTEL_EXPORTER_OTLP_ENDPOINT", Because(shape));
			shape.ReadText(projectSuffix, "Properties", "launchSettings.json")
				 .Should()
				 .NotContain("OTEL_", Because(shape));
			shape.Guide
				 .Should()
				 .Contain(guideProfileHeading, Because(shape));
		}
		else
		{
			shape.ProjectDirectoryExists(projectSuffix)
				 .Should()
				 .BeFalse(shape.Id);
			shape.Guide
				 .Should()
				 .NotContain(guideProfileHeading, Because(shape));
		}
	}

	private static void AssertAbsentHost(GeneratedRuntimeShape shape, string projectSuffix, string guideProfileHeading)
	{
		shape.ProjectDirectoryExists(projectSuffix)
			 .Should()
			 .BeFalse(shape.Id);
		AssertSolutionMembership(shape, projectSuffix, present: false);
		shape.Guide
			 .Should()
			 .NotContain(guideProfileHeading, Because(shape));
	}

	private static void AssertHostDiagnosticsTest(GeneratedRuntimeShape shape,
												  string fileName,
												  string typeName,
												  bool present)
	{
		if (present)
		{
			shape.FileExists("IntegrationTests", "Tests", fileName)
				 .Should()
				 .BeTrue(shape.Id);
			shape.ReadText("IntegrationTests", "Tests", fileName)
				 .Should()
				 .Contain($"class {typeName}", Because(shape));
		}
		else
		{
			shape.FileExists("IntegrationTests", "Tests", fileName)
				 .Should()
				 .BeFalse(shape.Id);
		}
	}

	private static void AssertSignalEnabled(string settingsJson, GeneratedRuntimeShape shape, string signalName)
	{
		using var document = System.Text.Json.JsonDocument.Parse(settingsJson,
																 new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true });
		document.RootElement
				.GetProperty("Observability")
				.GetProperty("Signals")
				.GetProperty(signalName)
				.GetProperty("Enabled")
				.GetBoolean()
				.Should()
				.BeTrue($"{shape.Id}: Observability:Signals:{signalName}:Enabled");
	}

	private static void AssertProjectPresence(GeneratedRuntimeShape shape, string projectSuffix, bool present)
	{
		if (present)
			shape.ProjectDirectoryExists(projectSuffix)
				 .Should()
				 .BeTrue($"{shape.Id}: expected {projectSuffix}");
		else
			shape.ProjectDirectoryExists(projectSuffix)
				 .Should()
				 .BeFalse($"{shape.Id}: unexpected {projectSuffix}");
	}

	private static void AssertSolutionMembership(GeneratedRuntimeShape shape, string projectSuffix, bool present)
	{
		var marker = $"Project Path=\"{shape.Name}.{projectSuffix}/";
		if (present)
			shape.Solution
				 .Should()
				 .Contain(marker, Because(shape));
		else
			shape.Solution
				 .Should()
				 .NotContain(marker, Because(shape));
	}

	private static void AssertNoObservabilityOnlySampleMessage(GeneratedRuntimeShape shape)
	{
		var offenders = Directory.EnumerateFiles(shape.OutputDirectory, "*.cs", SearchOption.AllDirectories)
								 .Where(path =>
										{
											var relative = Path.GetRelativePath(shape.OutputDirectory, path);
											return !relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
															.Any(segment => segment is "bin" or "obj");
										})
								 .Select(path => Path.GetFileNameWithoutExtension(path)!)
								 .Where(IsObservabilityOnlySampleName)
								 .Distinct(StringComparer.OrdinalIgnoreCase)
								 .ToArray();

		offenders.Should()
				 .BeEmpty($"{shape.Id} must not add an observability-only sample message: {string.Join(", ", offenders)}");
	}

	private static bool IsObservabilityOnlySampleName(string name) =>
		name.Contains("SampleMessage", StringComparison.OrdinalIgnoreCase) ||
		name.Contains("TelemetryProbe", StringComparison.OrdinalIgnoreCase) ||
		name.Contains("OtelSample", StringComparison.OrdinalIgnoreCase) ||
		(name.Contains("Observability", StringComparison.OrdinalIgnoreCase) &&
		 (name.Contains("Sample", StringComparison.OrdinalIgnoreCase) ||
		  name.Contains("Probe", StringComparison.OrdinalIgnoreCase) ||
		  name.Contains("Message", StringComparison.OrdinalIgnoreCase) ||
		  name.Contains("Fixture", StringComparison.OrdinalIgnoreCase)));

	private static string ExtractGuideSection(string guide, string heading)
	{
		var start = guide.IndexOf(heading, StringComparison.Ordinal);
		if (start < 0)
			return string.Empty;

		var next = guide.IndexOf("\n### ", start + heading.Length, StringComparison.Ordinal);
		if (next < 0)
			next = guide.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);

		return next < 0 ? guide[start..] : guide[start..next];
	}

	private static IReadOnlyList<string> ProjectReferences(string csproj) =>
	[
		.. ProjectReferenceInclude.Matches(csproj)
								  .Select(match => match.Groups[1].Value)
	];

	private static IReadOnlyList<string> PackageReferences(string csproj) =>
	[
		.. PackageReferenceInclude.Matches(csproj)
								  .Select(match => match.Groups[1].Value)
	];

	private static string Because(GeneratedRuntimeShape shape) =>
		shape.Id;

	public sealed record ShapeOptions(bool Api,
									  bool Worker,
									  bool Gateway,
									  bool Auth,
									  bool MassTransit,
									  bool Files,
									  bool Tests,
									  bool Common,
									  bool Keycloak);

	public sealed class GeneratedRuntimeShape
	{
		private static readonly Regex PackageInclude = new(@"Include=""([^""]+)""",
														   RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public GeneratedRuntimeShape(string id, string name, string outputDirectory, ShapeOptions options)
		{
			Id = id;
			Name = name;
			OutputDirectory = outputDirectory;
			Options = options;
			var solutionPath = Path.Combine(outputDirectory, $"{name}.slnx");
			Solution = File.ReadAllText(solutionPath);
			var packagesPath = Path.Combine(outputDirectory, "Directory.Packages.props");
			PackageVersions =
			[
				.. PackageInclude.Matches(File.ReadAllText(packagesPath))
								 .Select(match => match.Groups[1].Value)
			];
			GuidePath = Path.Combine(outputDirectory, "OBSERVABILITY.md");
			Guide = File.Exists(GuidePath) ? File.ReadAllText(GuidePath) : string.Empty;
		}

		public string Id { get; }
		public string Name { get; }
		public string OutputDirectory { get; }
		public ShapeOptions Options { get; }
		public string Solution { get; }
		public IReadOnlyList<string> PackageVersions { get; }
		public string GuidePath { get; }
		public string Guide { get; }

		public bool ProjectDirectoryExists(string projectSuffix) =>
			Directory.Exists(Path.Combine(OutputDirectory, $"{Name}.{projectSuffix}"));

		public bool FileExists(params string[] relativeParts) =>
			File.Exists(Path.Combine(new[] { OutputDirectory }.Concat(ExpandRelative(relativeParts))
															  .ToArray()));

		public string ReadText(params string[] relativeParts) =>
			File.ReadAllText(Path.Combine(new[] { OutputDirectory }.Concat(ExpandRelative(relativeParts))
																   .ToArray()));

		private IEnumerable<string> ExpandRelative(string[] relativeParts)
		{
			if (relativeParts.Length == 0)
				yield break;

			if (relativeParts[0]
					.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
				relativeParts[0]
					.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
				relativeParts[0]
					.Contains('.', StringComparison.Ordinal) &&
				relativeParts.Length == 1)
			{
				foreach (var part in relativeParts)
					yield return part;
				yield break;
			}

			yield return $"{Name}.{relativeParts[0]}";
			for (var i = 1; i < relativeParts.Length; i++)
				yield return relativeParts[i];
		}
	}

	public sealed class RuntimeShapeHiveFixture : IDisposable
	{
		private const int CommandTimeoutMilliseconds = 180_000;

		private readonly string _root = Path.Combine(Path.GetTempPath(), $"monaco-runtime-shape-tests-{Guid.NewGuid():N}");
		private readonly string _hivePath;

		public RuntimeShapeHiveFixture()
		{
			_hivePath = Path.Combine(_root, "hive");
			Directory.CreateDirectory(_hivePath);
			RunDotnet($"new install \"{FindTemplateSourceDirectory()}\" --debug:custom-hive \"{_hivePath}\"");

			R1 = Generate("R1", new ShapeOptions(true, true, false, true, true, true, true, true, true));
			R2 = Generate("R2",
						  new ShapeOptions(true, true, true, true, true, true, true, true, true),
						  "--apiGateway",
						  "true");
			R3 = Generate("R3",
						  new ShapeOptions(true, false, false, false, false, false, true, true, false),
						  "--workerService",
						  "false",
						  "--auth",
						  "false",
						  "--massTransitIntegration",
						  "false",
						  "--filesSupport",
						  "false",
						  "--keycloakConfig",
						  "false");
			R4 = Generate("R4",
						  new ShapeOptions(true, false, false, false, true, true, true, true, false),
						  "--workerService",
						  "false",
						  "--auth",
						  "false",
						  "--keycloakConfig",
						  "false");
			R5 = Generate("R5",
						  new ShapeOptions(false, true, false, false, true, true, false, true, false),
						  "--apiService",
						  "false",
						  "--auth",
						  "false",
						  "--tests",
						  "false",
						  "--keycloakConfig",
						  "false");
			R6 = Generate("R6",
						  new ShapeOptions(false, true, false, false, false, false, false, true, false),
						  "--apiService",
						  "false",
						  "--auth",
						  "false",
						  "--massTransitIntegration",
						  "false",
						  "--filesSupport",
						  "false",
						  "--tests",
						  "false",
						  "--keycloakConfig",
						  "false");
			R7 = Generate("R7",
						  new ShapeOptions(true, true, false, true, true, true, false, true, true),
						  "--tests",
						  "false");
		}

		public GeneratedRuntimeShape R1 { get; }
		public GeneratedRuntimeShape R2 { get; }
		public GeneratedRuntimeShape R3 { get; }
		public GeneratedRuntimeShape R4 { get; }
		public GeneratedRuntimeShape R5 { get; }
		public GeneratedRuntimeShape R6 { get; }
		public GeneratedRuntimeShape R7 { get; }

		public void Dispose()
		{
			try
			{
				Directory.Delete(_root, recursive: true);
			}
			catch (IOException)
			{ }
			catch (UnauthorizedAccessException)
			{ }
		}

		private GeneratedRuntimeShape Generate(string id, ShapeOptions options, params string[] parameters)
		{
			var name = $"Comp{id}";
			var outputDirectory = Path.Combine(_root, name);
			var arguments =
				$"new monaco-backend-solution --name \"{name}\" --output \"{outputDirectory}\" --debug:custom-hive \"{_hivePath}\"";
			if (parameters.Length > 0)
				arguments += $" {string.Join(" ", parameters)}";

			RunDotnet(arguments);
			return new GeneratedRuntimeShape(id, name, outputDirectory, options);
		}

		private static string RunDotnet(string arguments)
		{
			var startInfo = new ProcessStartInfo("dotnet", arguments)
							{
								RedirectStandardOutput = true,
								RedirectStandardError = true,
								UseShellExecute = false,
								CreateNoWindow = true,
								Environment = { ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1", ["DOTNET_NOLOGO"] = "1", ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1" }
							};

			using var process = Process.Start(startInfo)!;
			var standardOutput = process.StandardOutput.ReadToEndAsync();
			var standardError = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(CommandTimeoutMilliseconds))
			{
				process.Kill(entireProcessTree: true);
				throw new TimeoutException($"'dotnet {arguments}' did not finish within {CommandTimeoutMilliseconds / 1000} seconds.");
			}

			var combined = standardOutput.GetAwaiter().GetResult() + standardError.GetAwaiter().GetResult();
			Assert.True(process.ExitCode == 0,
						$"'dotnet {arguments}' failed with exit code {process.ExitCode}:{Environment.NewLine}{combined}");
			return combined;
		}

		private static string FindTemplateSourceDirectory()
		{
			for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
				if (File.Exists(Path.Combine(directory.FullName, ".template.config", "template.json")))
					return directory.FullName;

			throw new DirectoryNotFoundException("Could not locate the template source directory containing .template.config/template.json.");
		}
	}
}