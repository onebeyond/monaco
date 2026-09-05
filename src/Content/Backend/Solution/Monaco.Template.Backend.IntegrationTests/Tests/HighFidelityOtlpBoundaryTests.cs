#if (apiService)
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Google.Protobuf;
#if (auth)
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
#endif
using Microsoft.Extensions.Configuration;
using Monaco.Template.Backend.IntegrationTests.Apis;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Observability")]
public sealed class HighFidelityOtlpBoundaryTests(AppFixture fixture) : IntegrationTest(fixture)
{
#if (apiService && auth)
	protected override bool RequiresAuthentication => true;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
#if (auth)
		await SetupAccessToken([]);
#endif
	}

#if (auth)
	[Fact(DisplayName = "Authenticated API export decodes raw identity, claim events, matching log claims, and native HTTP context")]
	public async Task AuthenticatedApiExportDecodesIdentityClaimEventsMatchingLogsAndNativeHttpContext()
	{
		var expectedClaims = ReadAccessTokenClaims();
		var subject = expectedClaims.First(claim => claim.Type == "sub" && !string.IsNullOrEmpty(claim.Value)).Value;
		await using var collector = await OtlpLoopback.StartAsync();
		await using var factory = Fixture.WebAppFactory
										 .GetCustomFactory(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(OtlpLoopback.ExporterConfiguration(collector.Endpoint))));
		var api = GetApi<ICountriesApi>(factory);

		var response = await api.Query();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		await collector.WaitUntilAsync(() => HasDecodedIdentityExport(collector, subject));
		await collector.WaitUntilSettledWithoutSelfTrafficAsync();
		collector.HasPath("/v1/logs").Should().BeTrue();
		collector.HasPath("/v1/traces").Should().BeTrue();
		collector.HasPath("/v1/metrics").Should().BeTrue();
		collector.GetMetrics().Should().NotBeEmpty();
		collector.HasHttpClientSpanTargetingEndpoint().Should().BeFalse();

		var entry = collector.GetSpans()
							 .Should()
							 .Contain(span => span.ScopeName == "Microsoft.AspNetCore" && span.GetAttribute("user.id") == subject)
							 .Which;
		entry.TraceId.Should().NotBeNullOrEmpty();
		entry.GetAttribute("user.id").Should().Be(subject);
		entry.GetAttribute("http.request.method").Should().NotBeNullOrWhiteSpace();
		entry.GetAttribute("http.response.status_code").Should().Be("200");
		(entry.GetAttribute("url.path") ?? entry.GetAttribute("http.route")).Should().NotBeNullOrWhiteSpace();

		var claimEvents = entry.Events.Where(@event => @event.Name == "user.claim").ToArray();
		claimEvents.Should().HaveCount(expectedClaims.Count);
		foreach (var (claim, claimEvent) in expectedClaims.Zip(claimEvents))
		{
			claimEvent.Attributes.Select(attribute => attribute.Key)
					  .Should()
					  .Equal("user.claim.type", "user.claim.value", "user.claim.value_type", "user.claim.issuer", "user.claim.original_issuer");
			claimEvent.GetAttribute("user.claim.type").Should().Be(claim.Type);
			claimEvent.GetAttribute("user.claim.value").Should().Be(claim.Value);
			claimEvent.GetAttribute("user.claim.value_type").Should().Be(claim.ValueType);
			claimEvent.GetAttribute("user.claim.issuer").Should().Be(claim.Issuer);
			claimEvent.GetAttribute("user.claim.original_issuer").Should().Be(claim.OriginalIssuer);
		}

		var logs = collector.GetLogs()
							.Where(record => record.TraceId == entry.TraceId && record.GetAttribute("user.claims") != null)
							.ToArray();
		logs.Should().NotBeEmpty();
		logs.Should().OnlyContain(record => record.GetAttribute("user.id") == subject);
		AssertClaimEventsMatchLog(claimEvents, logs);
	}

