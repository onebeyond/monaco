using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Common.Application.Queries;

namespace Monaco.Template.Backend.Application.Features.Country.Queries;

public record GetCountryByIdQuery(Guid Id) : QueryByIdBase<CountryDto?>(Id);