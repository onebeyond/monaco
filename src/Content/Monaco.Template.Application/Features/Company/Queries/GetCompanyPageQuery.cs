using Monaco.Template.Common.Application.Queries;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Company.Queries;

public record GetCompanyPageQuery(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryPagedBase<CompanyDto>(QueryString)
{
	public bool ExpandCountry => Expand(nameof(CompanyDto.Country));
}