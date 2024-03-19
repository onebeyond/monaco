using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;

namespace Monaco.Template.Backend.Application.Features.File;

public sealed class CleanTempFiles
{
	public record Command : IRequest;

	public sealed class Handler : IRequestHandler<Command>
	{
		private readonly AppDbContext _dbContext;
		private readonly IBlobStorageService _blobStorageService;

		public Handler(AppDbContext dbContext, IBlobStorageService blobStorageService)
		{
			_dbContext = dbContext;
			_blobStorageService = blobStorageService;
		}

		public async Task Handle(Command request, CancellationToken cancellationToken)
		{
			var thresholdDate = DateTime.Now.AddHours(-6);

			var filesQuery = _dbContext.Set<Domain.Model.File>()
									   .Where(x => x.UploadedOn <= thresholdDate &&
												   x.IsTemp);

			var filesIds = await filesQuery.Select(x => x.Id)
										   .ToArrayAsync(cancellationToken);

			await Task.WhenAll(filesIds.Select(id => _blobStorageService.DeleteAsync(id, true, cancellationToken)));

			await filesQuery.ExecuteDeleteAsync(cancellationToken);
		}
	}
}