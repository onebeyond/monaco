using FluentValidation;

namespace DcslGs.Template.Application.Features.File.Commands.Validators;

public sealed class FileCreateCommandValidator : AbstractValidator<FileCreateCommand>
{
    public FileCreateCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Stream)
            .Must(x => x.Length > 0)
            .WithMessage("File uploaded cannot be empty");
    }
}