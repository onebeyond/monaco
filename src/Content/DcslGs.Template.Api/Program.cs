using DcslGs.Template.Api.Auth;
using DcslGs.Template.Common.Api.Auth;
using DcslGs.Template.Common.Api.Middleware.Extensions;
using DcslGs.Template.Common.Api.Swagger;
using DcslGs.Template.Common.Application.Commands.Behaviors;
#if includeFilesSupport
using DcslGs.Template.Common.BlobStorage.Extensions;
#endif
using DcslGs.Template.Common.Serilog;
using DcslGs.Template.Common.Serilog.ApplicationInsights.TelemetryConverters;
using DcslGs.Template.Infrastructure.Context;
#if includeFilesSupport
using DcslGs.Template.Infrastructure.Services.Extensions;
#endif
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using System.Reflection;
#if includeMassTransitSupport
using MassTransit;
#endif

[assembly: ApiConventionType(typeof(DefaultApiConventions))]

var builder = WebApplication.CreateBuilder(args);
builder.Host
	   .ConfigureLogging(b => b.ClearProviders())
	   .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration)
											  .WriteTo.Logger(l => l.WriteTo.Conditional(_ => context.HostingEnvironment.IsDevelopment(),	//Only for dev
																						 cfg => cfg.Debug()
																								   .WriteTo.File("logs/log.txt",
																												 rollingInterval: RollingInterval.Day,
																												 outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
																	.WriteTo.ApplicationInsights(context.Configuration["ApplicationInsights:InstrumentationKey"],
																								 new OperationTelemetryConverter(),
																								 LogEventLevel.Information)
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
	   .AddMediatR(GetApplicationAssembly())
	   .RegisterPreCommandProcessorBehaviors(GetApplicationAssembly())
	   .AddControllers()
	   .AddFluentValidation(config => config.RegisterValidatorsFromAssembly(GetApplicationAssembly()));

builder.Services.AddCors(x => x.AddDefaultPolicy(p => p.AllowAnyHeader()
													   .AllowAnyMethod()
													   .AllowAnyOrigin()));

builder.Services
	   .AddDbContext<AppDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("AppDbContext"),
																   sqlOptions =>
																   {
																	   sqlOptions.MigrationsAssembly("DcslGs.Template.Infrastructure.Migrations");
																	   sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(3), null);
																   })
													 .UseLazyLoadingProxies()
													 .EnableSensitiveDataLogging(bool.Parse(configuration["EnableEFSensitiveLogging"] ?? "false")));

#if includeMassTransitSupport
builder.Services
	   .AddMassTransit(x => x.UsingAzureServiceBus((_, cfg) => cfg.Host(configuration["MessageBus:ASBConnectionString"])));
#endif

#if includeFilesSupport
builder.Services
       .RegisterBlobStorageService(configuration["BlobStorage:Container"],
                                   configuration["BlobStorage:ConnectionString"])
       .RegisterFileService();
#endif

builder.Services.ConfigureApiVersionSwagger(configuration["SSO:Authority"],
											configuration["SSO:Audience"],
											Scopes.List,
											configuration["Swagger:ApiDescription"],
											configuration["Swagger:Title"],
											configuration["Swagger:Description"],
											configuration["Swagger:ContactName"],
											configuration["Swagger:ContactEmail"],
											configuration["Swagger:TermsOfService"]);

builder.Services
	   .AddHealthChecks()
	   .AddUrlGroup(new Uri($"{configuration["SSO:Authority"]}/.well-known/openid-configuration"), "SSO")
	   .AddDbContextCheck<AppDbContext>(nameof(AppDbContext));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
	app.UseDeveloperExceptionPage();

app.UseSwaggerConfiguration(app.Services.GetRequiredService<IApiVersionDescriptionProvider>(),
							configuration["SSO:SwaggerUIClientId"],
							configuration["Swagger:SwaggerUIAppName"]);

app.UseCors()
   .UseHttpsRedirection()
   .UseAuthorization()
   .UseSerilogContextEnricher();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => true });

app.MapControllers()
   .RequireAuthorization();

app.Run();


Assembly GetApplicationAssembly() => Assembly.Load("DcslGs.Template.Application");