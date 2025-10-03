using FluentValidation;
using MediatR;
using Monaco.Template.Backend.Application.Features.Company.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class CreateCompany
{
	public sealed record Command(string Name,
								 string Email,
								 string? WebSiteUrl,
								 string? Street,
								 string? City,
								 string? County,
								 string? PostCode,
								 Guid? CountryId) : CommandBase<Guid>;

	internal sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			RuleFor(x => x.Name)
				.NotEmpty()
				.MaximumLength(Domain.Model.Entities.Company.NameLength)
				.MustAsync(async (name, ct) => !await dbContext.ExistsAsync<Domain.Model.Entities.Company>(x => x.Name == name, ct))
				.WithMessage("A company with the name {PropertyValue} already exists");

			RuleFor(x => x.Email)
				.NotEmpty()
				.EmailAddress()
				.MaximumLength(Domain.Model.Entities.Company.EmailLength);

			RuleFor(x => x.WebSiteUrl)
				.MaximumLength(Domain.Model.Entities.Company.WebSiteUrlLength);

			RuleFor(x => x.Street)
				.MaximumLength(Domain.Model.ValueObjects.Address.StreetLength);

			RuleFor(x => x.City)
				.MaximumLength(Domain.Model.ValueObjects.Address.CityLength);

			RuleFor(x => x.County)
				.MaximumLength(Domain.Model.ValueObjects.Address.CountyLength);

			RuleFor(x => x.PostCode)
				.MaximumLength(Domain.Model.ValueObjects.Address.PostCodeLength);

			RuleFor(x => x.CountryId)
				.NotNull()
				.When(x => x.Street is not null || x.City is not null || x.County is not null || x.PostCode is not null, ApplyConditionTo.CurrentValidator)
				.MustExistAsync<Command, Domain.Model.Entities.Country, Guid>(dbContext)
				.When(x => x.CountryId.HasValue, ApplyConditionTo.CurrentValidator);
		}
	}

	internal sealed class Handler : IRequestHandler<Command, CommandResult<Guid>>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<CommandResult<Guid>> Handle(Command request, CancellationToken cancellationToken)
		{
			var country = await _dbContext.GetAsync<Domain.Model.Entities.Country>(request.CountryId, cancellationToken);
			var item = request.Map(country);

			_dbContext.Set<Domain.Model.Entities.Company>().Attach(item);
			await _dbContext.SaveEntitiesAsync(cancellationToken);

			return CommandResult<Guid>.Success(item.Id);
		}
	}
}
