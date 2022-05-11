using DcslGs.Template.Common.Application.Validators.Extensions;
using DcslGs.Template.Application.Infrastructure.Context;
using FluentValidation;

namespace DcslGs.Template.Application.Features.Company.Commands.Validators;

public sealed class CompanyDeleteCommandValidator : AbstractValidator<CompanyDeleteCommand>
{
    public CompanyDeleteCommandValidator(AppDbContext dbContext)
	{
		RuleLevelCascadeMode = CascadeMode.Stop;
		
        this.CheckIfExists<CompanyDeleteCommand, Domain.Model.Company>(dbContext);
    }
}