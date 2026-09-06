#if (apiService)
using AutoFixture.Xunit2;
using AwesomeAssertions;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.IntegrationTests.Apis;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Observability")]
public sealed class ApiHostProfileMetricsTests(AppFixture fixture) : IntegrationTest(fixture)
{
#if (apiService && auth)
	protected override bool RequiresAuthentication => true;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await RunScriptAsync(@"Scripts\Companies.sql");
#if (auth)
		await SetupAccessToken([Auth.Auth.Roles.Administrator]);
#endif
	}

	[Theory(DisplayName = "API OTLP metrics carry applicable host meters as aggregate supporting context")]
	[AutoData]
	public async Task ApiOtlpMetricsCarryApplicableHostMetersAsAggregateSupportingContext(string companyName, string email)
	{
		await using var collector = await OtlpLoopback.StartAsync();
		await using var factory = Fixture.WebAppFactory
										 .GetCustomFactory(builder =>
														   {
															   builder.UseSetting(WebHostDefaults.ApplicationKey, "Monaco.Template.Backend.Api");
															   builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(OtlpLoopback.ExporterConfiguration(collector.Endpoint)));
														   });
		var api = GetApi<ICompaniesApi>(factory);
		var company = new CompanyCreateEditDto($"{companyName}-{Guid.NewGuid():N}",
											   $"{Guid.NewGuid():N}@{email.Split('@').LastOrDefault() ?? "example.test"}",
											   null,
											   null,
											   null,
											   null,
											   null,
											   null);

		var response = await api.Create(company);

		response.StatusCode.Should().Be(HttpStatusCode.Created);
		await collector.WaitUntilAsync(() => HasApiMeters(collector), 200);

		var meters = MetersForHost(collector, ".Api");
		meters.Should().Contain(HostMetricNames.Runtime);
		meters.Should().Contain(meter => HostMetricNames.IsAspNetCore(meter));
		meters.Should().Contain(meter => HostMetricNames.IsHttpClient(meter));
		meters.Should().Contain(meter => HostMetricNames.IsSqlClient(meter));
		meters.Should().Contain(HostMetricNames.Application);
#if (auth)
		meters.Should().Contain(HostMetricNames.Authentication);
		meters.Should().Contain(HostMetricNames.Authorization);
#endif

		collector.GetSpans()
				 .Should()
				 .Contain(span => span.ScopeName == "Microsoft.AspNetCore");
		collector.GetSpans()
				 .Should()
				 .NotContain(span => span.ScopeName == HostMetricNames.Application);
		collector.GetSpans()
				 .SelectMany(span => span.Attributes)
				 .Should()
				 .NotContain(attribute => attribute.Key.Contains("company.successful_creations", StringComparison.Ordinal));
	}

	private static IReadOnlyCollection<string> MetersForHost(OtlpLoopback collector, string serviceSuffix)
	{
		var meters = collector.GetMeterNamesByServiceSuffix(serviceSuffix);
		return meters.Count > 0 ? meters : collector.GetMeterNames();
	}

	private static bool HasApiMeters(OtlpLoopback collector)
	{
		try
		{
			var meters = MetersForHost(collector, ".Api");
			return meters.Contains(HostMetricNames.Runtime) &&
				   meters.Any(HostMetricNames.IsAspNetCore) &&
				   meters.Any(HostMetricNames.IsHttpClient) &&
				   meters.Any(HostMetricNames.IsSqlClient) &&
				   meters.Contains(HostMetricNames.Application) &&
#if (auth)
				   collector.GetSpans().Any(span => span.ScopeName == "Microsoft.AspNetCore") &&
				   meters.Contains(HostMetricNames.Authentication) &&
				   meters.Contains(HostMetricNames.Authorization);
#else
				   collector.GetSpans().Any(span => span.ScopeName == "Microsoft.AspNetCore");
#endif
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