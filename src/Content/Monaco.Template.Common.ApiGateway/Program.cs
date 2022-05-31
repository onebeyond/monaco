using Monaco.Template.Common.Api.Auth;
using Monaco.Template.Common.Api.Cors;
using Monaco.Template.Common.ApiGateway.Auth;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host
	   .ConfigureHostConfiguration(cfg => cfg.AddJsonFile("reverseProxy.json", false, true))
	   .ConfigureLogging(b => b.ClearProviders())
	   .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
var configuration = builder.Configuration;
builder.Services
	   .AddAuthorization(cfg => Scopes.List.ForEach(s => cfg.AddPolicy(s, p => p.RequireClaim("scope", s)))) //Register all listed scopes as policies requiring the existance of such scope in User claims
	   .AddJwtBearerAuthentication(configuration["SSO:Authority"],
								   configuration["SSO:Audience"],
								   bool.Parse(configuration["SSO:RequireHttpsMetadata"] ?? "false"));
builder.Services
	   .AddCorsPolicies(configuration)
	   .AddReverseProxy()
	   .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
	app.UseDeveloperExceptionPage();

app.UseSerilogRequestLogging()
   .UseCors()
   .UseHttpsRedirection()
   .UseAuthentication()
   .UseAuthorization();

app.MapReverseProxy();
app.Run();
