using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;
using Monaco.Eng.RuntimeShapeVerification.WorkerHook;
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
		hosts.WorkerServices.Should().NotBeNull();
		hosts.ApiClient.Should().BeNull();
		Directory.Exists(shape.ProjectDir("Api")).Should().BeFalse();
		Directory.Exists(shape.ProjectDir("Messages")).Should().BeFalse();
		Directory.Exists(shape.ProjectDir("IntegrationTests")).Should().BeFalse();

		// The baseline Worker has no messaging tables; absence of the Messages project is the no-message guarantee.
		shape.Options.MassTransit.Should().BeFalse();

		using (var scope = hosts.WorkerServices!.CreateScope())
		{
			var companyId = await GeneratedCompanyStamp.SaveAsync(scope.ServiceProvider,
																  CancellationToken.None,
																  shape.Name);
			var (createdBy, createdAt) = await GeneratedDbAccess.ReadCompanyStampAsync(sql, companyId);
			createdBy.Should().NotBeNullOrWhiteSpace();
			createdAt.Should().NotBeNull();
		}

		if (mode != DestinationMode.ReceiverUnavailable)
		{
			receiver.Should().NotBeNull();
			await receiver.WaitUntilAsync(() => receiver.HasSignalKindsFor(shape.ServiceName("Worker")), attempts: 900);
			receiver.HasSignalKindsFor(shape.ServiceName("Worker")).Should().BeTrue();
			receiver.HasCounter(shape.ServiceName("Worker"),
								shape.ApplicationMeterName,
								"company.successful_creations")
					.Should()
					.BeFalse();
		}
	}
}