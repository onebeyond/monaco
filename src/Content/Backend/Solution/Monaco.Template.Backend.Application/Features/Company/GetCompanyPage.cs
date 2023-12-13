using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class GetCompanyPage
{
	public record Query(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryPagedBase<CompanyDto>(QueryString)
	{
		public bool ExpandCountry => Expand(nameof(CompanyDto.Country));
	}

	public sealed class Handler : IRequestHandler<Query, Page<CompanyDto>?>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<Page<CompanyDto>?> Handle(Query request, CancellationToken cancellationToken)
		{
			var query = _dbContext.Set<Domain.Model.Company>()
								  .AsNoTracking();

			if (request.ExpandCountry)
				query = query.Include(x => x.Address!.Country);

			var page = await query.ApplyFilter(request.QueryString, CompanyExtensions.GetMappedFields())
								  .ApplySort(request.Sort, nameof(CompanyDto.Name), CompanyExtensions.GetMappedFields())
								  .ToPageAsync(request.Offset,
											   request.Limit,
											   x => x.Map(request.ExpandCountry)!,
											   cancellationToken);
			return page;
		}
	}
}
