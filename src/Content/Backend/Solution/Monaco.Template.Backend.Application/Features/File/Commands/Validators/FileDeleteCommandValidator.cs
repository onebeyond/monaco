using FluentValidation;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Application.Features.File.Commands.Validators;

public sealed class FileDeleteCommandValidator : AbstractValidator<FileDeleteCommand>
{
	public FileDeleteCommandValidator(AppDbContext dbContext)
	{
		RuleLevelCascadeMode = CascadeMode.Stop;

		this.CheckIfExists<FileDeleteCommand, Domain.Model.File>(dbContext);
	}
}