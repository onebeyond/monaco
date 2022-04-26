using DcslGs.Template.Application.Commands.File;
using FluentValidation;

namespace DcslGs.Template.Application.Validators;

public sealed class FileCreateCommandValidator : AbstractValidator<FileCreateCommand>
{
    public FileCreateCommandValidator()
    {
        CascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Stream)
            .Must(x => x.Length > 0)
            .WithMessage("File uploaded cannot be empty");
    }
}