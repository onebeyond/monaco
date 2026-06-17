using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Domain.Model;
using Refit;

namespace Monaco.Template.Backend.IntegrationTests.Apis;

internal interface ICompaniesApi
{
    [Get("/api/v1/Companies")]
    Task<IApiResponse<Page<CompanyDto>>> Query([Query(CollectionFormat.Multi)] string[]? expand = null,
                                               int? offset = null,
                                               int? limit = null);

    [Get("/api/v1/Companies/{id}")]
    Task<IApiResponse<CompanyDto>> Get(Guid id);

    [Post("/api/v1/Companies")]
    Task<IApiResponse<CreatedResponse>> Create([Body] CompanyCreateEditDto dto);

    [Put("/api/v1/Companies/{id}")]
    Task<IApiResponse> Update(Guid id, [Body] CompanyCreateEditDto dto);

    [Delete("/api/v1/Companies/{id}")]
    Task<IApiResponse> Delete(Guid id);
}
