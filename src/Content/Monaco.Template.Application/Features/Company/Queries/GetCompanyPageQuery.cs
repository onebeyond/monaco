using Monaco.Template.Common.Application.Queries;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Company.Queries;

public class GetCompanyPageQuery : QueryPagedBase<CompanyDto>
{
    public GetCompanyPageQuery(IEnumerable<KeyValuePair<string, StringValues>> queryString) : base(queryString)
    {
    }

    public bool ExpandCountry => Expand(nameof(CompanyDto.Country));
}