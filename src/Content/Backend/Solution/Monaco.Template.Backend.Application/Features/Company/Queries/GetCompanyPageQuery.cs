using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.Company.Queries;

public record GetCompanyPageQuery(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryPagedBase<CompanyDto>(QueryString)
{
	public bool ExpandCountry => Expand(nameof(CompanyDto.Country));
}