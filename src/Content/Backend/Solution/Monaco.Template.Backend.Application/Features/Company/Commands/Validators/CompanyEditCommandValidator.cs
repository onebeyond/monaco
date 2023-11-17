using FluentValidation;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company.Commands.Validators;

public sealed class CompanyEditCommandValidator : AbstractValidator<CompanyEditCommand>
{
	public CompanyEditCommandValidator(AppDbContext dbContext)
	{
		RuleLevelCascadeMode = CascadeMode.Stop;

		this.CheckIfExists<CompanyEditCommand, Domain.Model.Company>(dbContext);

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
			.MustExistAsync<CompanyEditCommand, Domain.Model.Country>(dbContext)
			.When(x => x.CountryId.HasValue, ApplyConditionTo.CurrentValidator);
	}
}