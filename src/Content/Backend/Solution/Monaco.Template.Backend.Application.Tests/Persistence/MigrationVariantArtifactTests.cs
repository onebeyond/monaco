using AwesomeAssertions;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Persistence;

[ExcludeFromCodeCoverage]
[Trait("Application Persistence", "Migration Variants")]
public partial class MigrationVariantArtifactTests
{
	private static readonly Regex CreateTableName = CreateTableRegex();

	private static readonly (string Condition, string Source, string Id)[] ReducedRoutes =
	[
		("(!filesSupport && !massTransitIntegration)", "./_TemplateArtifacts/MigrationVariants/Core/", "20260915223105"),
		("(filesSupport && !massTransitIntegration)", "./_TemplateArtifacts/MigrationVariants/FilesOnly/", "20260915223146"),
		("(!filesSupport && massTransitIntegration)", "./_TemplateArtifacts/MigrationVariants/MessagingOnly/", "20260915223219")
	];

	[Fact(DisplayName = "Four Init trios match fingerprint table sets")]
	public void FourInitTriosMatchFingerprintTableSets()
	{
		var solutionRoot = FindSolutionRoot();
		solutionRoot.Should().NotBeNull();

		var fullDir = Path.Combine(solutionRoot, "Monaco.Template.Backend.Application", "Persistence", "Migrations");
		var variantsRoot = Path.Combine(solutionRoot, "_TemplateArtifacts", "MigrationVariants");

		var full = ReadTrio(fullDir, "Full");
		var core = ReadTrio(Path.Combine(variantsRoot, "Core"), "Core");
		var filesOnly = ReadTrio(Path.Combine(variantsRoot, "FilesOnly"), "FilesOnly");
		var messagingOnly = ReadTrio(Path.Combine(variantsRoot, "MessagingOnly"), "MessagingOnly");

		new[] { full.Id, core.Id, filesOnly.Id, messagingOnly.Id }.Should().OnlyHaveUniqueItems();

		AssertFingerprint(full, ["Country", "Company", "File", "Product", "ProductPicture", "InboxState", "OutboxState", "OutboxMessage"], true, true, true);
		AssertFingerprint(core, ["Country", "Company"], true, false, false);
		AssertFingerprint(filesOnly, ["Country", "Company", "File", "Product", "ProductPicture"], true, true, false);
		AssertFingerprint(messagingOnly, ["Country", "Company", "InboxState", "OutboxState", "OutboxMessage"], true, false, true);
	}

