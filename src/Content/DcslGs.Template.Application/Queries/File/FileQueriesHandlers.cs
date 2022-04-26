using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Application.DTOs.Extensions;
using DcslGs.Template.Common.BlobStorage.Contracts;
using DcslGs.Template.Infrastructure.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DcslGs.Template.Application.Queries.File;

public sealed class FileQueriesHandlers : IRequestHandler<GetFileByIdQuery, FileDto?>,
										  IRequestHandler<DownloadFileByIdQuery, FileDownloadDto?>
{
	private readonly AppDbContext _dbContext;
	private readonly IBlobStorageService _blobStorageService;

	public FileQueriesHandlers(AppDbContext dbContext, IBlobStorageService blobStorageService)
	{
		_dbContext = dbContext;
		_blobStorageService = blobStorageService;
	}

	public async Task<FileDto?> Handle(GetFileByIdQuery request, CancellationToken cancellationToken)
	{
		var item = await GetFile(request.Id, cancellationToken);
		return item.Map();
	}

	public async Task<FileDownloadDto?> Handle(DownloadFileByIdQuery request, CancellationToken cancellationToken)
	{
		var item = await GetFile(request.Id, cancellationToken);

		if (item == null)
			return null;

		var file = await _blobStorageService.DownloadAsync(request.Id, item.IsTemp, cancellationToken);

		var dto = new FileDownloadDto
				  {
					  FileContent = file,
					  FileName = $"{item.Name}{item.Extension}",
					  ContentType = item.ContentType
				  };
		return dto;
	}

	private async Task<Domain.Model.File?> GetFile(Guid id, CancellationToken cancellationToken)
	{
		var item = await _dbContext.Set<Domain.Model.File>()
								   .AsNoTracking()
								   .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
		return item;
	}
}