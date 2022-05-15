using Monaco.Template.Common.Application.Validators.Extensions;
using Monaco.Template.Application.Infrastructure.Context;
using FluentValidation;

namespace Monaco.Template.Application.Features.Company.Commands.Validators;

public sealed class CompanyDeleteCommandValidator : AbstractValidator<CompanyDeleteCommand>
{
	public CompanyDeleteCommandValidator(AppDbContext dbContext)
	{
		RuleLevelCascadeMode = CascadeMode.Stop;

		this.CheckIfExists<CompanyDeleteCommand, Domain.Model.Company>(dbContext);
	}
}