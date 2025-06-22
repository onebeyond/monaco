using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class DeleteCompany
{
	public sealed record Command(Guid Id) : CommandBase(Id);

	internal sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			this.CheckIfExists<Command, Domain.Model.Entities.Company>(dbContext);

			RuleFor(x => x)
				.MustAsync(async (cmd, ct) => await dbContext.Set<Domain.Model.Entities.Product>()
															 .AllAsync(p => p.CompanyId != cmd.Id, ct))
				.WithMessage("The Company cannot be deleted while it is still assigned to a Product");
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
			await _dbContext.Set<Domain.Model.Entities.Company>()
							.Where(c => c.Id == request.Id)
							.ExecuteDeleteAsync(cancellationToken);

			return CommandResult.Success();
		}
	}
}
