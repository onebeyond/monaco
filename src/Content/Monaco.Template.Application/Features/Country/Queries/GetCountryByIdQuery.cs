using Monaco.Template.Common.Application.Queries;
using Monaco.Template.Application.DTOs;

namespace Monaco.Template.Application.Features.Country.Queries;

public record GetCountryByIdQuery(Guid Id) : QueryByIdBase<CountryDto?>(Id);