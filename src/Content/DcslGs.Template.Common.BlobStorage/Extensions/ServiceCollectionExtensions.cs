using DcslGs.Template.Common.BlobStorage.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DcslGs.Template.Common.BlobStorage.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterBlobStorageService(this IServiceCollection services, Action<BlobStorageServiceOptions> options)
	{
		services.AddOptions<BlobStorageServiceOptions>();
		return services.Configure(options)
					   .AddSingleton<IBlobStorageService, BlobStorageService>();
    }
}