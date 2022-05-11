#if includeFilesSupport
using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Application.DTOs.Extensions;
using DcslGs.Template.Application.Infrastructure.Context;
using DcslGs.Template.Common.BlobStorage.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DcslGs.Template.Application.Features.Image.Queries;

public sealed class ImageQueriesHandlers : IRequestHandler<GetImageByIdQuery, ImageDto?>,
										   IRequestHandler<GetThumbnailByImageIdQuery, ImageDto?>,
										   IRequestHandler<DownloadThumbnailByImageIdQuery, FileDownloadDto?>
{
	private readonly AppDbContext _dbContext;
	private readonly IBlobStorageService _blobStorageService;

	public ImageQueriesHandlers(AppDbContext dbContext, IBlobStorageService blobStorageService)
	{
		_dbContext = dbContext;
		_blobStorageService = blobStorageService;
	}

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

		var file = await _blobStorageService.DownloadAsync(item.Id, item.IsTemp, cancellationToken);

		var dto = new FileDownloadDto
				  {
					  FileContent = file,
					  FileName = $"{item.Name}{item.Extension}",
					  ContentType = item.ContentType
				  };
		return dto;
	}

	private async Task<Domain.Model.Image?> GetImage(Guid id, CancellationToken cancellationToken)
	{
		var item = await _dbContext.Set<Domain.Model.Image>()
								   .AsNoTracking()
								   .Include(x => x.Thumbnail)
								   .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
		return item;
	}

	private async Task<Domain.Model.Image?> GetThumbnail(Guid id, CancellationToken cancellationToken)
	{
		var item = await _dbContext.Set<Domain.Model.Image>()
								   .AsNoTracking()
								   .Where(x => x.Id == id)
								   .Select(x => x.Thumbnail)
								   .SingleOrDefaultAsync(cancellationToken);
		return item;
	}
}
#endif