#endif
	[Fact(DisplayName = "Per-signal OTLP endpoints send traces to a separate receiver while logs and metrics use the common endpoint")]
	public async Task PerSignalOtlpEndpointsSendTracesToASeparateReceiver()
	{
		await using var common = await OtlpLoopback.StartAsync();
		await using var traces = await OtlpLoopback.StartAsync();
		await using var factory = Fixture.WebAppFactory
										 .GetCustomFactory(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(OtlpLoopback.ExporterConfiguration(common.Endpoint, traces.Endpoint))));
		var api = GetApi<ICountriesApi>(factory);

		var response = await api.Query();

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		await traces.WaitUntilAsync(() => HasDecodedPerSignalExport(common, traces));
		await traces.WaitUntilSettledWithoutSelfTrafficAsync(common.Endpoint);
		await common.WaitUntilSettledWithoutSelfTrafficAsync();

		common.HasPath("/v1/logs").Should().BeTrue();
		common.HasPath("/v1/metrics").Should().BeTrue();
		common.HasPath("/v1/traces").Should().BeFalse();
		traces.HasPath("/v1/traces").Should().BeTrue();
		traces.HasPath("/v1/logs").Should().BeFalse();
		traces.HasPath("/v1/metrics").Should().BeFalse();
		common.GetLogs().Should().NotBeEmpty();
		common.GetMetrics().Should().NotBeEmpty();
		traces.GetSpans().Should().NotBeEmpty();
		common.HasHttpClientSpanTargetingEndpoint().Should().BeFalse();
		traces.HasHttpClientSpanTargetingEndpoint().Should().BeFalse();
	}

	private static bool HasDecodedPerSignalExport(OtlpLoopback common, OtlpLoopback traces)
	{
		try
		{
			return traces.HasPath("/v1/traces") &&
				   traces.GetSpans().Count > 0 &&
				   common.HasPath("/v1/logs") &&
				   common.GetLogs().Count > 0 &&
				   common.HasPath("/v1/metrics") &&
				   common.GetMetrics().Count > 0 &&
				   !common.HasPath("/v1/traces");
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

#if (auth)
	private IReadOnlyList<Claim> ReadAccessTokenClaims()
	{
		var claims = new JwtSecurityTokenHandler().ReadJwtToken(AccessToken!.AccessToken).Claims.ToList();
		var scope = claims.Find(claim => claim.Type == "scope");
		if (scope?.Value.Contains(' ') == true)
		{
			claims.Remove(scope);
			claims.AddRange(scope.Value.Split(' ').Select(value => new Claim(scope.Type, value, scope.ValueType, scope.Issuer, scope.OriginalIssuer)));
		}

		return claims;
	}

	private static bool HasDecodedIdentityExport(OtlpLoopback collector, string subject)
	{
		try
		{
			var entry = collector.GetSpans()
								 .FirstOrDefault(span => span.ScopeName == "Microsoft.AspNetCore" &&
														 span.GetAttribute("user.id") == subject &&
														 !string.IsNullOrEmpty(span.TraceId));
			if (entry is null || !collector.HasPath("/v1/metrics") || collector.GetMetrics().Count == 0)
				return false;

			var serializedClaims = collector.GetLogs()
											.Where(record => record.TraceId == entry.TraceId)
											.Select(record => record.GetAttribute("user.claims"))
											.FirstOrDefault(value => !string.IsNullOrEmpty(value));
			if (serializedClaims is null)
				return false;

			using var document = JsonDocument.Parse(serializedClaims);
			return document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() > 0;
		}
		catch (InvalidProtocolBufferException)
		{
			return false;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private static void AssertClaimEventsMatchLog(IReadOnlyList<DecodedSpanEvent> claimEvents, IReadOnlyList<DecodedLogRecord> logs)
	{
		foreach (var log in logs)
		{
			var serializedClaims = log.GetAttribute("user.claims");
			serializedClaims.Should().NotBeNullOrEmpty();
			using var document = JsonDocument.Parse(serializedClaims);
			document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
			var loggedClaims = document.RootElement.EnumerateArray().ToArray();
			loggedClaims.Should().HaveCount(claimEvents.Count);
			foreach (var (claimEvent, loggedClaim) in claimEvents.Zip(loggedClaims))
			{
				loggedClaim.GetProperty("type").GetString().Should().Be(claimEvent.GetAttribute("user.claim.type"));
				loggedClaim.GetProperty("value").GetString().Should().Be(claimEvent.GetAttribute("user.claim.value"));
				loggedClaim.GetProperty("value_type").GetString().Should().Be(claimEvent.GetAttribute("user.claim.value_type"));
				loggedClaim.GetProperty("issuer").GetString().Should().Be(claimEvent.GetAttribute("user.claim.issuer"));
				loggedClaim.GetProperty("original_issuer").GetString().Should().Be(claimEvent.GetAttribute("user.claim.original_issuer"));
			}
		}
	}
#endif
}
#endif