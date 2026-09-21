using AwesomeAssertions;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Persistence;

[ExcludeFromCodeCoverage]
[Trait("Application Persistence", "Attribution Composition Boundaries")]
public sealed class AttributionCompositionBoundaryTests
{
	private static readonly string[] StampNames =
	[
		"CreatedAtUtc", "CreatedBy", "ModifiedAtUtc", "ModifiedBy"
	];

	[Fact(DisplayName = "Hosts and Application compose attribution exactly once")]
	public void HostsAndApplicationComposeAttributionExactlyOnce()
	{
		var root = RequireSolutionRoot();

		var applicationDi = Read(root, "Monaco.Template.Backend.Application", "DependencyInjection", "ServiceCollectionExtensions.cs");
		CountOccurrences(applicationDi, "AddScoped<AuditableSaveChangesInterceptor>()")
			.Should()
			.Be(1);
		CountOccurrences(applicationDi, "AddInterceptors(sp.GetRequiredService<AuditableSaveChangesInterceptor>())")
			.Should()
			.Be(1);
		applicationDi.Should()
					 .NotContain("IPersistenceActorProvider");

		var apiProgram = Read(root, "Monaco.Template.Backend.Api", "Program.cs");
		CountOccurrences(apiProgram, "AddScoped<IPersistenceActorProvider, ApiPersistenceActorProvider>()")
			.Should()
			.Be(1);
		apiProgram.Should()
				  .NotContain("AuditableSaveChangesInterceptor");
		apiProgram.Should()
				  .NotContain("AddInterceptors");

		var workerProgram = Read(root, "Monaco.Template.Backend.Worker", "Program.cs");
		CountOccurrences(workerProgram, "AddScoped<IPersistenceActorProvider, WorkerPersistenceActorProvider>()")
			.Should()
			.Be(1);
		workerProgram.Should()
					 .NotContain("AuditableSaveChangesInterceptor");
		workerProgram.Should()
					 .NotContain("AddInterceptors");

		foreach (var entity in new[] { "Company", "Product", "File" })
		{
			var config = Read(root,
							  "Monaco.Template.Backend.Application",
							  "Persistence",
							  "EntityConfigurations",
							  $"{entity}EntityConfiguration.cs");
			CountOccurrences(config, "ConfigureAuditableFields()")
				.Should()
				.Be(1, entity);
		}

		var entityConfigs = Directory.GetFiles(Path.Combine(root,
															"Monaco.Template.Backend.Application",
															"Persistence",
															"EntityConfigurations"),
											   "*EntityConfiguration.cs");
		entityConfigs.Select(path => CountOccurrences(File.ReadAllText(path), "ConfigureAuditableFields()"))
					 .Sum()
					 .Should()
					 .Be(3);
	}

	[Fact(DisplayName = "Gateway and host excludes keep attribution off non-persistence shapes")]
	public void GatewayAndHostExcludesKeepAttributionOffNonPersistenceShapes()
	{
		var root = RequireSolutionRoot();

		var gatewayProgram = Read(root, "Monaco.Template.Backend.Common.ApiGateway", "Program.cs");
		gatewayProgram.Should()
					  .NotContain("ConfigureApplication");
		gatewayProgram.Should()
					  .NotContain("IPersistenceActorProvider");
		gatewayProgram.Should()
					  .NotContain("AuditableSaveChangesInterceptor");
		gatewayProgram.Should()
					  .NotContain("AddDbContext");
		gatewayProgram.Should()
					  .NotContain("AppDbContext");
		gatewayProgram.Should()
					  .NotContain("ConfigureAuditableFields");

		using var template = JsonDocument.Parse(Read(root, ".template.config", "template.json"));
		var sources = template.RootElement
							  .GetProperty("sources")[0];

		UnconditionalExcludes(sources)
			.Should()
			.Contain("Monaco.Template.Backend.Application.Tests/Persistence/AttributionCompositionBoundaryTests.cs");

		ConditionalExcludes(sources, "(!apiService)")
			.Should()
			.Contain("Monaco.Template.Backend.Api/**/*");
		ConditionalExcludes(sources, "(!workerService)")
			.Should()
			.Contain("Monaco.Template.Backend.Worker/**/*");
		ConditionalExcludes(sources, "(!apiGateway)")
			.Should()
			.Contain("Monaco.Template.Backend.Common.ApiGateway/**/*");
		ConditionalExcludes(sources, "(!commonLibraries)")
			.Should()
			.Contain("Monaco.Template.Backend.Common.Infrastructure/**/*");

		var noPersistenceHosts = ConditionalExcludes(sources, "(!apiService && !workerService)");
		noPersistenceHosts.Should()
						  .Contain("Monaco.Template.Backend.Api/**/*");
		noPersistenceHosts.Should()
						  .Contain("Monaco.Template.Backend.Worker/**/*");
	}

