using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;

namespace Monaco.Template.Backend.Common.BlobStorage.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection RegisterBlobStorageService(this IServiceCollection services, Action<BlobStorageServiceOptions> options)
	{
		var optionsValue = new BlobStorageServiceOptions();
		options.Invoke(optionsValue);
		return services.AddSingleton<IBlobStorageService>(new BlobStorageService(new BlobServiceClient(optionsValue.ConnectionString),
																				 optionsValue.ContainerName!));
	}
}