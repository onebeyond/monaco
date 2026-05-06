using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Application.Features.Company.Extensions;
using Monaco.Template.Backend.Application.Features.Country.DTOs;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class GetCompanyPage
{
	public sealed record Query(IEnumerable<KeyValuePair<string, StringValues>> QueryParams) : QueryPagedBase<CompanyDto, Domain.Model.Entities.Company>(QueryParams)
	{
		public bool ExpandCountry => Expand(nameof(CompanyDto.Country));

		public override Dictionary<string, Expression<Func<Domain.Model.Entities.Company, object>>> GetFilteringMappedFields() =>
			new()
			{
				[nameof(CompanyDto.Id)] = x => x.Id,
				[nameof(CompanyDto.Name)] = x => x.Name,
				[nameof(CompanyDto.Email)] = x => x.Email,
				[nameof(CompanyDto.WebSiteUrl)] = x => x.WebSiteUrl!,
				[nameof(CompanyDto.Street)] = x => x.Address!.Street!,
				[nameof(CompanyDto.City)] = x => x.Address!.City!,
				[nameof(CompanyDto.County)] = x => x.Address!.County!,
				[nameof(CompanyDto.PostCode)] = x => x.Address!.PostCode!,
				[nameof(CompanyDto.CountryId)] = x => x.Address!.CountryId,
				[$"{nameof(CompanyDto.Country)}.{nameof(CountryDto.Name)}"] = x => x.Address!.Country.Name
			};
	}

	internal sealed class Handler : IRequestHandler<Query, QueryResult<Page<CompanyDto>>>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<QueryResult<Page<CompanyDto>>> Handle(Query request, CancellationToken cancellationToken)
		{
			var query = _dbContext.Set<Domain.Model.Entities.Company>()
								  .AsNoTracking();

			if (request.ExpandCountry)
				query = query.Include(x => x.Address!.Country);

			var page = await query.ApplyFilter(request.QueryParams, request.GetFilteringMappedFields())
								  .ApplySort(request.Sort, nameof(CompanyDto.Name), request.GetSortingMappedFields())
								  .ToPageAsync(request.Offset,
											   request.Limit,
											   x => x.Map(request.ExpandCountry)!,
											   cancellationToken);
			return QueryResult<Page<CompanyDto>>.Success(page);
		}
	}
}