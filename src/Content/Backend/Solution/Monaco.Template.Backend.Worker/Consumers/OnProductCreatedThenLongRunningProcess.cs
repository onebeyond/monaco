using MassTransit;
using MediatR;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Messages.V1;

namespace Monaco.Template.Backend.Worker.Consumers;

public class OnProductCreatedThenLongRunningProcess : IConsumer<ProductCreated>
{
	private readonly ISender _sender;

	public OnProductCreatedThenLongRunningProcess(ISender sender)
	{
		_sender = sender;
	}

	public async Task Consume(ConsumeContext<ProductCreated> context)
	{
		//Sample of launching a long-running process in response to the event
		var msg = context.Message;
		await _sender.Send(new LongRunningProcess.Command(msg.Id,
														  msg.Title,
														  msg.Description,
														  msg.Price,
														  msg.CompanyId),
						   context.CancellationToken);
	}
}