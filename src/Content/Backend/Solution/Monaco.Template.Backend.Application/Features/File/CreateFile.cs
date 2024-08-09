using FluentValidation;
using MediatR;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Application.Commands;

namespace Monaco.Template.Backend.Application.Features.File;

public sealed class CreateFile
{
	public record Command(Stream Stream, string FileName, string ContentType) : CommandBase<Guid>;

	internal class Validator : AbstractValidator<Command>
	{
		public Validator()
		{
			RuleLevelCascadeMode = CascadeMode.Stop;

			RuleFor(x => x)
				.Must(x => x.Stream.Length > 0)
				.WithMessage("File uploaded cannot be empty")
				.Must(x => Path.GetFileNameWithoutExtension(x.FileName).Length > 0)
				.WithMessage("File name without extension cannot be empty")
				.Must(x => Path.GetFileNameWithoutExtension(x.FileName).Length <= Domain.Model.File.NameLength)
				.WithMessage($"File name without extension cannot be longer than {Domain.Model.File.NameLength} characters")
				.Must(x => Path.GetExtension(x.FileName).Length <= Domain.Model.File.ExtensionLength)
				.WithMessage($"File extension cannot be longer than {Domain.Model.File.ExtensionLength} characters")
				.Must(x => x.ContentType.Length <= Domain.Model.File.ContentTypeLength)
				.WithMessage($"ContentType cannot be longer than {Domain.Model.File.ContentTypeLength} characters");
		}
	}

	#if (filesSupport)
	internal sealed class Handler : IRequestHandler<Command, CommandResult<Guid>>
	{
		private readonly AppDbContext _dbContext;
		private readonly IFileService _fileService;

		public Handler(AppDbContext dbContext, IFileService fileService)
		{
			_dbContext = dbContext;
			_fileService = fileService;
		}

		public async Task<CommandResult<Guid>> Handle(Command request, CancellationToken cancellationToken)
		{
			var file = await _fileService.UploadAsync(request.Stream, request.FileName, request.ContentType, cancellationToken);

			try
			{
				await _dbContext.Set<Domain.Model.File>()
								.AddAsync(file, cancellationToken);
				await _dbContext.SaveEntitiesAsync(cancellationToken);
			}
			catch
			{
				await _fileService.DeleteFileAsync(file, cancellationToken);
				throw;
			}
			
			return CommandResult<Guid>.Success(file.Id);
		}
	}
	#endif
}