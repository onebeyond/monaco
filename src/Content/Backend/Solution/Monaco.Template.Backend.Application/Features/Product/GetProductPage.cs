using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Application.Features.Product.DTOs;
using Monaco.Template.Backend.Application.Features.Product.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Product;

public sealed class GetProductPage
{
	public sealed record Query(IEnumerable<KeyValuePair<string, StringValues>> QueryParams) : QueryPagedBase<ProductDto, Domain.Model.Entities.Product>(QueryParams)
	{
		public bool ExpandCompany => Expand(nameof(ProductDto.Company));
		public bool ExpandPictures => Expand(nameof(ProductDto.Pictures));
		public bool ExpandDefaultPicture => Expand(nameof(ProductDto.DefaultPicture));

		public override Dictionary<string, Expression<Func<Domain.Model.Entities.Product, object>>> GetFilteringMappedFields() =>
			new()
			{
				[nameof(ProductDto.Id)] = x => x.Id,
				[nameof(ProductDto.Title)] = x => x.Title,
				[nameof(ProductDto.Description)] = x => x.Description,
				[nameof(ProductDto.Price)] = x => x.Price,
				[nameof(ProductDto.CompanyId)] = x => x.CompanyId,
				[$"{nameof(ProductDto.Company)}.{nameof(CompanyDto.Name)}"] = x => x.Company.Name,
				[nameof(ProductDto.DefaultPictureId)] = x => x.DefaultPictureId
			};
	}

	internal sealed class Handler : IRequestHandler<Query, QueryResult<Page<ProductDto>>>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<QueryResult<Page<ProductDto>>> Handle(Query request, CancellationToken cancellationToken)
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

			var page = await query.ApplyFilter(request.QueryParams, request.GetFilteringMappedFields())
								  .ApplySort(request.Sort, nameof(ProductDto.Title), request.GetSortingMappedFields())
								  .ToPageAsync(request.Offset,
											   request.Limit,
											   x => x.Map(request.ExpandCompany,
														  request.ExpandPictures,
														  request.ExpandDefaultPicture),
											   cancellationToken);
			return QueryResult<Page<ProductDto>>.Success(page);
		}
	}
}