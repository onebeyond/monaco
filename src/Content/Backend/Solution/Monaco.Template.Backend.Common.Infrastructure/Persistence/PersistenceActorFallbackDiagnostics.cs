using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Common.Infrastructure.Persistence;

public sealed class PersistenceActorFallbackDiagnostics
{
	private static readonly TimeSpan AggregateWindow = TimeSpan.FromMinutes(5);

	private readonly ILogger<PersistenceActorFallbackDiagnostics> _logger;
	private readonly TimeProvider _timeProvider;
	private readonly Lock _gate = new();

	private readonly Dictionary<(string Code, string HostRole), Window> _windows = [];

	public PersistenceActorFallbackDiagnostics(ILogger<PersistenceActorFallbackDiagnostics> logger,
											   TimeProvider timeProvider)
	{
		_logger = logger;
		_timeProvider = timeProvider;
	}

	public void Report(string code, string hostRole)
	{
		lock (_gate)
		{
			var now = _timeProvider.GetUtcNow();
			var key = (code, hostRole);
			if (!_windows.TryGetValue(key, out var window))
			{
				EmitFirst(code, hostRole);
				_windows[key] = new Window(now);
				return;
			}

			if (now - window.StartedAt < AggregateWindow)
			{
				window.SuppressedCount++;
				return;
			}

			if (window.SuppressedCount > 0)
				EmitAggregate(code, hostRole, window.SuppressedCount);

			EmitFirst(code, hostRole);
			_windows[key] = new Window(now);
		}
	}

	private void EmitFirst(string code, string hostRole) =>
		_logger.LogWarning("Persistence actor resolution failed. {code} {stage} {hostRole}",
						   code,
						   PersistenceActor.ResolutionStage,
						   hostRole);

	private void EmitAggregate(string code, string hostRole, int suppressedCount) =>
		_logger.LogWarning("Persistence actor resolution failures suppressed. {code} {hostRole} {suppressedCount}",
						   code,
						   hostRole,
						   suppressedCount);

	private sealed class Window
	{
		public Window(DateTimeOffset startedAt) =>
			StartedAt = startedAt;

		public DateTimeOffset StartedAt { get; }

		public int SuppressedCount { get; set; }
	}
}