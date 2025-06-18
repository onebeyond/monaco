﻿using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Application.Features.Company.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Application.Features.Company;

public sealed class GetCompanyPage
{
	public sealed record Query(IEnumerable<KeyValuePair<string, StringValues>> QueryParams) : QueryPagedBase<CompanyDto>(QueryParams)
	{
		public bool ExpandCountry => Expand(nameof(CompanyDto.Country));
	}

	internal sealed class Handler : IRequestHandler<Query, Page<CompanyDto>?>
	{
		private readonly AppDbContext _dbContext;

		public Handler(AppDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<Page<CompanyDto>?> Handle(Query request, CancellationToken cancellationToken)
		{
			var query = _dbContext.Set<Domain.Model.Entities.Company>()
								  .AsNoTracking();

			if (request.ExpandCountry)
				query = query.Include(x => x.Address!.Country);

			var page = await query.ApplyFilter(request.QueryParams, CompanyExtensions.GetMappedFields())
								  .ApplySort(request.Sort, nameof(CompanyDto.Name), CompanyExtensions.GetMappedFields())
								  .ToPageAsync(request.Offset,
											   request.Limit,
											   x => x.Map(request.ExpandCountry)!,
											   cancellationToken);
			return page;
		}
	}
}
