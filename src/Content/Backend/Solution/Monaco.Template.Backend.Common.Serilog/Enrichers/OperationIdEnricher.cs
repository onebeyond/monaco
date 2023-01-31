using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Monaco.Template.Backend.Common.Serilog.Enrichers;

public class OperationIdEnricher : ILogEventEnricher
{
	public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
	{
		var activity = Activity.Current;

		if (activity is null) return;

		if (activity.Id != null)
			logEvent.AddPropertyIfAbsent(new LogEventProperty("operationId", new ScalarValue(activity.Id)));
		if (activity.ParentId != null)
			logEvent.AddPropertyIfAbsent(new LogEventProperty("parentId", new ScalarValue(activity.ParentId)));
	}
}