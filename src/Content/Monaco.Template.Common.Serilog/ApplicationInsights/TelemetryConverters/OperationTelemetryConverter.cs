using Microsoft.ApplicationInsights.Channel;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

namespace Monaco.Template.Common.Serilog.ApplicationInsights.TelemetryConverters;

public class OperationTelemetryConverter : TraceTelemetryConverter
{
    private const string OperationId = "operationId";
    private const string ParentId = "parentId";
    private const string UserId = "userId";
    private const string UserName = "userName";

    public override IEnumerable<ITelemetry> Convert(LogEvent logEvent, IFormatProvider formatProvider)
    {
        foreach (var telemetry in base.Convert(logEvent, formatProvider))
        {
            if (TryGetScalarProperty(logEvent, OperationId, out var operationId))
                telemetry.Context.Operation.Id = operationId!.ToString();

            if (TryGetScalarProperty(logEvent, ParentId, out var parentId))
                telemetry.Context.Operation.ParentId = parentId!.ToString();

            if (TryGetScalarProperty(logEvent, UserId, out var userId))
                telemetry.Context.User.Id = userId!.ToString();

            if (TryGetScalarProperty(logEvent, UserName, out var username))
                telemetry.Context.User.AccountId = username!.ToString();

            yield return telemetry;
        }
    }

    private static bool TryGetScalarProperty(LogEvent logEvent, string propertyName, out object? value)
    {
        var hasScalarValue = logEvent.Properties.TryGetValue(propertyName, out var someValue) &&
							 someValue is ScalarValue;

        value = hasScalarValue ? ((ScalarValue)someValue!).Value : default;

        return hasScalarValue;
    }
}