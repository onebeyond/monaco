using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;
using Microsoft.Extensions.Primitives;

namespace DcslGs.Template.Application.Features.Company.Queries;

public class GetCompanyPageQuery : QueryPagedBase<CompanyDto>
{
    public GetCompanyPageQuery(IEnumerable<KeyValuePair<string, StringValues>> queryString) : base(queryString)
    {
    }

    public bool ExpandCountry => Expand(nameof(CompanyDto.Country));
}