using Microsoft.Extensions.DependencyInjection;
using Monaco.Template.Common.BlobStorage.Contracts;

namespace Monaco.Template.Common.BlobStorage.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterBlobStorageService(this IServiceCollection services, Action<BlobStorageServiceOptions> options)
	{
		services.AddOptions<BlobStorageServiceOptions>();
		return services.Configure(options)
					   .AddSingleton<IBlobStorageService, BlobStorageService>();
    }
}