using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class EditCompany
{
	public sealed record Command(Guid Id,
								 string Name,
								 string Email,
								 string WebSiteUrl,
								 string? Street,
								 string? City,
								 string? County,
								 string? PostCode,
								 Guid? CountryId) : CommandBase(Id);

	internal sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			this.CheckIfExists<Command, Domain.Model.Company>(dbContext);

			RuleFor(x => x.Name)
				.NotEmpty()
				.MaximumLength(100)
				.MustAsync(async (cmd, name, ct) => !await dbContext.ExistsAsync<Domain.Model.Company>(x => x.Id != cmd.Id &&
																											x.Name == name,
																									   ct))
				.WithMessage("Another company with the name {PropertyValue} already exists");

			RuleFor(x => x.Email)
				.NotEmpty()
				.EmailAddress()
				.MaximumLength(255);

			RuleFor(x => x.WebSiteUrl)
				.MaximumLength(300);

			RuleFor(x => x.Street)
				.MaximumLength(100);

			RuleFor(x => x.City)
				.MaximumLength(100);

			RuleFor(x => x.County)
				.MaximumLength(100);

			RuleFor(x => x.PostCode)
				.MaximumLength(10);

			RuleFor(x => x.CountryId)
				.NotNull()
				.When(x => x.Street is not null || x.City is not null || x.County is not null || x.PostCode is not null, ApplyConditionTo.CurrentValidator)
				.MustExistAsync<Command, Domain.Model.Country>(dbContext)
				.When(x => x.CountryId.HasValue, ApplyConditionTo.CurrentValidator);
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
			var item = await _dbContext.Set<Domain.Model.Company>().SingleAsync(x => x.Id == request.Id, cancellationToken);
			var country = await _dbContext.GetAsync<Domain.Model.Country>(request.CountryId, cancellationToken);

			request.Map(item, country);

			await _dbContext.SaveEntitiesAsync(cancellationToken);

			return CommandResult.Success();
		}
	}
}
