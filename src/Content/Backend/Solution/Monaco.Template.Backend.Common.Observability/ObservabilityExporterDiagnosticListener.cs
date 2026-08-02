using System.Diagnostics.Tracing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Common.Observability;

internal sealed class ObservabilityExporterDiagnosticListener(ObservabilityProfileRegistration profile,
															  ILogger<ObservabilityExporterDiagnosticListener> logger) : IHostedService, IDisposable
{
	private readonly OtlpExporterFailureListener _listener = new();

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_listener.Start(() => ObservabilityConfigurationDiagnosticThrottle.Report(logger,
																				  ObservabilityConfigurationDiagnosticCodes.OtlpExportFailure,
																				  profile.Profile));
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		Dispose();
		return Task.CompletedTask;
	}

	public void Dispose() =>
		_listener.Dispose();
}

internal sealed class OtlpExporterFailureListener : EventListener
{
	private const string ExporterEventSourceName = "OpenTelemetry-Exporter-OpenTelemetryProtocol";
	private readonly string _exporterEventSourceName;

	private Action? _reportFailure;

	internal OtlpExporterFailureListener(string? exporterEventSourceName = null) =>
		_exporterEventSourceName = exporterEventSourceName ?? ExporterEventSourceName;

	internal void Start(Action reportFailure)
	{
		_reportFailure = reportFailure;

		foreach (var source in EventSource.GetSources())
			EnableExporterEvents(source);
	}

	protected override void OnEventSourceCreated(EventSource eventSource) =>
		EnableExporterEvents(eventSource);

	protected override void OnEventWritten(EventWrittenEventArgs eventData)
	{
		if (eventData.Level == EventLevel.Error)
			_reportFailure?.Invoke();
	}

	private void EnableExporterEvents(EventSource eventSource)
	{
		if (eventSource.Name == _exporterEventSourceName)
			EnableEvents(eventSource, EventLevel.Error, EventKeywords.All);
	}
}