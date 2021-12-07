using DcslGs.Template.Common.BlobStorage.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DcslGs.Template.Common.BlobStorage.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterBlobStorageService(this IServiceCollection services, string containerName, string connectionString)
    {
        return services.AddSingleton<IBlobStorageService>(new BlobStorageService(containerName, connectionString));
    }
}