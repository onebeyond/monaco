using FluentValidation;
using MediatR;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Application.Features.File;

public sealed class DeleteFile
{
	public record Command(Guid Id) : CommandBase(Id);

	public sealed class Validator : AbstractValidator<Command>
	{
		public Validator(AppDbContext dbContext)
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			this.CheckIfExists<Command, Domain.Model.File>(dbContext);
		}
	}

	#if filesSupport
	public sealed class Handler : IRequestHandler<Command, ICommandResult>
	{
		private readonly IFileService _fileService;

		public Handler(IFileService fileService)
		{
			_fileService = fileService;
		}

		public async Task<ICommandResult> Handle(Command request, CancellationToken cancellationToken)
		{
			await _fileService.Delete(request.Id, cancellationToken);

			return new CommandResult();
		}
	}
	#endif
}