	[Fact(DisplayName = "Template JSON routes one Init trio per fingerprint")]
	public void TemplateJsonRoutesOneInitTrioPerFingerprint()
	{
		var solutionRoot = FindSolutionRoot();
		solutionRoot.Should().NotBeNull();

		using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(solutionRoot!, ".template.config", "template.json")));
		var sources = template.RootElement.GetProperty("sources");
		sources.GetArrayLength().Should().Be(4);

		var primary = sources[0];
		primary.GetProperty("exclude")
			   .EnumerateArray()
			   .Select(element => element.GetString())
			   .Should()
			   .Contain("_TemplateArtifacts/**");

		var migrationsExclude = primary.GetProperty("modifiers")
									   .EnumerateArray()
									   .Single(element => element.GetProperty("condition").GetString() ==
														  "(!filesSupport || !massTransitIntegration)");
		migrationsExclude.GetProperty("exclude")
						 .EnumerateArray()
						 .Select(element => element.GetString())
						 .Should()
						 .Contain("Monaco.Template.Backend.Application/Persistence/Migrations/**");

		var fullDir = Path.Combine(solutionRoot!, "Monaco.Template.Backend.Application", "Persistence", "Migrations");
		var fullId = Path.GetFileName(Directory.GetFiles(fullDir, "*_Init.cs").Single())[..^"_Init.cs".Length];
		fullId.Should().Be("20260915220138");

		for (var index = 0; index < ReducedRoutes.Length; index++)
		{
			var (condition, source, id) = ReducedRoutes[index];
			var route = sources[index + 1];
			route.GetProperty("condition").GetString().Should().Be(condition);
			route.GetProperty("source").GetString().Should().Be(source);
			route.GetProperty("target")
				 .GetString()
				 .Should()
				 .Be("./Monaco.Template.Backend.Application/Persistence/Migrations/");

			var stagedDir = Path.Combine(solutionRoot!,
										 "_TemplateArtifacts",
										 "MigrationVariants",
										 source.TrimEnd('/')
											   .Split('/')
											   .Last());
			var stagedId = Path.GetFileName(Directory.GetFiles(stagedDir, "*_Init.cs").Single())[..^"_Init.cs".Length];
			stagedId.Should().Be(id);
		}
	}

	[Fact(DisplayName = "Design startup follows host shape")]
	public void DesignStartupFollowsHostShape()
	{
		var solutionRoot = FindSolutionRoot();
		solutionRoot.Should().NotBeNull();

		var api = File.ReadAllText(Path.Combine(solutionRoot!, "Monaco.Template.Backend.Api", "Monaco.Template.Backend.Api.csproj"));
		api.Should().Contain("Microsoft.EntityFrameworkCore.Design");
		api.Should().Contain("<PrivateAssets>all</PrivateAssets>");
		api.Should()
		   .Contain("<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>");

		var worker = File.ReadAllText(Path.Combine(solutionRoot!, "Monaco.Template.Backend.Worker", "Monaco.Template.Backend.Worker.csproj"));
		const string designPackage = "PackageReference Include=\"Microsoft.EntityFrameworkCore.Design\"";
		var designMatches = Regex.Matches(worker, Regex.Escape(designPackage));
		designMatches.Should().ContainSingle();

		var designBlockStart = worker.IndexOf("<!--#if (!apiService && workerService)-->", StringComparison.Ordinal);
		designBlockStart.Should().BeGreaterThanOrEqualTo(0);
		var designBlockEnd = worker.IndexOf("<!--#endif-->", designBlockStart, StringComparison.Ordinal);
		designBlockEnd.Should().BeGreaterThan(designBlockStart);
		var designBlock = worker[designBlockStart..designBlockEnd];
		designMatches[0].Index.Should().BeGreaterThan(designBlockStart);
		designMatches[0].Index.Should().BeLessThan(designBlockEnd);
		designBlock.Should().Contain(designPackage);
		designBlock.Should().Contain("<PrivateAssets>all</PrivateAssets>");
		designBlock.Should()
				   .Contain("<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>");

		var application = File.ReadAllText(Path.Combine(solutionRoot!,
														"Monaco.Template.Backend.Application",
														"Monaco.Template.Backend.Application.csproj"));
		application.Should().NotContain("Microsoft.EntityFrameworkCore.Design");

		foreach (var projectDir in new[]
				 {
					 "Monaco.Template.Backend.Api",
					 "Monaco.Template.Backend.Worker",
					 "Monaco.Template.Backend.Application",
					 "Monaco.Template.Backend.Domain",
					 "Monaco.Template.Backend.Messages",
					 "Monaco.Template.Backend.Common.Api",
					 "Monaco.Template.Backend.Common.Api.Application",
					 "Monaco.Template.Backend.Common.Application",
					 "Monaco.Template.Backend.Common.BlobStorage",
					 "Monaco.Template.Backend.Common.Domain",
					 "Monaco.Template.Backend.Common.Infrastructure",
					 "Monaco.Template.Backend.Common.Observability",
					 "Monaco.Template.Backend.Common.ApiGateway"
				 })
		{
			var root = Path.Combine(solutionRoot!, projectDir);
			if (!Directory.Exists(root))
				continue;

			foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
			{
				if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
					|| path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
					continue;

				File.ReadAllText(path).Should().NotContain("IDesignTimeDbContextFactory", because: path);
			}
		}
	}

	[Fact(DisplayName = "Write and message contracts omit stamp names")]
	public void WriteAndMessageContractsOmitStampNames()
	{
		var solutionRoot = FindSolutionRoot();
		solutionRoot.Should().NotBeNull();

		var sources =
			new[]
			{
				Path.Combine(solutionRoot, "Monaco.Template.Backend.Api", "DTOs", "CompanyCreateEditDto.cs"),
				Path.Combine(solutionRoot, "Monaco.Template.Backend.Api", "DTOs", "ProductCreateEditDto.cs"),
				Path.Combine(solutionRoot, "Monaco.Template.Backend.Messages", "V1", "ProductCreated.cs")
			};

		foreach (var path in sources)
		{
			File.Exists(path).Should().BeTrue(path);
			var text = File.ReadAllText(path);
			text.Should().NotContain("CreatedAtUtc");
			text.Should().NotContain("CreatedBy");
			text.Should().NotContain("ModifiedAtUtc");
			text.Should().NotContain("ModifiedBy");
		}
	}

	private static void AssertFingerprint(Trio trio,
										  string[] expectedTables,
										  bool companyStamps,
										  bool fileProductStamps,
										  bool outbox)
	{
		trio.Implementation.Should().NotContain("#if");
		trio.Designer.Should().NotContain("#if");
		trio.Snapshot.Should().NotContain("#if");
		trio.Implementation.Should().NotContain("DefaultValueSql");

		trio.Tables.Should().BeEquivalentTo(expectedTables);

		AssertStampPresence(trio.Implementation, "Company", companyStamps);
		AssertStampPresence(trio.Implementation, "File", fileProductStamps);
		AssertStampPresence(trio.Implementation, "Product", fileProductStamps);

		if (outbox)
		{
			trio.Tables.Should().Contain(["InboxState", "OutboxState", "OutboxMessage"]);
			AssertNoStampColumnsNearTable(trio.Implementation, "InboxState");
			AssertNoStampColumnsNearTable(trio.Implementation, "OutboxState");
			AssertNoStampColumnsNearTable(trio.Implementation, "OutboxMessage");
		}
		else
			trio.Tables.Should().NotContain(["InboxState", "OutboxState", "OutboxMessage"]);
	}

	private static void AssertStampPresence(string implementation, string table, bool expected)
	{
		var block = TableBlock(implementation, table);
		if (!expected)
		{
			block.Should().BeNull();
			return;
		}

		block.Should().NotBeNull();
		block.Should().Contain("CreatedAtUtc");
		block.Should().Contain("datetimeoffset(7)");
		block.Should().Contain("CreatedBy");
		block.Should().Contain("nvarchar(512)");
		block.Should().Contain("ModifiedAtUtc");
		block.Should().Contain("ModifiedBy");
	}

	private static void AssertNoStampColumnsNearTable(string implementation, string table)
	{
		var block = TableBlock(implementation, table);
		block.Should().NotBeNull();
		block.Should().NotContain("CreatedAtUtc");
		block.Should().NotContain("CreatedBy");
		block.Should().NotContain("ModifiedAtUtc");
		block.Should().NotContain("ModifiedBy");
	}

	private static string? TableBlock(string implementation, string table)
	{
		var marker = $"name: \"{table}\"";
		var start = implementation.IndexOf("CreateTable(", StringComparison.Ordinal);
		while (start >= 0)
		{
			var next = implementation.IndexOf("CreateTable(", start + 1, StringComparison.Ordinal);
			var end = next >= 0 ? next : implementation.Length;
			var block = implementation[start..end];
			if (block.Contains(marker, StringComparison.Ordinal))
				return block;
			start = next;
		}

		return null;
	}

	private static Trio ReadTrio(string directory, string fingerprint)
	{
		Directory.Exists(directory).Should().BeTrue(fingerprint);

		var init = Directory.GetFiles(directory, "*_Init.cs").Single();
		var designer = Directory.GetFiles(directory, "*_Init.Designer.cs").Single();
		var snapshot = Path.Combine(directory, "AppDbContextModelSnapshot.cs");
		File.Exists(snapshot).Should().BeTrue(fingerprint);

		var implementation = File.ReadAllText(init);
		var id = Path.GetFileName(init)[..^"_Init.cs".Length];

		return new Trio(id,
						implementation,
						File.ReadAllText(designer),
						File.ReadAllText(snapshot),
						[
							.. CreateTableName.Matches(implementation)
											  .Select(match => match.Groups["name"].Value)
											  .Distinct()
						]);
	}

	private static string? FindSolutionRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Monaco.Template.Backend.slnx")))
				return directory.FullName;
			directory = directory.Parent;
		}

		return null;
	}

	private sealed record Trio(string Id, string Implementation, string Designer, string Snapshot, string[] Tables);

	[GeneratedRegex("""
					CreateTable\(\s*name:\s*"(?<name>[^"]+)"
					""", RegexOptions.Compiled)]
	private static partial Regex CreateTableRegex();
}