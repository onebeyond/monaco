using FluentValidation;
using LinqKit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Application.Features.Product;

public sealed class DeleteProduct
{
	public sealed record Command(Guid Id) : CommandBase(Id);

	internal sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			this.CheckIfExists<Command, Domain.Model.Entities.Product>(dbContext);
		}
	}

	internal sealed class Handler : IRequestHandler<Command, CommandResult>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.Set<Domain.Model.Entities.Product>()
									   .Include(x => x.Pictures)
									   .ThenInclude(x => x.Thumbnail)
									   .SingleAsync(x => x.Id == request.Id, cancellationToken);
			
			item.Pictures
				.ForEach(picture => picture.MarkForRemoval());

			_dbContext.Set<Domain.Model.Entities.Product>()
					  .Remove(item);

			await _dbContext.SaveEntitiesAsync(cancellationToken);

			return CommandResult.Success();
		}
	}
}