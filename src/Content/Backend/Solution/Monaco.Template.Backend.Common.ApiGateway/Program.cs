using Monaco.Template.Backend.Common.ApiGateway.Auth;
using Monaco.Template.Backend.Common.Api.Auth;
using Monaco.Template.Backend.Common.Api.Cors;
using Monaco.Template.Backend.Common.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("reverseProxy.json", false, true);

// Add services to the container.
var configuration = builder.Configuration;
builder.Services.AddProblemDetails();
builder.Services
	   .AddAuthorization(cfg => Scopes.List.ForEach(s => cfg.AddPolicy(s, p => p.RequireScope(s)))) // Register all listed scopes as policies requiring the existence of such scope in User claims
	   .AddJwtBearerAuthentication(configuration["SSO:Authority"]!,
								   configuration["SSO:Audience"]!,
								   bool.Parse(configuration["SSO:RequireHttpsMetadata"] ?? "false"));
builder.Services
	   .AddCorsPolicies(configuration)
	   .AddReverseProxy()
	   .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddExceptionHandler<BoundaryExceptionHandler>();

builder.AddGatewayObservability();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler()
   .UseCors()
   .UseHttpsRedirection()
   .UseAuthentication()
   .UseIdentityEnrichment()
   .UseAuthorization();

app.MapReverseProxy();
app.Run();

namespace Monaco.Template.Backend.Common.ApiGateway
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	public partial class Program;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}