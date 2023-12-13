using MediatR;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.Image.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;

namespace Monaco.Template.Backend.Application.Features.Image;

public sealed class DownloadThumbnailByImageId
{
	public record Query(Guid Id) : QueryByIdBase<FileDownloadDto>(Id);

	public sealed class Handler : IRequestHandler<Query, FileDownloadDto?>
	{
		private readonly AppDbContext _dbContext;
		private readonly IBlobStorageService _blobStorageService;

		public Handler(AppDbContext dbContext, IBlobStorageService blobStorageService)
		{
			_dbContext = dbContext;
			_blobStorageService = blobStorageService;
		}

		public async Task<FileDownloadDto?> Handle(Query request, CancellationToken cancellationToken)
		{
			var item = await _dbContext.GetThumbnail(request.Id, cancellationToken);

			if (item is null)
				return null;

			var file = await _blobStorageService.DownloadAsync(item.Id, item.IsTemp, cancellationToken);

			return new(file,
					   $"{item.Name}{item.Extension}",
					   item.ContentType);
		}
	}
}