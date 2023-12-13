using FluentValidation;
using MediatR;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;

namespace Monaco.Template.Backend.Application.Features.File;

public sealed class CreateFile
{
	public record Command(Stream Stream, string FileName, string ContentType) : CommandBase<Guid>;

	public class Validator : AbstractValidator<Command>
	{
		public Validator()
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			RuleFor(x => x.Stream)
				.Must(x => x.Length > 0)
				.WithMessage("File uploaded cannot be empty");
		}
	}

	public sealed class Handler : IRequestHandler<Command, ICommandResult<Guid>>
	{
		private readonly IFileService _fileService;

		public Handler(IFileService fileService)
		{
			_fileService = fileService;
		}

		public async Task<ICommandResult<Guid>> Handle(Command request, CancellationToken cancellationToken)
		{
			var file = await _fileService.Upload(request.Stream, request.FileName, request.ContentType, cancellationToken);

			return new CommandResult<Guid>(file.Id);
		}
	}
}