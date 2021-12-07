using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DcslGs.Template.Common.Api.Swagger;

public static class ConfigureSwaggerExtensions
{
    public static IServiceCollection ConfigureSwagger(this IServiceCollection services,
                                                      string authority,
                                                      string apiName,
                                                      List<string> scopesList,
                                                      string apiDescription,
                                                      string title,
                                                      string description,
                                                      string contactName,
                                                      string contactEmail,
                                                      string termsOfServiceUrl)
    {
        var scopes = new Dictionary<string, string>(scopesList.ToDictionary(x => x, _ => "")) { { apiName, apiDescription } };

        return services.AddTransient<IConfigureOptions<SwaggerGenOptions>, SwaggerOptions>(provider => new SwaggerOptions(provider.GetRequiredService<IApiVersionDescriptionProvider>(),
                                                                                                                          title,
                                                                                                                          description,
                                                                                                                          contactName,
                                                                                                                          contactEmail,
                                                                                                                          termsOfServiceUrl))
                       .AddSwaggerGen(options =>
                                      {
                                          //Add security for authenticated APIs
                                          options.AddSecurityDefinition("oauth2",
                                                                        new OpenApiSecurityScheme
                                                                        {
                                                                            Type = SecuritySchemeType.OAuth2,
                                                                            Flows = new OpenApiOAuthFlows
                                                                                    {
                                                                                        AuthorizationCode = new OpenApiOAuthFlow
                                                                                                            {
                                                                                                                AuthorizationUrl = new Uri($"{authority}/protocol/openid-connect/auth"),
                                                                                                                TokenUrl = new Uri($"{authority}/protocol/openid-connect/token"),
                                                                                                                Scopes = scopes
                                                                                                            }
                                                                                    }
                                                                        });
                                          // add a custom operation filter which sets default values
                                          options.OperationFilter<SwaggerDefaultValues>();
                                          options.OperationFilter<AuthorizeCheckOperationFilter>(apiName);
                                          // integrate xml comments
                                          var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml");
                                          foreach (var xmlFile in xmlFiles)
                                              options.IncludeXmlComments(xmlFile);
                                      });
    }

    public static IServiceCollection ConfigureApiVersionSwagger(this IServiceCollection services,
                                                                string authority,
                                                                string apiName,
                                                                List<string> scopes,
                                                                string apiDescription,
                                                                string title,
                                                                string description,
                                                                string contactName,
                                                                string contactEmail,
                                                                string termsOfServiceUrl)
    {
        return services.AddApiVersioning(options =>
                                         {
                                             options.Conventions.Add(new VersionByNamespaceConvention());
                                             options.ReportApiVersions = true;
                                             options.DefaultApiVersion = new ApiVersion(1, 0);
                                             options.AssumeDefaultVersionWhenUnspecified = true;
                                         })
                       .AddVersionedApiExplorer(options =>
                                                {
                                                    options.GroupNameFormat = "'v'VVV";
                                                    options.SubstituteApiVersionInUrl = true;
                                                })
                       .ConfigureSwagger(authority,
                                         apiName,
                                         scopes,
                                         apiDescription,
                                         title,
                                         description,
                                         contactName,
                                         contactEmail,
                                         termsOfServiceUrl);
    }

    public static IApplicationBuilder UseSwaggerConfiguration(this IApplicationBuilder app,
                                                              IApiVersionDescriptionProvider provider,
                                                              string clientId,
                                                              string appName)
    {
        return app.UseSwagger() // Enable middleware to serve generated Swagger as a JSON endpoint.
                  .UseSwaggerUI(options =>
                                {    // build a swagger endpoint for each discovered API version
                                    foreach (var description in provider.ApiVersionDescriptions)
                                        options.SwaggerEndpoint($"{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                                    options.OAuthClientId(clientId);
                                    options.OAuthAppName(appName);
                                    options.OAuthScopeSeparator(" ");
                                    options.OAuthUsePkce();
                                });
    }

    /// <summary>
    /// Configures the Swagger generation options.
    /// </summary>
    /// <remarks>This allows API versioning to define a Swagger document per API version after the
    /// <see cref="IApiVersionDescriptionProvider"/> service has been resolved from the service container.</remarks>
    public class SwaggerOptions : IConfigureOptions<SwaggerGenOptions>
    {
        private readonly IApiVersionDescriptionProvider _provider;
        private readonly string _title;
        private readonly string _description;
        private readonly string _contactName;
        private readonly string _contactEmail;
        private readonly string _termsOfServiceUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="SwaggerOptions"/> class.
        /// </summary>
        /// <param name="provider">The <see cref="IApiVersionDescriptionProvider">provider</see> used to generate Swagger documents.</param>
        /// <param name="title"></param>
        /// <param name="description"></param>
        /// <param name="contactName"></param>
        /// <param name="contactEmail"></param>
        /// <param name="termsOfServiceUrl"></param>
        public SwaggerOptions(IApiVersionDescriptionProvider provider,
                              string title,
                              string description,
                              string contactName,
                              string contactEmail,
                              string termsOfServiceUrl)
        {
            _provider = provider;
            _title = title;
            _description = description;
            _contactName = contactName;
            _contactEmail = contactEmail;
            _termsOfServiceUrl = termsOfServiceUrl;
        }

        /// <inheritdoc />
        public void Configure(SwaggerGenOptions options)
        {
            // add a swagger document for each discovered API version
            // note: you might choose to skip or document deprecated API versions differently
            foreach (var description in _provider.ApiVersionDescriptions)
                options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
        }

        private OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
        {
            var info = new OpenApiInfo
                       {
                           Title = _title,
                           Version = description.ApiVersion.ToString(),
                           Description = _description,
                           Contact = new OpenApiContact { Name = _contactName, Email = _contactEmail },
                           TermsOfService = new Uri(_termsOfServiceUrl)
                       };

            if (description.IsDeprecated)
                info.Description += " This API version has been deprecated.";

            return info;
        }
    }
}