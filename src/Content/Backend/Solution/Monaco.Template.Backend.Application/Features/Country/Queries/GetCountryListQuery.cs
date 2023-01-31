using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.Country.Queries;

public record GetCountryListQuery(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryBase<List<CountryDto>>(QueryString);