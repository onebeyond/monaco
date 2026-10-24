using AwesomeAssertions;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;
using System.Diagnostics.CodeAnalysis;

namespace Monaco.Eng.RuntimeShapeVerification.Drivers;

[ExcludeFromCodeCoverage]
internal static class WorkerBaselineDriver
{
	internal static async Task ExecuteAsync(GeneratedShape shape,
											ShapeHostSet hosts,
											string sql,
											OtlpSignalReceiver? receiver,
											DestinationMode mode)
	{
		hosts.WorkerServices.Should().BeNull();
		hosts.ApiClient.Should().BeNull();
		shape.Options.MassTransit.Should().BeFalse();

		hosts.StampMarkerPath.Should().NotBeNull();
		await WaitUntilAsync(() => Task.FromResult(File.Exists(hosts.StampMarkerPath!)), attempts: 300);
		var stampedId = Guid.Parse((await File.ReadAllTextAsync(hosts.StampMarkerPath!)).Trim());
		var (createdBy, createdAt) = await GeneratedDbAccess.ReadCompanyStampAsync(sql, stampedId);
		createdBy.Should().NotBeNullOrWhiteSpace();
		createdAt.Should().NotBeNull();
		(await GeneratedDbAccess.CountCompaniesAsync(sql)).Should().Be(1);

		if (mode != DestinationMode.ReceiverUnavailable)
		{
			receiver.Should().NotBeNull();
			await receiver.WaitUntilAsync(() => receiver.HasSignalKindsFor(shape.ServiceName("Worker")),
										  attempts: 900,
										  describe: receiver.Describe);
			receiver.HasSignalKindsFor(shape.ServiceName("Worker")).Should().BeTrue(receiver.Describe());
			if (mode == DestinationMode.ProductionOverride)
				receiver.SawAuthorization(OtlpSignalReceiver.ProductionAuthorization)
						.Should()
						.BeTrue(receiver.Describe());
			receiver.HasCounter(shape.ServiceName("Worker"),
								shape.ApplicationMeterName,
								"company.successful_creations")
					.Should()
					.BeFalse();
		}
	}

	private static async Task WaitUntilAsync(Func<Task<bool>> predicate, int attempts)
	{
		for (var attempt = 0; attempt < attempts; attempt++)
		{
			if (await predicate())
				return;
			await Task.Delay(100);
		}

		if (!await predicate())
			throw new TimeoutException("Worker baseline evidence was not observed in time.");
	}
}