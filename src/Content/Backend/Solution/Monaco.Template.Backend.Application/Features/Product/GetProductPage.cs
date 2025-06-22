using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Features.Product.DTOs;
using Monaco.Template.Backend.Application.Features.Product.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Product;

public sealed class GetProductPage
{
	public sealed record Query(IEnumerable<KeyValuePair<string, StringValues>> QueryParams) : QueryPagedBase<ProductDto>(QueryParams)
	{
		public bool ExpandCompany => Expand(nameof(ProductDto.Company));

		public bool ExpandPictures => Expand(nameof(ProductDto.Pictures));

		public bool ExpandDefaultPicture => Expand(nameof(ProductDto.DefaultPicture));
	}

	internal sealed class Handler : IRequestHandler<Query, Page<ProductDto>?>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<Page<ProductDto>?> Handle(Query request, CancellationToken cancellationToken)
		{
			var query = _dbContext.Set<Domain.Model.Entities.Product>()
								  .AsNoTracking();

			if (request.ExpandCompany)
				query = query.Include(x => x.Company);
			if (request.ExpandPictures)
				query = query.Include(x => x.Pictures)
							 .ThenInclude(x => x.Thumbnail);
			if (request.ExpandDefaultPicture)
				query = query.Include(x => x.DefaultPicture);

			var page = await query.ApplyFilter(request.QueryParams, ProductExtensions.GetMappedFields())
								  .ApplySort(request.Sort, nameof(ProductDto.Title), ProductExtensions.GetMappedFields())
								  .ToPageAsync(request.Offset,
											   request.Limit,
											   x => x.Map(request.ExpandCompany,
														  request.ExpandPictures,
														  request.ExpandDefaultPicture)!,
											   cancellationToken);
			return page;
		}
	}
}