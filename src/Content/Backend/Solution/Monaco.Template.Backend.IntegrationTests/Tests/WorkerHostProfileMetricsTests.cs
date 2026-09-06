#if (workerService)
using AwesomeAssertions;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Domain.Model.Entities;
using System.Diagnostics.CodeAnalysis;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Observability")]
public sealed class WorkerHostProfileMetricsTests(AppFixture fixture) : IntegrationTest(fixture)
{
#if (apiService && auth)
	protected override bool RequiresAuthentication => false;
#endif

	[Fact(DisplayName = "Worker OTLP metrics carry applicable host meters without ASP.NET or company meters")]
	public async Task WorkerOtlpMetricsCarryApplicableHostMetersWithoutAspNetOrCompanyMeters()
	{
		await using var collector = await OtlpLoopback.StartAsync();
		await using var worker = Fixture.WorkerServiceFactory
										.GetCustomFactory(builder =>
														  {
															  builder.UseSetting(WebHostDefaults.ApplicationKey, "Monaco.Template.Backend.Worker");
															  builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(OtlpLoopback.ExporterConfiguration(collector.Endpoint)));
														  });
		await using var scope = worker.Services.CreateAsyncScope();
		_ = await scope.ServiceProvider
					   .GetRequiredService<AppDbContext>()
					   .Set<Country>()
					   .AsNoTracking()
					   .CountAsync();

		await collector.WaitUntilAsync(() => HasWorkerMeters(collector), 200);

		var meters = MetersForHost(collector, ".Worker");
		meters.Should().Contain(HostMetricNames.Runtime);
		meters.Should().Contain(meter => HostMetricNames.IsHttpClient(meter));
		meters.Should().Contain(meter => HostMetricNames.IsSqlClient(meter));
		meters.Should().NotContain(HostMetricNames.Application);
		meters.Should().NotContain(meter => HostMetricNames.IsAspNetCore(meter));
		meters.Should().NotContain(HostMetricNames.Authentication);
		meters.Should().NotContain(HostMetricNames.Authorization);
	}

	private static IReadOnlyCollection<string> MetersForHost(OtlpLoopback collector, string serviceSuffix)
	{
		var meters = collector.GetMeterNamesByServiceSuffix(serviceSuffix);
		return meters.Count > 0 ? meters : collector.GetMeterNames();
	}

	private static bool HasWorkerMeters(OtlpLoopback collector)
	{
		try
		{
			var meters = MetersForHost(collector, ".Worker");
			return meters.Contains(HostMetricNames.Runtime) &&
				   meters.Any(HostMetricNames.IsHttpClient) &&
				   meters.Any(HostMetricNames.IsSqlClient);
		}
		catch (InvalidProtocolBufferException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}
}
#endif