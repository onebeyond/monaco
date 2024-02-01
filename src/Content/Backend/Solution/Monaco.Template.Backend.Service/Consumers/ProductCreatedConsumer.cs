using MassTransit;
using MediatR;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Messages.V1;

namespace Monaco.Template.Backend.Service.Consumers;

public class ProductCreatedConsumer : IConsumer<ProductCreated>
{
	private readonly ISender _sender;

	public ProductCreatedConsumer(ISender sender)
	{
		_sender = sender;
	}

	public async Task Consume(ConsumeContext<ProductCreated> context)
	{
		//Sample of launching a long-running process in response to the event
		await _sender.Send(new LongRunningProcessCommand(), context.CancellationToken);
	}
}