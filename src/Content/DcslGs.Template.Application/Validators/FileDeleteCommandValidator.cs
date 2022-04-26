using DcslGs.Template.Application.Commands.File;
using DcslGs.Template.Common.Application.Validators;
using DcslGs.Template.Infrastructure.Context;
using FluentValidation;
using File = DcslGs.Template.Domain.Model.File;

namespace DcslGs.Template.Application.Validators;

public sealed class FileDeleteCommandValidator : AbstractValidator<FileDeleteCommand>
{
    public FileDeleteCommandValidator(AppDbContext dbContext)
    {
        CascadeMode = CascadeMode.Stop;

        this.CheckIfExists<FileDeleteCommand, File>(dbContext);
    }
}