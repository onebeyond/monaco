#if filesSupport
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;

namespace Monaco.Template.Backend.Application.Features.Image.Queries;

public sealed class ImageQueriesHandlers(AppDbContext dbContext, IBlobStorageService blobStorageService) : IRequestHandler<GetImageByIdQuery, ImageDto?>,
																										   IRequestHandler<GetThumbnailByImageIdQuery, ImageDto?>,
																										   IRequestHandler<DownloadThumbnailByImageIdQuery, FileDownloadDto?>
{
	public async Task<ImageDto?> Handle(GetImageByIdQuery request, CancellationToken cancellationToken)
	{
		var item = await GetImage(request.Id, cancellationToken);
		return item.Map();
	}

	public async Task<ImageDto?> Handle(GetThumbnailByImageIdQuery request, CancellationToken cancellationToken)
	{
		var item = await GetThumbnail(request.Id, cancellationToken);
		return item.Map();
	}

	public async Task<FileDownloadDto?> Handle(DownloadThumbnailByImageIdQuery request, CancellationToken cancellationToken)
	{
		var item = await GetThumbnail(request.Id, cancellationToken);

		if (item == null)
			return null;

		var file = await blobStorageService.DownloadAsync(item.Id, item.IsTemp, cancellationToken);

		return new FileDownloadDto(file,
								   $"{item.Name}{item.Extension}",
								   item.ContentType);
	}

	private Task<Domain.Model.Image?> GetImage(Guid id, CancellationToken cancellationToken) =>
		dbContext.Set<Domain.Model.Image>()
				  .AsNoTracking()
				  .Include(x => x.Thumbnail)
				  .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

	private Task<Domain.Model.Image?> GetThumbnail(Guid id, CancellationToken cancellationToken) =>
		dbContext.Set<Domain.Model.Image>()
				  .AsNoTracking()
				  .Where(x => x.Id == id)
				  .Select(x => x.Thumbnail)
				  .SingleOrDefaultAsync(cancellationToken);
}
#endif