using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.Sinks.ApplicationInsights.TelemetryConverters;

namespace DcslGs.Template.Common.Serilog.ApplicationInsights.TelemetryConverters;

public class AuditEventTelemetryConverter : TelemetryConverterBase
{
    private const string OperationId = "operationId";
    private const string ParentId = "parentId";
    private const string UserId = "userId";
    private const string UserName = "userName";

    public override IEnumerable<ITelemetry> Convert(LogEvent logEvent, IFormatProvider formatProvider)
    {
        if (logEvent == null)
            throw new ArgumentNullException(nameof(logEvent));

        var telemetry = new EventTelemetry("Audit Trail")
                        {
                            Timestamp = logEvent.Timestamp
                        };
			
        ForwardPropertiesToTelemetryProperties(logEvent, telemetry, formatProvider, false, true, false);

        if (TryGetScalarProperty(logEvent, OperationId, out var operationId))
        {
            telemetry.Context.Operation.Id = operationId.ToString();
            telemetry.Name = $"Audit Trail for OperationId: {operationId.ToString().Trim('\"')}";
        }
			
        if (TryGetScalarProperty(logEvent, ParentId, out var parentId))
            telemetry.Context.Operation.ParentId = parentId.ToString();

        if (TryGetScalarProperty(logEvent, UserId, out var userId))
            telemetry.Context.User.Id = userId.ToString();

        if (TryGetScalarProperty(logEvent, UserName, out var username))
            telemetry.Context.User.AccountId = username.ToString();

        yield return telemetry;
    }

    private bool TryGetScalarProperty(LogEvent logEvent, string propertyName, out object value)
    {
        var hasScalarValue = logEvent.Properties.TryGetValue(propertyName, out var someValue) &&
                             someValue is ScalarValue;

        value = hasScalarValue ? ((ScalarValue)someValue).Value : default;

        return hasScalarValue;
    }
}