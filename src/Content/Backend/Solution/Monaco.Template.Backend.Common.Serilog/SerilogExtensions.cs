using Serilog.Configuration;
using Serilog;
using Monaco.Template.Backend.Common.Serilog.Enrichers;

namespace Monaco.Template.Backend.Common.Serilog;

public static class SerilogExtensions
{
	public static LoggerConfiguration WithOperationId(this LoggerEnrichmentConfiguration enrichConfiguration)
	{
		ArgumentNullException.ThrowIfNull(enrichConfiguration, nameof(enrichConfiguration));

		return enrichConfiguration.With<OperationIdEnricher>();
	}
}