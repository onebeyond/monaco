#if (massTransitIntegration)
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Domain.Tests.Factories;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Product", "Long-running process")]
public sealed class LongRunningProcessHandlerTests
{
	[Theory(DisplayName = "Completion log keeps EventId 2000")]
	[AutoDomainData]
	public async Task CompletionLogKeepsEventId(LongRunningProcess.Command command)
	{
		var logger = new CapturingLogger();
		var sut = new LongRunningProcess.Handler(logger);

		await sut.Handle(command, CancellationToken.None);

		var entry = logger.Entries.Should().ContainSingle().Which;
		entry.LogLevel.Should().Be(LogLevel.Information);
		entry.EventId.Should().Be(new EventId(2000, "LongRunningProcessCompleted"));
		entry.Exception.Should().BeNull();
		var fields = entry.State.ToDictionary(field => field.Key, field => field.Value);
		fields["{OriginalFormat}"].Should().Be("Long-running process command completed.");
		fields.Keys.Should().Equal("{OriginalFormat}");
	}

	private sealed class CapturingLogger : ILogger<LongRunningProcess.Handler>
	{
		internal List<CapturedLogEntry> Entries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel,
								EventId eventId,
								TState state,
								Exception? exception,
								Func<TState, Exception?, string> formatter) =>
			Entries.Add(new CapturedLogEntry(logLevel,
											 eventId,
											 exception,
											 state as IReadOnlyList<KeyValuePair<string, object?>> ?? []));
	}

	private sealed record CapturedLogEntry(LogLevel LogLevel,
										   EventId EventId,
										   Exception? Exception,
										   IReadOnlyList<KeyValuePair<string, object?>> State);
}
#endif