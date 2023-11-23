#if filesSupport
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;

namespace Monaco.Template.Backend.Application.Features.File.Queries;

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

		return new(file,
				   $"{item.Name}{item.Extension}",
				   item.ContentType);
	}

	private Task<Domain.Model.File?> GetFile(Guid id, CancellationToken cancellationToken) =>
		_dbContext.Set<Domain.Model.File>()
				  .AsNoTracking()
				  .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
}
#endif