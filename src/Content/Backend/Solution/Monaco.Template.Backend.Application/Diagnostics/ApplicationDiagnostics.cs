using System.Diagnostics.Metrics;

namespace Monaco.Template.Backend.Application.Diagnostics;

internal static class ApplicationDiagnostics
{
	internal const string MeterName = "Monaco.Template.Backend.Application";

	private static readonly Meter Meter = new(MeterName);

	internal static readonly Counter<long> SuccessfulCompanyCreations = Meter.CreateCounter<long>("company.successful_creations",
																								  "{company}",
																								  "The number of companies successfully created after the Company unit of work commits.");
}