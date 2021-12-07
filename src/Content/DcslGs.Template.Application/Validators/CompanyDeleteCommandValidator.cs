using FluentValidation;
using DcslGs.Template.Application.Commands.Company;
using DcslGs.Template.Common.Application.Validators;
using DcslGs.Template.Domain.Model;
using DcslGs.Template.Infrastructure.Context;

namespace DcslGs.Template.Application.Validators;

public class CompanyDeleteCommandValidator : AbstractValidator<CompanyDeleteCommand>
{
    public CompanyDeleteCommandValidator(AppDbContext dbContext)
    {
        this.CheckIfExists<CompanyDeleteCommand, Company>(dbContext);
    }
}