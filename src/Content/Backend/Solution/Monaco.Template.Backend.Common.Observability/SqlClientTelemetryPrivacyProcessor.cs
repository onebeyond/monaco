using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed class SqlClientTelemetryPrivacyProcessor(ObservabilityQueryTextMode queryTextMode,
														 ObservabilityHostProfile profile = ObservabilityHostProfile.Api,
														 ILogger? logger = null) : BaseProcessor<Activity>
{
	private const int MaximumQueryTextLength = 4 * 1024;

	public override void OnEnd(Activity data)
	{
		if (data.GetTagItem("db.system.name") is null &&
			data.GetTagItem("db.system") is null)
			return;

		data.SetTag("db.statement", null);
		data.SetTag("db.query.text", null);
		data.SetTag("db.connection_string", null);
		data.SetTag("db.user", null);

		foreach (var tag in data.TagObjects
								.Where(tag => tag.Key.StartsWith("db.query.parameter.", StringComparison.Ordinal))
								.Select(tag => tag.Key)
								.ToArray())
			data.SetTag(tag, null);

		data.SetStatus(data.Status);

		if (queryTextMode is ObservabilityQueryTextMode.SanitizedText)
			if (TryCreateSanitizedSummary(data.GetTagItem("db.operation.name") as string, out var summary))
				data.SetTag("db.query.text", summary);
			else if (logger is not null)
				ObservabilityConfigurationDiagnosticThrottle.Report(logger, ObservabilityConfigurationDiagnosticCodes.SqlSanitizationRejected, profile);
	}

	private static bool TryCreateSanitizedSummary(string? operation, out string? summary)
	{
		summary = null;
		if (string.IsNullOrWhiteSpace(operation) ||
			operation.Length > 32 ||
			operation.Any(character => !char.IsAsciiLetter(character)))
			return false;

		var normalizedOperation = operation.ToUpperInvariant();
		if (normalizedOperation is not ("SELECT" or "INSERT" or "UPDATE" or "DELETE" or "EXEC"))
			return false;

		summary = $"{normalizedOperation} statement";
		return summary.Length <= MaximumQueryTextLength;
	}
}