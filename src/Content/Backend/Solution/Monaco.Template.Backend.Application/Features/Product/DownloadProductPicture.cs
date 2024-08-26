using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Application.DTOs;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.Product;

public sealed class DownloadProductPicture
{
	public sealed record Query(Guid ProductId,
							   Guid PictureId,
							   IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryBase<FileDownloadDto?>(QueryString)
	{
		public bool? IsThumbnail => GetValueBool("thumbnail");
	};

	internal sealed class Handler : IRequestHandler<Query, FileDownloadDto?>
	{
		private readonly AppDbContext _dbContext;
		private readonly IFileService _fileService;

		public Handler(AppDbContext dbContext, IFileService fileService)
		{
			_dbContext = dbContext;
			_fileService = fileService;
		}

		public async Task<FileDownloadDto?> Handle(Query request, CancellationToken cancellationToken)
		{
			var query = _dbContext.Set<Domain.Model.Product>()
								  .AsNoTracking()
								  .Where(x => x.Id == request.ProductId)
								  .Select(x => x.Pictures
												.SingleOrDefault(p => p.Id == request.PictureId));

			if (request.IsThumbnail.HasValue && request.IsThumbnail.Value)
				query = query.Select(x => x!.Thumbnail);

			var item = await query.SingleOrDefaultAsync(cancellationToken);

			return item is null
					   ? null
					   : await _fileService.DownloadFileAsync(item,
															  cancellationToken);
		}
	}
}