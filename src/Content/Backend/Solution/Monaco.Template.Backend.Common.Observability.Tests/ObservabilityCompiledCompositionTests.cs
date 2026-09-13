using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Monaco.Template.Backend.Common.Observability.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Application Services", "Observability")]
public sealed class ObservabilityCompiledCompositionTests
{
	private const string ApplicationMeterName = "Monaco.Template.Backend.Application";

	[Fact(DisplayName = "Public observability composition API exposes only host-builder entry points")]
	public void PublicObservabilityCompositionApiExposesOnlyHostBuilderEntryPoints()
	{
		var methods = typeof(ObservabilityHostBuilderExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
																.Where(method => method.Name.StartsWith("Add", StringComparison.Ordinal) &&
																				 method.Name.EndsWith("Observability", StringComparison.Ordinal))
																.OrderBy(method => method.Name)
																.ToArray();

		Assert.Equal([
						 "AddApiObservability",
						 "AddGatewayObservability",
						 "AddWorkerObservability"
					 ],
					 methods.Select(method => method.Name));
		Assert.All(methods,
				   method =>
				   {
					   Assert.Single(method.GetParameters());
					   Assert.Equal(typeof(IHostApplicationBuilder), method.GetParameters()[0].ParameterType);
					   Assert.DoesNotContain(method.GetParameters(), parameter => parameter.ParameterType == typeof(IConfiguration));
				   });

		typeof(ObservabilityHostBuilderExtensions).GetMethod("UseIdentityEnrichment", BindingFlags.Public | BindingFlags.Static)
												  .Should()
												  .NotBeNull();
		typeof(ObservabilityHostBuilderExtensions).GetMethod("UseIdentityEnrichment", BindingFlags.Public | BindingFlags.Static)!
												  .GetParameters()[0]
												  .ParameterType
												  .Should()
												  .Be(typeof(IApplicationBuilder));
	}

	[Fact(DisplayName = "Profile hash sets contain only compiled instrumentation members and omit YARP, MassTransit, and the Application meter")]
	public void ProfileHashSetsContainOnlyCompiledInstrumentationMembers()
	{
		ObservabilityProfileRegistration.ApiInstrumentations
										.Should()
										.BeEquivalentTo([
															ObservabilityInstrumentation.Runtime,
															ObservabilityInstrumentation.AspNetCore,
															ObservabilityInstrumentation.Http,
															ObservabilityInstrumentation.SqlClient
														]);
		ObservabilityProfileRegistration.WorkerInstrumentations
										.Should()
										.BeEquivalentTo([
															ObservabilityInstrumentation.Runtime,
															ObservabilityInstrumentation.Http,
															ObservabilityInstrumentation.SqlClient
														]);
		ObservabilityProfileRegistration.GatewayInstrumentations
										.Should()
										.BeEquivalentTo([
															ObservabilityInstrumentation.Runtime,
															ObservabilityInstrumentation.AspNetCore,
															ObservabilityInstrumentation.Http
														]);

		var names = Enum.GetNames<ObservabilityInstrumentation>();
		names.Should()
			 .NotContain(name => name.Contains("Yarp", StringComparison.OrdinalIgnoreCase) ||
								 name.Contains("MassTransit", StringComparison.OrdinalIgnoreCase) ||
								 name.Contains("Application", StringComparison.OrdinalIgnoreCase));
	}

	[Fact(DisplayName = "Shared composition uses one unified OTLP path, logging options, and API-only Application meter")]
	public void SharedCompositionUsesUnifiedOtlpLoggingOptionsAndApiOnlyApplicationMeter()
	{
		var logging = ProfileComposition.LoggingOptions();
		logging.IncludeFormattedMessage
			   .Should()
			   .BeTrue();
		logging.IncludeScopes
			   .Should()
			   .BeTrue();
		logging.ParseStateValues
			   .Should()
			   .BeTrue();

		var referenced = typeof(ObservabilityHostBuilderExtensions).Assembly
																   .GetReferencedAssemblies()
																   .Select(assembly => assembly.Name)
																   .ToArray();
		referenced.Should()
				  .Contain("OpenTelemetry.Exporter.OpenTelemetryProtocol");
		referenced.Where(name => name is not null)
				  .Should()
				  .NotContain(name => name!.Contains("Jaeger", StringComparison.OrdinalIgnoreCase) ||
									  name.Contains("Prometheus", StringComparison.OrdinalIgnoreCase) ||
									  name.Contains("InMemory", StringComparison.OrdinalIgnoreCase));

		typeof(ObservabilityHostBuilderExtensions).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
												  .Select(method => method.Name)
												  .Should()
												  .NotContain("AddOtlpExporter")
												  .And
												  .NotContain("AddProcessor")
												  .And
												  .NotContain("AddView");

		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Api, "System.Runtime")
						  .Should()
						  .BeTrue();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Worker, "System.Runtime")
						  .Should()
						  .BeTrue();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Gateway, "System.Runtime")
						  .Should()
						  .BeTrue();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Api, ApplicationMeterName)
						  .Should()
						  .BeTrue();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Worker, ApplicationMeterName)
						  .Should()
						  .BeFalse();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Gateway, ApplicationMeterName)
						  .Should()
						  .BeFalse();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Api, "Yarp.ReverseProxy")
						  .Should()
						  .BeFalse();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Gateway, "Yarp.ReverseProxy")
						  .Should()
						  .BeFalse();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Api, "Azure.Storage.Blobs")
						  .Should()
						  .BeFalse();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Worker, "Azure.Storage.Blobs")
						  .Should()
						  .BeFalse();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Gateway, "Azure.Storage.Blobs")
						  .Should()
						  .BeFalse();
	}

	[Fact(DisplayName = "Live tracing and metrics subscribe through compiled profile composition")]
	public void LiveTracingAndMetricsSubscribeThroughCompiledProfileComposition()
	{
		ProfileComposition.ListensToSource(ObservabilityHostProfile.Gateway, "Yarp.ReverseProxy")
						  .Should()
						  .BeTrue();
		ProfileComposition.ListensToSource(ObservabilityHostProfile.Api, "Yarp.ReverseProxy")
						  .Should()
						  .BeFalse();
		ProfileComposition.ListensToSource(ObservabilityHostProfile.Worker, "Yarp.ReverseProxy")
						  .Should()
						  .BeFalse();

		ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																{
																	AssertLiveSources(builder => builder.AddApiObservability(),
																					  aspNetCore: true,
																					  yarp: false,
																					  sql: true,
																					  massTransit: MassTransitCompiled);
																	AssertLiveSources(builder => builder.AddWorkerObservability(),
																					  aspNetCore: false,
																					  yarp: false,
																					  sql: true,
																					  massTransit: MassTransitCompiled);
																	AssertLiveSources(builder => builder.AddGatewayObservability(),
																					  aspNetCore: true,
																					  yarp: true,
																					  sql: false,
																					  massTransit: false);
																});
	}

	[Fact(DisplayName = "API and Worker subscribe to native MassTransit when messaging compiles")]
	public void ApiAndWorkerSubscribeToNativeMassTransitWhenMessagingCompiles()
	{
		ProfileComposition.ListensToSource(ObservabilityHostProfile.Api, "MassTransit")
						  .Should()
						  .Be(MassTransitCompiled);
		ProfileComposition.ListensToSource(ObservabilityHostProfile.Worker, "MassTransit")
						  .Should()
						  .Be(MassTransitCompiled);
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Api, "MassTransit")
						  .Should()
						  .Be(MassTransitCompiled);
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Worker, "MassTransit")
						  .Should()
						  .Be(MassTransitCompiled);
		ProfileComposition.ListensToSource(ObservabilityHostProfile.Gateway, "MassTransit")
						  .Should()
						  .BeFalse();
		ProfileComposition.ListensToMeter(ObservabilityHostProfile.Gateway, "MassTransit")
						  .Should()
						  .BeFalse();
	}

	[Fact(DisplayName = "Shared observability composition contains no Monaco telemetry governance surface")]
	public void SharedObservabilityCompositionContainsNoMonacoTelemetryGovernanceSurface()
	{
		var assembly = typeof(ObservabilityHostBuilderExtensions).Assembly;
		var typeNames = assembly.GetTypes()
								.Select(type => type.Name)
								.ToArray();
		var memberNames = assembly.GetTypes()
								  .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
								  .Select(member => member.Name)
								  .ToArray();

		typeNames.Should()
				 .NotContain(name => name.Contains("Processor", StringComparison.OrdinalIgnoreCase) ||
									 name.Contains("Adapter", StringComparison.OrdinalIgnoreCase) ||
									 name.Contains("View", StringComparison.OrdinalIgnoreCase) ||
									 name.Contains("Sanitiz", StringComparison.OrdinalIgnoreCase) ||
									 name.Contains("Redact", StringComparison.OrdinalIgnoreCase) ||
									 name.Contains("Hmac", StringComparison.OrdinalIgnoreCase) ||
									 name.Contains("Preflight", StringComparison.OrdinalIgnoreCase) ||
									 name.Contains("SecretProvider", StringComparison.OrdinalIgnoreCase));
		memberNames.Should()
				   .NotContain("AddProcessor")
				   .And
				   .NotContain("AddView")
				   .And
				   .NotContain("FilterHttpRequestMessage")
				   .And
				   .NotContain("MetricStreamConfiguration")
				   .And
				   .NotContain("ForceFlush")
				   .And
				   .NotContain("SetMissing")
				   .And
				   .NotContain("ApplyAdoptedDefaults")
				   .And
				   .NotContain("AddMassTransitInstrumentation")
				   .And
				   .NotContain("ConfigureMassTransit");
		typeof(ObservabilityHostBuilderExtensions).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
												  .Select(method => method.Name)
												  .Should()
												  .NotContain("IHostedService")
												  .And
												  .NotContain(name => name.Contains("HostedService", StringComparison.Ordinal));
	}

	[Fact(DisplayName = "Boundary exception handler is an ordinary HTTP handler without Activity mutation")]
	public void BoundaryExceptionHandlerIsOrdinaryHttpHandlerWithoutActivityMutation()
	{
		typeof(BoundaryExceptionHandler).IsPublic
										.Should()
										.BeTrue();
		typeof(BoundaryExceptionHandler).IsSealed
										.Should()
										.BeTrue();
		typeof(IExceptionHandler).IsAssignableFrom(typeof(BoundaryExceptionHandler))
								 .Should()
								 .BeTrue();
		typeof(BoundaryExceptionHandler).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
										.Select(member => member.Name)
										.Should()
										.NotContain(name => name.Contains("Activity", StringComparison.Ordinal));
	}

	[Fact(DisplayName = "Identity enrichment middleware emits claim events only to the entry span and logging scope")]
	public void IdentityEnrichmentEmitsClaimEventsOnlyToEntrySpanAndLoggingScope()
	{
		var middleware = typeof(IdentityEnrichmentMiddleware);
		var members = middleware.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
								.Select(member => member.Name)
								.ToArray();

		members.Should()
			   .Contain("InvokeAsync");
		members.Should()
			   .NotContain("Baggage")
			   .And
			   .NotContain("AddBaggage")
			   .And
			   .NotContain("RecordObservable")
			   .And
			   .NotContain("StartActivity")
			   .And
			   .NotContain(name => name.Contains("Hmac", StringComparison.OrdinalIgnoreCase))
			   .And
			   .NotContain("IdentityMode");

		var constants = middleware.GetFields(BindingFlags.NonPublic | BindingFlags.Static)
								  .Select(field => field.GetValue(null)
														?.ToString())
								  .OfType<string>()
								  .ToArray();
		constants.Should()
				 .Contain([
							  "user.id",
							  "user.claims",
							  "user.claim",
							  "user.claim.type",
							  "user.claim.value",
							  "user.claim.value_type",
							  "user.claim.issuer",
							  "user.claim.original_issuer",
							  "sub"
						  ]);
	}

	private static bool MassTransitCompiled =>
