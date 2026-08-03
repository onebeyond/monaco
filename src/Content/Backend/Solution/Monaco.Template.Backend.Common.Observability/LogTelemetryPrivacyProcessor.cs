using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed class LogTelemetryPrivacyProcessor : BaseProcessor<LogRecord>
{
	public override void OnEnd(LogRecord data)
	{
		data.Attributes = null;
		data.Body = null;
		data.Exception = null;
		data.FormattedMessage = null;
	}
}