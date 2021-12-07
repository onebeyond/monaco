using FluentValidation;
using DcslGs.Template.Application.Commands.Company;
using DcslGs.Template.Common.Application.Validators;
using DcslGs.Template.Common.Infrastructure.Context.Extensions;
using DcslGs.Template.Domain.Model;
using DcslGs.Template.Infrastructure.Context;

namespace DcslGs.Template.Application.Validators;

public class CompanyCreateCommandValidator : AbstractValidator<CompanyCreateCommand>
{
    public CompanyCreateCommandValidator(AppDbContext dbContext)
    {
        CascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .MustAsync(async (name, ct) => !await dbContext.ExistsAsync<Company>(x => x.Name == name, ct))
            .WithMessage("A company with the name {PropertyValue} already exists");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.WebSiteUrl)
            .MaximumLength(300);

        RuleFor(x => x.Address)
            .MaximumLength(100);

        RuleFor(x => x.City)
            .MaximumLength(100);

        RuleFor(x => x.County)
            .MaximumLength(100);

        RuleFor(x => x.PostCode)
            .MaximumLength(10);
			
        RuleFor(x => x.CountryId)
            .NotEmpty()
            .MustExistAsync<CompanyCreateCommand, Country, Guid>(dbContext);
			
    }
}