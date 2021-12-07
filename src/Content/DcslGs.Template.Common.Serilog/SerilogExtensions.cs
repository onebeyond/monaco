using Serilog.Configuration;
using Serilog;
using DcslGs.Template.Common.Serilog.Enrichers;

namespace DcslGs.Template.Common.Serilog;

public static class SerilogExtensions
{
    public static LoggerConfiguration WithOperationId(this LoggerEnrichmentConfiguration enrichConfiguration)
    {
        if (enrichConfiguration is null) throw new ArgumentNullException(nameof(enrichConfiguration));

        return enrichConfiguration.With<OperationIdEnricher>();
    }
}