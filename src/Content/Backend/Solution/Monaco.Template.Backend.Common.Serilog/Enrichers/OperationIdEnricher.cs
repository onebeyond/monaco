using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Monaco.Template.Backend.Common.Serilog.Enrichers;

public class OperationIdEnricher : ILogEventEnricher
{
	public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
	{
		var activity = Activity.Current;

		switch (activity)
		{
			case null:
				return;
			case { Id: not null }:
				logEvent.AddPropertyIfAbsent(new LogEventProperty("operationId", new ScalarValue(activity.Id)));
				break;
			case { ParentId: not null }:
				logEvent.AddPropertyIfAbsent(new LogEventProperty("parentId", new ScalarValue(activity.ParentId)));
				break;
		}
	}
}