using AwesomeAssertions;
using Monaco.Eng.RuntimeShapeVerification.Fixtures;
using Monaco.Eng.RuntimeShapeVerification.Infrastructure;
using Monaco.Eng.RuntimeShapeVerification.Workloads;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Eng.RuntimeShapeVerification;

[ExcludeFromCodeCoverage]
[Collection("RuntimeShapeVerification")]
[Trait("Runtime Shape Verification", "RUNTIME-ORACLE-v1")]
public sealed class RuntimeOracleTests
{
	private static readonly SemaphoreSlim Gate = new(1, 1);

	private readonly ShapeHiveFixture _shapes;
	private readonly InfrastructureFixture _infra;

	public RuntimeOracleTests(ShapeHiveFixture shapes, InfrastructureFixture infra)
	{
		_shapes = shapes;
		_infra = infra;
	}

	[Fact(DisplayName = "R1-DA Full API and Worker, receiver available")]
	public Task FullApiWorker_ReceiverAvailable() =>
		RunAsync(RuntimeShape.FullApiWorker, DestinationMode.ReceiverAvailable);

	[Fact(DisplayName = "R1-DU Full API and Worker, receiver unavailable")]
	public Task FullApiWorker_ReceiverUnavailable() =>
		RunAsync(RuntimeShape.FullApiWorker, DestinationMode.ReceiverUnavailable);

	[Fact(DisplayName = "R1-PO Full API and Worker, production override")]
	public Task FullApiWorker_ProductionOverride() =>
		RunAsync(RuntimeShape.FullApiWorker, DestinationMode.ProductionOverride);

	[Fact(DisplayName = "R2-DA Gateway full, receiver available")]
	public Task GatewayFull_ReceiverAvailable() =>
		RunAsync(RuntimeShape.GatewayFull, DestinationMode.ReceiverAvailable);

	[Fact(DisplayName = "R2-DU Gateway full, receiver unavailable")]
	public Task GatewayFull_ReceiverUnavailable() =>
		RunAsync(RuntimeShape.GatewayFull, DestinationMode.ReceiverUnavailable);

	[Fact(DisplayName = "R2-PO Gateway full, production override")]
	public Task GatewayFull_ProductionOverride() =>
		RunAsync(RuntimeShape.GatewayFull, DestinationMode.ProductionOverride);

	[Fact(DisplayName = "R3-DA Lean API, receiver available")]
	public Task LeanApi_ReceiverAvailable() =>
		RunAsync(RuntimeShape.LeanApi, DestinationMode.ReceiverAvailable);

	[Fact(DisplayName = "R3-DU Lean API, receiver unavailable")]
	public Task LeanApi_ReceiverUnavailable() =>
		RunAsync(RuntimeShape.LeanApi, DestinationMode.ReceiverUnavailable);

	[Fact(DisplayName = "R3-PO Lean API, production override")]
	public Task LeanApi_ProductionOverride() =>
		RunAsync(RuntimeShape.LeanApi, DestinationMode.ProductionOverride);

	[Fact(DisplayName = "R4-DA Publisher API, receiver available")]
	public Task PublisherApi_ReceiverAvailable() =>
		RunAsync(RuntimeShape.PublisherApi, DestinationMode.ReceiverAvailable);

	[Fact(DisplayName = "R4-DU Publisher API, receiver unavailable")]
	public Task PublisherApi_ReceiverUnavailable() =>
		RunAsync(RuntimeShape.PublisherApi, DestinationMode.ReceiverUnavailable);

	[Fact(DisplayName = "R4-PO Publisher API, production override")]
	public Task PublisherApi_ProductionOverride() =>
		RunAsync(RuntimeShape.PublisherApi, DestinationMode.ProductionOverride);

	[Fact(DisplayName = "R5-DA Worker consumer, receiver available")]
	public Task WorkerConsumer_ReceiverAvailable() =>
		RunAsync(RuntimeShape.WorkerConsumer, DestinationMode.ReceiverAvailable);

	[Fact(DisplayName = "R5-DU Worker consumer, receiver unavailable")]
	public Task WorkerConsumer_ReceiverUnavailable() =>
		RunAsync(RuntimeShape.WorkerConsumer, DestinationMode.ReceiverUnavailable);

	[Fact(DisplayName = "R5-PO Worker consumer, production override")]
	public Task WorkerConsumer_ProductionOverride() =>
		RunAsync(RuntimeShape.WorkerConsumer, DestinationMode.ProductionOverride);

	[Fact(DisplayName = "R6-DA Worker baseline, receiver available")]
	public Task WorkerBaseline_ReceiverAvailable() =>
		RunAsync(RuntimeShape.WorkerBaseline, DestinationMode.ReceiverAvailable);

	[Fact(DisplayName = "R6-DU Worker baseline, receiver unavailable")]
	public Task WorkerBaseline_ReceiverUnavailable() =>
		RunAsync(RuntimeShape.WorkerBaseline, DestinationMode.ReceiverUnavailable);

	[Fact(DisplayName = "R6-PO Worker baseline, production override")]
	public Task WorkerBaseline_ProductionOverride() =>
		RunAsync(RuntimeShape.WorkerBaseline, DestinationMode.ProductionOverride);

	[Fact(DisplayName = "R7-DA Full API and Worker without tests, receiver available")]
	public Task FullApiWorkerWithoutTests_ReceiverAvailable() =>
		RunAsync(RuntimeShape.FullApiWorkerWithoutTests, DestinationMode.ReceiverAvailable);

	[Fact(DisplayName = "R7-DU Full API and Worker without tests, receiver unavailable")]
	public Task FullApiWorkerWithoutTests_ReceiverUnavailable() =>
		RunAsync(RuntimeShape.FullApiWorkerWithoutTests, DestinationMode.ReceiverUnavailable);

	[Fact(DisplayName = "R7-PO Full API and Worker without tests, production override")]
	public Task FullApiWorkerWithoutTests_ProductionOverride() =>
		RunAsync(RuntimeShape.FullApiWorkerWithoutTests, DestinationMode.ProductionOverride);

	private async Task RunAsync(RuntimeShape shape, DestinationMode mode)
	{
		await Gate.WaitAsync();
		try
		{
			await RunCoreAsync(shape, mode);
		}
		finally
		{
			Gate.Release();
		}
	}

	private async Task RunCoreAsync(RuntimeShape shapeId, DestinationMode mode)
	{
		_infra.EnsureDocker();
		var shape = _shapes.Get(shapeId);
		shape.Options.Common.Should().BeTrue();

		OtlpSignalReceiver? receiver = null;
		try
		{
			if (mode == DestinationMode.ReceiverUnavailable)
				await OtlpSignalReceiver.EnsureDevelopmentPortFreeAsync();

			receiver = mode switch
					   {
						   DestinationMode.ReceiverAvailable => await OtlpSignalReceiver.StartDevelopmentAsync(),
						   DestinationMode.ProductionOverride => await OtlpSignalReceiver.StartProductionAsync(),
						   _ => null
					   };

			await RuntimeWorkloads.ExecuteAsync(shape,
												_infra,
												mode,
												receiver);
		}
		finally
		{
			if (receiver is not null)
				await receiver.DisposeAsync();
		}
	}
}