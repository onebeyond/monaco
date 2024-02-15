using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;

namespace Monaco.Template.Backend.Application.Features.File;

public sealed class CleanTempFile
{
	public record CleanupCommand() : IRequest;

	public sealed class Handler : IRequestHandler<CleanupCommand>
	{
		private readonly AppDbContext _dbContext;
		private readonly IFileService _fileService;

		public Handler(AppDbContext dbContext, IFileService fileService)
		{
			_dbContext = dbContext;
			_fileService = fileService;
		}

		public async Task Handle(CleanupCommand request, CancellationToken cancellationToken)
		{
			var files = await _dbContext.Set<Domain.Model.File>()
										.Where(x => x.IsTemp &&
													DateTime.Now - x.UploadedOn >= TimeSpan.FromHours(6))
										.ToArrayAsync(cancellationToken);

			await _fileService.DeleteFilesAsync(files, cancellationToken);

			_dbContext.Set<Domain.Model.File>()
					  .RemoveRange(files);
			await _dbContext.SaveEntitiesAsync(cancellationToken);
		}
	}
}