	[Fact(DisplayName = "Compiled Full Init stays alone while staged trios stay staged")]
	public void CompiledFullInitStaysAloneWhileStagedTriosStayStaged()
	{
		var root = RequireSolutionRoot();

		var compiledMigrations = Path.Combine(root, "Monaco.Template.Backend.Application", "Persistence", "Migrations");
		Directory.Exists(compiledMigrations)
				 .Should()
				 .BeTrue();
		Directory.GetFiles(compiledMigrations, "*_Init.cs")
				 .Should()
				 .ContainSingle()
				 .Which
				 .Should()
				 .EndWith("_Init.cs")
				 .And
				 .NotEndWith("_Init.Designer.cs");
		Directory.GetFiles(compiledMigrations, "*_Init.Designer.cs")
				 .Should()
				 .ContainSingle();
		File.Exists(Path.Combine(compiledMigrations, "AppDbContextModelSnapshot.cs"))
			.Should()
			.BeTrue();

		var variantsRoot = Path.Combine(root, "_TemplateArtifacts", "MigrationVariants");
		foreach (var fingerprint in new[] { "Core", "FilesOnly", "MessagingOnly" })
		{
			var directory = Path.Combine(variantsRoot, fingerprint);
			Directory.Exists(directory)
					 .Should()
					 .BeTrue(fingerprint);
			Directory.GetFiles(directory, "*_Init.cs")
					 .Should()
					 .ContainSingle(fingerprint);
			Directory.GetFiles(directory, "*_Init.Designer.cs")
					 .Should()
					 .ContainSingle(fingerprint);
			File.Exists(Path.Combine(directory, "AppDbContextModelSnapshot.cs"))
				.Should()
				.BeTrue(fingerprint);
		}

		using var template = JsonDocument.Parse(Read(root, ".template.config", "template.json"));
		UnconditionalExcludes(template.RootElement
									  .GetProperty("sources")[0])
			.Should()
			.Contain("_TemplateArtifacts/**");
	}

