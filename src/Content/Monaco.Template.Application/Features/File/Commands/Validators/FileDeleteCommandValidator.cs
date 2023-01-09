using FluentValidation;
using Monaco.Template.Application.Infrastructure.Context;
using Monaco.Template.Common.Application.Validators.Extensions;

namespace Monaco.Template.Application.Features.File.Commands.Validators;

public sealed class FileDeleteCommandValidator : AbstractValidator<FileDeleteCommand>
{
	public FileDeleteCommandValidator(AppDbContext dbContext)
	{
		RuleLevelCascadeMode = CascadeMode.Stop;

		this.CheckIfExists<FileDeleteCommand, Domain.Model.File>(dbContext);
	}
}