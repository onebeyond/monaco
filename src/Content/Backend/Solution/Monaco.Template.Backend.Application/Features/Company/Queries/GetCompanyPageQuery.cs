using Microsoft.Extensions.Primitives;
using Monaco.Template.Application.DTOs;
using Monaco.Template.Common.Application.Queries;

namespace Monaco.Template.Application.Features.Company.Queries;

public record GetCompanyPageQuery(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryPagedBase<CompanyDto>(QueryString)
{
	public bool ExpandCountry => Expand(nameof(CompanyDto.Country));
}