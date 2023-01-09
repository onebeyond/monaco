using Microsoft.Extensions.Primitives;
using Monaco.Template.Application.DTOs;
using Monaco.Template.Common.Application.Queries;

namespace Monaco.Template.Application.Features.Country.Queries;

public record GetCountryListQuery(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryBase<List<CountryDto>>(QueryString);