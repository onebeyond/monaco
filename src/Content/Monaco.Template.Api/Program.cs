#if massTransitIntegration
using MassTransit;
#endif
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
#if (!disableAuth)
using Monaco.Template.Api.Auth;
#endif
using Monaco.Template.Application.DependencyInjection;
using Monaco.Template.Application.Infrastructure.Context;
#if (!disableAuth)
using Monaco.Template.Common.Api.Auth;
#endif
using Monaco.Template.Common.Api.Cors;
using Monaco.Template.Common.Api.Middleware.Extensions;
using Monaco.Template.Common.Api.Swagger;
using Monaco.Template.Common.Serilog;
using Monaco.Template.Common.Serilog.ApplicationInsights.TelemetryConverters;
using Serilog;

[assembly: ApiConventionType(typeof(DefaultApiConventions))]

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration)
												   .WriteTo.Logger(l => l.WriteTo.Conditional(_ => context.HostingEnvironment.IsDevelopment(),	//Only for dev
																							  cfg => cfg.Debug()
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
											  .Enrich.FromLogContext());

// Add services to the container.
var configuration = builder.Configuration;
#if (!disableAuth)
builder.Services
	   .AddAuthorizationWithPolicies(Scopes.List)
	   .AddJwtBearerAuthentication(configuration["SSO:Authority"]!,
								   configuration["SSO:Audience"]!,
								   bool.Parse(configuration["SSO:RequireHttpsMetadata"] ?? "false"));
#endif

builder.Services
	   .ConfigureApplication(options =>
							 {
								 options.EntityFramework.ConnectionString = configuration.GetConnectionString("AppDbContext")!;
								 options.EntityFramework.EnableEfSensitiveLogging = bool.Parse(configuration["EnableEFSensitiveLogging"] ?? "false");
#if filesSupport
								 options.BlobStorage.ConnectionString = configuration["BlobStorage:ConnectionString"]!;
								 options.BlobStorage.ContainerName = configuration["BlobStorage:Container"]!;
#endif
							 })
#if disableAuth
	   .ConfigureApiVersionSwagger(configuration["Swagger:ApiDescription"]!,
								   configuration["Swagger:Title"]!,
								   configuration["Swagger:Description"]!,
								   configuration["Swagger:ContactName"]!,
								   configuration["Swagger:ContactEmail"]!,
								   configuration["Swagger:TermsOfService"]!)
#else
	   .ConfigureApiVersionSwagger(configuration["Swagger:ApiDescription"]!,
								   configuration["Swagger:Title"]!,
								   configuration["Swagger:Description"]!,
								   configuration["Swagger:ContactName"]!,
								   configuration["Swagger:ContactEmail"]!,
								   configuration["Swagger:TermsOfService"]!,
								   configuration["SSO:Authority"],
								   configuration["SSO:Audience"],
								   Scopes.List)
#endif
#if massTransitIntegration
	   .AddMassTransit(cfg =>
					   {
						   if (builder.Environment.IsDevelopment())
							   cfg.UsingRabbitMq((_, busCfg) =>
												 {
													 var rabbitMqConfig = configuration.GetSection("MessageBus:RabbitMQ");
													 busCfg.Host(rabbitMqConfig["Host"],
																 rabbitMqConfig["VHost"],
																 h =>
																 {
																	 h.Username(rabbitMqConfig["Username"]);
																	 h.Password(rabbitMqConfig["Password"]);
																 });
												 });
						   else
							   cfg.UsingAzureServiceBus((_, busCfg) => busCfg.Host(configuration["MessageBus:ASBConnectionString"]));
					   })
#endif
	   .AddHealthChecks()
#if disableAuth
	   .AddUrlGroup(new Uri($"{configuration["SSO:Authority"]}/.well-known/openid-configuration"), "SSO")
#endif
	   .AddDbContextCheck<AppDbContext>(nameof(AppDbContext));

builder.Services
	   .AddCorsPolicies(configuration)
	   .AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
	app.UseDeveloperExceptionPage();

#if (disableAuth)
app.UseSwaggerConfiguration();
#else
app.UseSwaggerConfiguration(configuration["SSO:SwaggerUIClientId"]!,
							configuration["Swagger:SwaggerUIAppName"]!);
#endif

app.UseCors()
#if (!disableAuth)
   .UseHttpsRedirection()
   .UseAuthorization()
#endif
   .UseSerilogContextEnricher();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => true });

#if disableAuth
app.MapControllers();
#else
app.MapControllers()
   .RequireAuthorization();
#endif

app.Run();