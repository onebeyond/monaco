using FluentValidation;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company.Commands.Validators;

public sealed class CompanyDeleteCommandValidator : AbstractValidator<CompanyDeleteCommand>
{
	public CompanyDeleteCommandValidator(AppDbContext dbContext)
	{
		RuleLevelCascadeMode = CascadeMode.Stop;

		this.CheckIfExists<CompanyDeleteCommand, Domain.Model.Company>(dbContext);
	}
}