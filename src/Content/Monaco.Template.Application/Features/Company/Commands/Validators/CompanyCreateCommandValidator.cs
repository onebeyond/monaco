using Monaco.Template.Common.Application.Validators.Extensions;
using Monaco.Template.Common.Infrastructure.Context.Extensions;
using Monaco.Template.Application.Infrastructure.Context;
using FluentValidation;

namespace Monaco.Template.Application.Features.Company.Commands.Validators;

public sealed class CompanyCreateCommandValidator : AbstractValidator<CompanyCreateCommand>
{
	public CompanyCreateCommandValidator(AppDbContext dbContext)
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
			.MustExistAsync<CompanyCreateCommand, Domain.Model.Country, Guid>(dbContext)
			.When(x => x.CountryId.HasValue);

	}
}