using DcslGs.Template.Infrastructure.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DcslGs.Template.Infrastructure.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterFileService(this IServiceCollection services)
    {
        return services.AddScoped<IFileService, FileService>();
    }
}