using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Application.Features.Product.DTOs;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Domain.Model;
using Refit;

namespace Monaco.Template.Backend.IntegrationTests.Apis;

internal interface IProductsApi
{
    [Get("/api/v1/Products")]
    Task<IApiResponse<Page<ProductDto>>> Query([Query(CollectionFormat.Multi)] string[]? expand = null,
                                               int? offset = null,
                                               int? limit = null);

    [Get("/api/v1/Products/{id}")]
    Task<IApiResponse<ProductDto>> Get(Guid id);

    [Get("/api/v1/Products/{productId}/Pictures/{pictureId}")]
    Task<HttpResponseMessage> DownloadPicture(Guid productId,
                                              Guid pictureId,
                                              bool? thumbnail = null);

    [Post("/api/v1/Products")]
    Task<IApiResponse<CreatedResponse>> Create([Body] ProductCreateEditDto dto);

    [Put("/api/v1/Products/{id}")]
    Task<IApiResponse> Update(Guid id, [Body] ProductCreateEditDto dto);

    [Delete("/api/v1/Products/{id}")]
    Task<IApiResponse> Delete(Guid id);
}