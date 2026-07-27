using Monaco.Template.Backend.Common.ApiGateway.Auth;
using Monaco.Template.Backend.Common.Api.Auth;
using Monaco.Template.Backend.Common.Api.Cors;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("reverseProxy.json", false, true);

// Add services to the container.
var configuration = builder.Configuration;
builder.Services
	   .AddAuthorization(cfg => Scopes.List.ForEach(s => cfg.AddPolicy(s, p => p.RequireScope(s)))) // Register all listed scopes as policies requiring the existence of such scope in User claims
	   .AddJwtBearerAuthentication(configuration["SSO:Authority"]!,
								   configuration["SSO:Audience"]!,
								   bool.Parse(configuration["SSO:RequireHttpsMetadata"] ?? "false"));
builder.Services
	   .AddCorsPolicies(configuration)
	   .AddReverseProxy()
	   .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
	app.UseDeveloperExceptionPage();

app.UseCors()
   .UseHttpsRedirection()
   .UseAuthentication()
   .UseAuthorization();

app.MapReverseProxy();
app.Run();
