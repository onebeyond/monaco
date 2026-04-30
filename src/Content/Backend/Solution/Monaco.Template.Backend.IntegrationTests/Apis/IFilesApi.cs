using Refit;

namespace Monaco.Template.Backend.IntegrationTests.Apis;

internal interface IFilesApi
{
    [Multipart]
    [Post("/api/v1/Files")]
    Task<IApiResponse> Upload(StreamPart file);
}