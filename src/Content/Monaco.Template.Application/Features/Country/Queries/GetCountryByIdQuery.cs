using Monaco.Template.Application.DTOs;
using Monaco.Template.Common.Application.Queries;

namespace Monaco.Template.Application.Features.Country.Queries;

public record GetCountryByIdQuery(Guid Id) : QueryByIdBase<CountryDto?>(Id);