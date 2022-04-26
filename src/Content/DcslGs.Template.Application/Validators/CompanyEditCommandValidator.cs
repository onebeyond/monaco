using FluentValidation;
using DcslGs.Template.Application.Commands.Company;
using DcslGs.Template.Common.Application.Validators;
using DcslGs.Template.Common.Infrastructure.Context.Extensions;
using DcslGs.Template.Domain.Model;
using DcslGs.Template.Infrastructure.Context;

namespace DcslGs.Template.Application.Validators;

public sealed class CompanyEditCommandValidator : AbstractValidator<CompanyEditCommand>
{
    public CompanyEditCommandValidator(AppDbContext dbContext)
    {
        CascadeMode = CascadeMode.Stop;

        this.CheckIfExists<CompanyEditCommand, Company>(dbContext);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .MustAsync(async (cmd, name, ct) => !await dbContext.ExistsAsync<Company>(x => x.Id != cmd.Id &&
                                                                                           x.Name == name,
                                                                                      ct))
            .WithMessage("Another company with the name {PropertyValue} already exists");

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
            .MustExistAsync<CompanyEditCommand, Country>(dbContext);
    }
}