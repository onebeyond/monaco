using Monaco.Template.Common.Application.Queries;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Country.Queries;

public record GetCountryListQuery : QueryBase<List<CountryDto>>
{
    public GetCountryListQuery(IEnumerable<KeyValuePair<string, StringValues>> queryString) : base(queryString)
    {
    }
}