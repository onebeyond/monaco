using System.Text.Json;

namespace Monaco.Template.Backend.Common.Observability.Tests;

internal static class TemplateDirectiveSource
{
	private const string IfDirective = "#if";
	private const string EndIfDirective = "#endif";

	internal static string If(string symbol) =>
		$"{IfDirective} ({symbol})";

	internal static string SolutionDirectory { get; } = FindSolutionDirectory();

	internal static string Read(string relativePath) =>
		File.ReadAllText(Path.Combine(SolutionDirectory, relativePath));

	internal static IReadOnlyList<string> ConditionalBlocks(string source, string symbol)
	{
		var open = If(symbol);
		const string close = EndIfDirective;
		var blocks = new List<string>();
		var start = 0;
		while (true)
		{
			var gate = source.IndexOf(open, start, StringComparison.Ordinal);
			if (gate < 0)
				return blocks;

			var end = source.IndexOf(close, gate, StringComparison.Ordinal);
			if (end < 0)
				throw new InvalidOperationException($"Unclosed {open} in source.");

			blocks.Add(source[gate..end]);
			start = end + close.Length;
		}
	}

	internal static void AssertGatedInvocations(string relativePath,
												string symbol,
												IReadOnlyList<string> invocations,
												IReadOnlyList<string> allowedProfiles,
												IReadOnlyList<string> forbiddenProfiles)
	{
		var source = Read(relativePath);
		var blocks = ConditionalBlocks(source, symbol);
		Assert.NotEmpty(blocks);

		var gated = string.Concat(blocks);
		var ungated = blocks.Aggregate(source, (current, block) => current.Replace(block, string.Empty, StringComparison.Ordinal));

		foreach (var invocation in invocations)
		{
			Assert.Contains(invocation, gated, StringComparison.Ordinal);
			Assert.DoesNotContain(invocation, ungated, StringComparison.Ordinal);
		}

		foreach (var profile in allowedProfiles)
			Assert.Contains($"ObservabilityHostProfile.{profile}", gated, StringComparison.Ordinal);

		foreach (var profile in forbiddenProfiles)
			Assert.DoesNotContain($"ObservabilityHostProfile.{profile}", gated, StringComparison.Ordinal);
	}

	internal static void AssertPathGatedBy(string relativePath, string symbol, string pathToken)
	{
		var source = Read(relativePath);
		var blocks = ConditionalBlocks(source, symbol);
		Assert.Contains(blocks, block => block.Contains(pathToken, StringComparison.Ordinal));
	}

	internal static void AssertDoesNotContainDirective(string relativePath, string symbol)
	{
		var source = Read(relativePath);
		Assert.DoesNotContain(If(symbol), source, StringComparison.Ordinal);
		Assert.DoesNotContain(If($"!{symbol}"), source, StringComparison.Ordinal);
	}

	internal static IReadOnlyList<string> UnconditionalExcludes()
	{
		using var document = JsonDocument.Parse(Read(Path.Combine(".template.config", "template.json")));
		return
		[
			.. document.RootElement
					   .GetProperty("sources")[0]
					   .GetProperty("exclude")
					   .EnumerateArray()
					   .Select(element => element.GetString())
					   .Where(value => value is not null)
					   .Cast<string>()
		];
	}

	internal static IReadOnlyList<string> ConditionalExcludes(string predicate)
	{
		using var document = JsonDocument.Parse(Read(Path.Combine(".template.config", "template.json")));
		return
		[
			.. document.RootElement
					   .GetProperty("sources")[0]
					   .GetProperty("modifiers")
					   .EnumerateArray()
					   .Where(modifier => modifier.TryGetProperty("condition", out var condition) &&
										  condition.GetString() == predicate)
					   .SelectMany(modifier => modifier.GetProperty("exclude")
													   .EnumerateArray())
					   .Select(element => element.GetString())
					   .Where(value => value is not null)
					   .Cast<string>()
		];
	}

	internal static bool HasConditionalModifier(string predicate)
	{
		using var document = JsonDocument.Parse(Read(Path.Combine(".template.config", "template.json")));
		return document.RootElement
					   .GetProperty("sources")[0]
					   .GetProperty("modifiers")
					   .EnumerateArray()
					   .Any(modifier => modifier.TryGetProperty("condition", out var condition) &&
										condition.GetString() == predicate);
	}

	internal static JsonDocument TemplateDocument() =>
		JsonDocument.Parse(Read(Path.Combine(".template.config", "template.json")));

	private static string FindSolutionDirectory()
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
			if (File.Exists(Path.Combine(directory.FullName, "Monaco.Template.Backend.slnx")))
				return directory.FullName;

		throw new DirectoryNotFoundException("Could not locate Monaco.Template.Backend.slnx from the test output directory.");
	}
}