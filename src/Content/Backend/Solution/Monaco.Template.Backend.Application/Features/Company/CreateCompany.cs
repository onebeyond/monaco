using FluentValidation;
using MediatR;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class CreateCompany
{
	public record Command(string Name,
					      string Email,
					      string WebSiteUrl,
					      string? Street,
					      string? City,
					      string? County,
					      string? PostCode,
					      Guid? CountryId) : CommandBase<Guid>;

	public sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			RuleFor(x => x.Name)
				.NotEmpty()
				.MaximumLength(100)
				.MustAsync(async (name, ct) => !await dbContext.ExistsAsync<Domain.Model.Company>(x => x.Name == name, ct))
				.WithMessage("A company with the name {PropertyValue} already exists");

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
				.MustExistAsync<Command, Domain.Model.Country, Guid>(dbContext)
				.When(x => x.CountryId.HasValue, ApplyConditionTo.CurrentValidator);
		}
	}

	public sealed class Handler : IRequestHandler<Command, CommandResult<Guid>>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<CommandResult<Guid>> Handle(Command request, CancellationToken cancellationToken)
		{
			var country = await _dbContext.GetAsync<Domain.Model.Country>(request.CountryId, cancellationToken);
			var item = request.Map(country);

			_dbContext.Set<Domain.Model.Company>().Attach(item);
			await _dbContext.SaveEntitiesAsync(cancellationToken);

			return CommandResult<Guid>.Success(item.Id);
		}
	}
}
