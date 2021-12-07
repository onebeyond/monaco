using Microsoft.AspNetCore.Builder;

namespace DcslGs.Template.Common.Api.Middleware.Extensions;

public static class SerilogContextExtensions
{
    public static IApplicationBuilder UseSerilogContextEnricher(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SerilogContextEnricherMiddleware>();
    }
}