#if (apiGateway)
using AwesomeAssertions;
using Google.Protobuf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using GatewayProgram = Monaco.Template.Backend.Common.ApiGateway.Program;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Trait("Integration Tests", "Observability")]
public sealed class GatewayHostProfileMetricsTests
{
	[Fact(DisplayName = "Gateway OTLP metrics carry HTTP host meters without SQL, messaging, blob, or company meters")]
	public async Task GatewayOtlpMetricsCarryHttpHostMetersWithoutSqlMessagingBlobOrCompanyMeters()
	{
		await using var collector = await OtlpLoopback.StartAsync();
		await using var downstream = await DownstreamApi.StartAsync();
		await using var factory = new WebApplicationFactory<GatewayProgram>()
			.WithWebHostBuilder(builder =>
								{
									builder.UseSetting(WebHostDefaults.ApplicationKey, "Monaco.Template.Backend.Common.ApiGateway");
									builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>(OtlpLoopback.ExporterConfiguration(collector.Endpoint))
																																{
																																	["ReverseProxy:Clusters:api:Destinations:Template.Api:Address"] = downstream.Endpoint
																																}));
									builder.ConfigureServices(services => services.AddAuthorization(options =>
																									{
																										options.AddPolicy("files:write", policy => policy.RequireAssertion(_ => true));
																										options.AddPolicy("products:write", policy => policy.RequireAssertion(_ => true));
																									})
																				  .PostConfigure<AuthorizationOptions>(options => options.DefaultPolicy = new AuthorizationPolicyBuilder()
																																						  .RequireAssertion(_ => true).Build()));
								});
		using var client = factory.CreateDefaultClient(new UriBuilder(factory.Server.BaseAddress) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri);

		var response = await client.GetAsync("/api/observability-test");

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		await collector.WaitUntilAsync(() => HasGatewayMeters(collector), 200);

		var meters = collector.GetMeterNames("Monaco.Template.Backend.Common.ApiGateway");
		meters.Should().Contain(HostMetricNames.Runtime);
		meters.Should().Contain(name => HostMetricNames.IsAspNetCore(name));
		meters.Should().Contain(name => HostMetricNames.IsHttpClient(name));
#if (auth)
		meters.Should().Contain(HostMetricNames.Authentication);
		meters.Should().Contain(HostMetricNames.Authorization);
#endif
		meters.Should().NotContain(HostMetricNames.Application);
		meters.Should().NotContain(HostMetricNames.MassTransit);
		meters.Should().NotContain(name => HostMetricNames.IsSqlClient(name));
		meters.Should().NotContain(name => HostMetricNames.IsBlobOrYarp(name));
	}

	private static bool HasGatewayMeters(OtlpLoopback collector)
	{
		try
		{
			var meters = collector.GetMeterNames("Monaco.Template.Backend.Common.ApiGateway");
			return meters.Contains(HostMetricNames.Runtime) &&
				   meters.Any(HostMetricNames.IsAspNetCore) &&
				   meters.Any(HostMetricNames.IsHttpClient) &&
#if (auth)
				   meters.Contains(HostMetricNames.Authentication) &&
				   meters.Contains(HostMetricNames.Authorization) &&
#endif
				   !meters.Contains(HostMetricNames.Application) &&
				   !meters.Contains(HostMetricNames.MassTransit) &&
				   !meters.Any(HostMetricNames.IsSqlClient) &&
				   !meters.Any(HostMetricNames.IsBlobOrYarp);
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

	private sealed class DownstreamApi(WebApplication application) : IAsyncDisposable
	{
		internal string Endpoint => application.Urls.Single();

		internal static async Task<DownstreamApi> StartAsync()
		{
			var builder = WebApplication.CreateBuilder();
			builder.WebHost.UseUrls("http://127.0.0.1:0");
			var application = builder.Build();
			application.MapGet("/api/observability-test", Results.NoContent);
			await application.StartAsync();
			return new DownstreamApi(application);
		}

		public ValueTask DisposeAsync() => application.DisposeAsync();
	}
}
#endif