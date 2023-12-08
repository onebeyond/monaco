using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class DeleteCompany
{
	public record Command(Guid Id) : CommandBase(Id);

	public sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			this.CheckIfExists<Command, Domain.Model.Company>(dbContext);
		}
	}

	public sealed class Handler : IRequestHandler<Command, ICommandResult>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<ICommandResult> Handle(Command request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.Set<Domain.Model.Company>().SingleAsync(x => x.Id == request.Id, cancellationToken);

			_dbContext.Set<Domain.Model.Company>().Remove(item);
			await _dbContext.SaveEntitiesAsync(cancellationToken);

			return new CommandResult();
		}
	}
}
