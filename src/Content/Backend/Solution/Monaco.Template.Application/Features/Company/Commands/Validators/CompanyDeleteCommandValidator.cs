using FluentValidation;
using Monaco.Template.Application.Infrastructure.Context;
using Monaco.Template.Common.Application.Validators.Extensions;

namespace Monaco.Template.Application.Features.Company.Commands.Validators;

public sealed class CompanyDeleteCommandValidator : AbstractValidator<CompanyDeleteCommand>
{
	public CompanyDeleteCommandValidator(AppDbContext dbContext)
	{
		RuleLevelCascadeMode = CascadeMode.Stop;

		this.CheckIfExists<CompanyDeleteCommand, Domain.Model.Company>(dbContext);
	}
}