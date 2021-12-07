using Microsoft.Extensions.Primitives;
using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Queries.Company;

public class GetCompanyPageQuery : QueryPagedBase<CompanyDto>
{
    public GetCompanyPageQuery(IEnumerable<KeyValuePair<string, StringValues>> queryString) : base(queryString)
    {
    }

    public bool ExpandCountry => Expand(nameof(CompanyDto.Country));
}