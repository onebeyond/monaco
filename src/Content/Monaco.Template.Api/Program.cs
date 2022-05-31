using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Monaco.Template.Api.Auth;
using Monaco.Template.Application.DependencyInjection;
using Monaco.Template.Application.Infrastructure.Context;
using Monaco.Template.Common.Api.Auth;
using Monaco.Template.Common.Api.Cors;
using Monaco.Template.Common.Api.Middleware.Extensions;
using Monaco.Template.Common.Api.Swagger;
using Monaco.Template.Common.Serilog;
using Monaco.Template.Common.Serilog.ApplicationInsights.TelemetryConverters;
using Serilog;

[assembly: ApiConventionType(typeof(DefaultApiConventions))]

var builder = WebApplication.CreateBuilder(args);
builder.Host
	   .ConfigureLogging(b => b.ClearProviders())
	   .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration)
											  .WriteTo.Logger(l => l.WriteTo.Conditional(_ => context.HostingEnvironment.IsDevelopment(),	//Only for dev
																						 cfg => cfg.Debug()
																								   .WriteTo.Console()
																								   .WriteTo.File("logs/log.txt",
																												 rollingInterval: RollingInterval.Day,
																												 outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
																	.WriteTo.Console()
																	.WriteTo.ApplicationInsights(context.Configuration["ApplicationInsights:InstrumentationKey"],
																								 new OperationTelemetryConverter())
																	.Filter.ByExcluding(x => x.Properties.ContainsKey("AuditEntries")))
											  .WriteTo.Logger(l => l.WriteTo.ApplicationInsights(context.Configuration["ApplicationInsights:InstrumentationKey"],
																								   new AuditEventTelemetryConverter())
																	.Filter.ByIncludingOnly(x => x.Properties.ContainsKey("AuditEntries")))
											  .Enrich.WithOperationId()
											  .Enrich.FromLogContext());

// Add services to the container.
var configuration = builder.Configuration;
builder.Services
	   .AddAuthorizationWithPolicies(Scopes.List)
	   .AddJwtBearerAuthentication(configuration["SSO:Authority"],
						  configuration["SSO:Audience"],
						  bool.Parse(configuration["SSO:RequireHttpsMetadata"] ?? "false"));

builder.Services
	   .ConfigureApplication(options =>
							 {
								 options.EntityFramework.ConnectionString = configuration.GetConnectionString("AppDbContext");
								 options.EntityFramework.EnableEfSensitiveLogging = bool.Parse(configuration["EnableEFSensitiveLogging"] ?? "false");
#if includeFilesSupport
								 options.BlobStorage.ConnectionString = configuration["BlobStorage:ConnectionString"];
								 options.BlobStorage.ContainerName = configuration["BlobStorage:Container"];
#endif
#if includeMassTransitSupport
								 options.MessageBus.AzureServiceBusConnectionString = configuration["Messaging:ASBConnectionString"];
#endif
							 })
	   .ConfigureApiVersionSwagger(configuration["SSO:Authority"],
								   configuration["SSO:Audience"],
								   Scopes.List,
								   configuration["Swagger:ApiDescription"],
								   configuration["Swagger:Title"],
								   configuration["Swagger:Description"],
								   configuration["Swagger:ContactName"],
								   configuration["Swagger:ContactEmail"],
								   configuration["Swagger:TermsOfService"])
	   .AddHealthChecks()
	   .AddUrlGroup(new Uri($"{configuration["SSO:Authority"]}/.well-known/openid-configuration"), "SSO")
	   .AddDbContextCheck<AppDbContext>(nameof(AppDbContext));

builder.Services
	   .AddCorsPolicies(configuration)
	   .AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
	app.UseDeveloperExceptionPage();

app.UseSwaggerConfiguration(configuration["SSO:SwaggerUIClientId"],
							configuration["Swagger:SwaggerUIAppName"]);

app.UseCors()
	.UseHttpsRedirection()
	.UseAuthorization()
	.UseSerilogContextEnricher();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => true });

app.MapControllers()
   .RequireAuthorization();

app.Run();