#if (massTransitIntegration)
		true;
#else
		false;
#endif

	private static void AssertLiveSources(Action<IHostApplicationBuilder> addProfile,
										  bool aspNetCore,
										  bool yarp,
										  bool sql,
										  bool massTransit)
	{
		using var host = ObservabilityTestEnvironment.BuildHost(addProfile);
		_ = host.Services.GetRequiredService<TracerProvider>();

		SourceIsListened("System.Net.Http")
			.Should()
			.BeTrue();
		SourceIsListened("Microsoft.AspNetCore")
			.Should()
			.Be(aspNetCore);
		SourceIsListened("Yarp.ReverseProxy")
			.Should()
			.Be(yarp);
		AnySourceListened("Microsoft.Data.SqlClient", "OpenTelemetry.Instrumentation.SqlClient", "System.Data.SqlClient")
			.Should()
			.Be(sql);
		SourceIsListened("MassTransit")
			.Should()
			.Be(massTransit);
	}

	private static bool SourceIsListened(string name)
	{
		using var source = new ActivitySource(name);
		return source.HasListeners();
	}

	private static bool AnySourceListened(params string[] names) =>
		names.Any(SourceIsListened);

	private static class ProfileComposition
	{
		private static readonly MethodInfo ConfigureTracingMethod = RequireMethod("ConfigureTracing");
		private static readonly MethodInfo ConfigureLoggingMethod = RequireMethod("ConfigureLogging");

		internal static OpenTelemetryLoggerOptions LoggingOptions()
		{
			var options = new OpenTelemetryLoggerOptions();
			ConfigureLoggingMethod.Invoke(null,
										  [
											  options
										  ]);
			return options;
		}

		internal static bool ListensToSource(ObservabilityHostProfile profile, string name)
		{
			var builder = Sdk.CreateTracerProviderBuilder();
			ConfigureTracingMethod.Invoke(null,
										  [
											  builder, profile
										  ]);
			using var provider = builder.Build();
			using var source = new ActivitySource(name);
			return source.HasListeners();
		}

		internal static bool ListensToMeter(ObservabilityHostProfile profile, string name)
		{
			var listened = false;
			ObservabilityTestEnvironment.WithClearedOtelEnvironment(() =>
																	{
																		using var host = ObservabilityTestEnvironment.BuildHost(builder =>
																																{
																																	switch (profile)
																																	{
																																		case ObservabilityHostProfile.Api:
																																			builder.AddApiObservability();
																																			break;
																																		case ObservabilityHostProfile.Worker:
																																			builder.AddWorkerObservability();
																																			break;
																																		case ObservabilityHostProfile.Gateway:
																																			builder.AddGatewayObservability();
																																			break;
																																		default: throw new ArgumentOutOfRangeException(nameof(profile), profile, null);
																																	}
																																});
																		_ = host.Services.GetRequiredService<MeterProvider>();
																		using var listener = new MeterListener();
																		listener.Start();
																		using var meter = new Meter(name);
																		listened = meter.CreateCounter<long>("observability.probe")
																						.Enabled;
																	});
			return listened;
		}

		private static MethodInfo RequireMethod(string name) =>
			typeof(ObservabilityHostBuilderExtensions).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException($"{name} must remain a compiled composition method.");
	}
}