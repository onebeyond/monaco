#if (massTransitIntegration)
using MassTransit;
#endif
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Monaco.Template.Backend.Application.DependencyInjection;
#if (auth)
using Monaco.Template.Backend.Api.Auth;
using Monaco.Template.Backend.Common.Api.Auth;
#endif
using Monaco.Template.Backend.Common.Api.Cors;
using Monaco.Template.Backend.Common.Api.Middleware.Extensions;
using Monaco.Template.Backend.Api.Endpoints.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var configuration = builder.Configuration;
#if (auth)
builder.Services
	   .AddAuthorizationWithPolicies(Scopes.List)
	   .AddJwtBearerAuthentication(configuration["SSO:Authority"]!,
								   configuration["SSO:Audience"]!,
								   bool.Parse(configuration["SSO:RequireHttpsMetadata"] ?? bool.FalseString));
#endif

builder.Services
	   .AddProblemDetails()
	   .ConfigureApplication(options =>
							 {
								 options.EntityFramework.ConnectionString = configuration.GetConnectionString("AppDbContext")!;
								 options.EntityFramework.EnableEfSensitiveLogging = bool.Parse(configuration["EnableEFSensitiveLogging"] ?? bool.FalseString);
#if (filesSupport)
								 options.BlobStorage.ConnectionString = configuration["BlobStorage:ConnectionString"]!;
								 options.BlobStorage.ContainerName = configuration["BlobStorage:Container"]!;
#endif
							 })
#if (auth)
	   .AddOpenApiDocs(configuration["Scalar:AuthEndpoint"]!,
					   configuration["Scalar:TokenEndpoint"]!,
					   configuration["SSO:Audience"]!,
					   Scopes.List)
#else
	   .AddOpenApiDocs()
#endif
#if (massTransitIntegration)
	   .AddMassTransit(cfg =>
					   {
						   cfg.AddEntityFrameworkOutbox<AppDbContext>(o =>
																	  {
																		  o.QueryDelay = TimeSpan.FromSeconds(1);

																		  o.UseSqlServer();
																		  o.UseBusOutbox();
																		  // Disable it in API so only the Worker takes care of this.
																		  o.DisableInboxCleanupService();
																	  });

						   var rabbitMqConfig = configuration.GetSection("MessageBus:RabbitMQ");
						   if (rabbitMqConfig.Exists())
							   cfg.UsingRabbitMq((_, busCfg) => busCfg.Host(rabbitMqConfig["Host"],
																			ushort.Parse(rabbitMqConfig["Port"] ?? "5672"),
																			rabbitMqConfig["VHost"],
																			h =>
																			{
																				h.Username(rabbitMqConfig["Username"]!);
																				h.Password(rabbitMqConfig["Password"]!);
																			}));
						   else
							   cfg.UsingAzureServiceBus((_, busCfg) => busCfg.Host(configuration["MessageBus:ASBConnectionString"]));
					   })
#endif
	   .AddHealthChecks()
#if (auth)
	   .AddUrlGroup(new Uri($"{configuration["SSO:Authority"]}/.well-known/openid-configuration"), "SSO")
#endif
	   .AddDbContextCheck<AppDbContext>(nameof(AppDbContext));

builder.Services
	   .AddCorsPolicies(configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
	app.UseDeveloperExceptionPage();

app.UseExceptionHandler()
   .UseStatusCodePages();

#if (auth)
app.UseOpenApiDocs(configuration["Scalar:Title"]!,
				   configuration["Scalar:AuthEndpoint"]!,
				   configuration["Scalar:TokenEndpoint"]!,
				   configuration["Scalar:ClientId"]!,
				   configuration["SSO:Audience"]!,
				   Scopes.List);
#else
app.UseOpenApiDocs(configuration["Scalar:Title"]!);
#endif

app.UseCors()
   .UseRouting()
   .UseHttpsRedirection()
#if (auth)
   .UseAuthorization()
#endif
   .UseEndpoints(b => b.RegisterEndpoints());

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => true });

app.Run();

namespace Monaco.Template.Backend.Api
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	public partial class Program;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}