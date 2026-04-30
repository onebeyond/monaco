using Monaco.Template.Backend.Application.Features.Country.DTOs;
using Refit;

namespace Monaco.Template.Backend.IntegrationTests.Apis;

internal interface ICountriesApi
{
    [Get("/api/v1/Countries")]
    Task<IApiResponse<CountryDto[]>> Query();

    [Get("/api/v1/Countries/{id}")]
    Task<IApiResponse<CountryDto>> Get(Guid id);
}
