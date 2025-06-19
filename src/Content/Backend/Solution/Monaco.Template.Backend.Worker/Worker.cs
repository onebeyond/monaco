#if (massTransitIntegration)
using MassTransit;

#endif
namespace Monaco.Template.Backend.Worker;

public class Worker : BackgroundService
{
#if (massTransitIntegration)
	private readonly IBusControl _bus;

	public Worker(IBusControl bus)
	{
		_bus = bus;
	}

#endif
	protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
		Task.CompletedTask;
#if (massTransitIntegration)

	public override async Task StartAsync(CancellationToken cancellationToken) =>
		await _bus.StartAsync(cancellationToken).ConfigureAwait(false);

	public override async Task StopAsync(CancellationToken cancellationToken) =>
		await _bus.StopAsync(cancellationToken).ConfigureAwait(false);
#endif
}
