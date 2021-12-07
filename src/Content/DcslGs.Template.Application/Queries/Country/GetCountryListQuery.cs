using Microsoft.Extensions.Primitives;
using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;

namespace DcslGs.Template.Application.Queries.Country;

public class GetCountryListQuery : QueryBase<List<CountryDto>>
{
    public GetCountryListQuery(IEnumerable<KeyValuePair<string, StringValues>> queryString) : base(queryString)
    {
    }
}