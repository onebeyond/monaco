using DcslGs.Template.Common.Application.Validators.Extensions;
using DcslGs.Template.Application.Infrastructure.Context;
using FluentValidation;

namespace DcslGs.Template.Application.Features.File.Commands.Validators;

public sealed class FileDeleteCommandValidator : AbstractValidator<FileDeleteCommand>
{
    public FileDeleteCommandValidator(AppDbContext dbContext)
    {
		RuleLevelCascadeMode = CascadeMode.Stop;

        this.CheckIfExists<FileDeleteCommand, Domain.Model.File>(dbContext);
    }
}