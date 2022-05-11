using DcslGs.Template.Application.DTOs;
using DcslGs.Template.Common.Application.Queries;
using Microsoft.Extensions.Primitives;

namespace DcslGs.Template.Application.Features.Country.Queries;

public class GetCountryListQuery : QueryBase<List<CountryDto>>
{
    public GetCountryListQuery(IEnumerable<KeyValuePair<string, StringValues>> queryString) : base(queryString)
    {
    }
}