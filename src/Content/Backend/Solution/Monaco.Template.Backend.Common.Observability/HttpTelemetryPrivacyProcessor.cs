using System.Diagnostics;
using OpenTelemetry;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed class HttpTelemetryPrivacyProcessor : BaseProcessor<Activity>
{
	public override void OnEnd(Activity data)
	{
		data.SetTag("url.query", null);
		data.SetTag("url.full", null);
		data.SetTag("http.url", null);
		data.SetTag("http.target", null);

		foreach (var tag in data.TagObjects
								.Where(tag => tag.Key.StartsWith("http.request.header.", StringComparison.Ordinal) ||
											  tag.Key.StartsWith("http.response.header.", StringComparison.Ordinal))
								.Select(tag => tag.Key)
								.ToArray())
			data.SetTag(tag, null);
	}
}