	[Fact(DisplayName = "DeleteCompany bypasses tracked save and no other bulk SQL APIs appear")]
	public void DeleteCompanyBypassesTrackedSaveAndNoOtherBulkSqlApisAppear()
	{
		var root = RequireSolutionRoot();

		var deleteCompany = Read(root,
								 "Monaco.Template.Backend.Application",
								 "Features",
								 "Company",
								 "DeleteCompany.cs");
		deleteCompany.Should()
					 .Contain("ExecuteDeleteAsync");
		deleteCompany.Should()
					 .NotContain("SaveEntitiesAsync");

		var deleteProduct = Read(root,
								 "Monaco.Template.Backend.Application",
								 "Features",
								 "Product",
								 "DeleteProduct.cs");
		deleteProduct.Should()
					 .Contain("SaveEntitiesAsync");
		deleteProduct.Should()
					 .NotContain("ExecuteDeleteAsync");

		var applicationSources = Directory.GetFiles(Path.Combine(root, "Monaco.Template.Backend.Application"),
													"*.cs",
													SearchOption.AllDirectories)
										  .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
																		StringComparison.OrdinalIgnoreCase) &&
														 !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
																		StringComparison.OrdinalIgnoreCase))
										  .ToList();

		foreach (var path in applicationSources)
		{
			var text = File.ReadAllText(path);
			text.Should()
				.NotContain("ExecuteUpdate", because: path);
			text.Should()
				.NotContain("FromSql", because: path);
			text.Should()
				.NotContain("ExecuteSql", because: path);
		}

		applicationSources.Count(path => File.ReadAllText(path)
											 .Contains("ExecuteDeleteAsync", StringComparison.Ordinal))
						  .Should()
						  .Be(1);
	}

	[Fact(DisplayName = "Stamp names stay off public surfaces and interceptor assigns only UTC plus actor")]
	public void StampNamesStayOffPublicSurfacesAndInterceptorAssignsOnlyUtcPlusActor()
	{
		var root = RequireSolutionRoot();

		var publicSurfaces =
			new[]
			{
				Path.Combine(root, "Monaco.Template.Backend.Application", "Features", "Company", "DTOs", "CompanyDto.cs"),
				Path.Combine(root, "Monaco.Template.Backend.Application", "Features", "Country", "DTOs", "CountryDto.cs"),
				Path.Combine(root, "Monaco.Template.Backend.Application", "Features", "Product", "DTOs", "ProductDto.cs"),
				Path.Combine(root, "Monaco.Template.Backend.Application", "Features", "File", "DTOs", "FileDto.cs"),
				Path.Combine(root, "Monaco.Template.Backend.Application", "Features", "File", "DTOs", "ImageDto.cs"),
				Path.Combine(root, "Monaco.Template.Backend.Api", "DTOs", "CompanyCreateEditDto.cs"),
				Path.Combine(root, "Monaco.Template.Backend.Api", "DTOs", "ProductCreateEditDto.cs"),
				Path.Combine(root, "Monaco.Template.Backend.Messages", "V1", "ProductCreated.cs"),
				Path.Combine(root, "Monaco.Template.Backend.Common.Observability", "IdentityEnrichmentMiddleware.cs"),
				Path.Combine(root, "Monaco.Template.Backend.Common.Application", "Extensions", "ClaimsPrincipalExtensions.cs")
			};

		foreach (var path in publicSurfaces)
		{
			File.Exists(path)
				.Should()
				.BeTrue(path);
			AssertNoStampNames(File.ReadAllText(path), path);
		}

		foreach (var path in Directory.GetFiles(Path.Combine(root, "Monaco.Template.Backend.Common.Observability"),
												"*.cs",
												SearchOption.AllDirectories)
									  .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
																	StringComparison.OrdinalIgnoreCase) &&
													 !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
																	StringComparison.OrdinalIgnoreCase)))
			AssertNoStampNames(File.ReadAllText(path), path);

		var interceptor = Read(root,
							   "Monaco.Template.Backend.Common.Infrastructure",
							   "Persistence",
							   "AuditableSaveChangesInterceptor.cs");
		var creation = MethodBody(interceptor, "ApplyCreationStamps");
		var modification = MethodBody(interceptor, "ApplyModificationStamps");

		creation.Should()
				.Contain("utcNow");
		creation.Should()
				.Contain("actor");
		modification.Should()
					.Contain("utcNow");
		modification.Should()
					.Contain("actor");

		foreach (var body in new[] { creation, modification })
		{
			body.Should()
				.NotContain("exception", because: "stamp assigns must not copy exception text");
			body.Should()
				.NotContain("Activity");
			body.Should()
				.NotContain("Baggage");
			body.Should()
				.NotContain("GetUtcNow");
			body.Should()
				.NotContain("Resolve");
			body.Should()
				.NotContain("Message");
		}
	}

	private static void AssertNoStampNames(string text, string path)
	{
		foreach (var stamp in StampNames)
			text.Should()
				.NotContain(stamp, because: path);
	}

	private static string MethodBody(string source, string methodName)
	{
		var match = Regex.Match(source,
								$$"""
								  private\s+static\s+void\s+{{Regex.Escape(methodName)}}\s*\([^)]*\)\s*\{(?<body>.*?)(?=\r?\n\tprivate\s|\r?\n\})
								  """,
								RegexOptions.Singleline);
		match.Success
			 .Should()
			 .BeTrue(methodName);
		return match.Groups["body"].Value;
	}

	private static int CountOccurrences(string text, string value)
	{
		var count = 0;
		var index = 0;
		while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += value.Length;
		}

		return count;
	}

	private static IReadOnlyList<string> UnconditionalExcludes(JsonElement sources) =>
	[
		.. sources.GetProperty("exclude")
				  .EnumerateArray()
				  .Select(element => element.GetString())
				  .Where(value => value is not null)
				  .Cast<string>()
	];

	private static IReadOnlyList<string> ConditionalExcludes(JsonElement sources, string condition)
	{
		var modifier = sources.GetProperty("modifiers")
							  .EnumerateArray()
							  .Single(element => element.TryGetProperty("condition", out var value) &&
												 value.GetString() == condition);
		return
		[
			.. modifier.GetProperty("exclude")
					   .EnumerateArray()
					   .Select(element => element.GetString())
					   .Where(value => value is not null)
					   .Cast<string>()
		];
	}

	private static string Read(string root, params string[] segments)
	{
		var path = Path.Combine([
									root, .. segments
								]);
		File.Exists(path)
			.Should()
			.BeTrue(path);
		return File.ReadAllText(path);
	}

	private static string RequireSolutionRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Monaco.Template.Backend.slnx")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new InvalidOperationException("Could not locate Monaco.Template.Backend.slnx from the test base directory.");
	}
}