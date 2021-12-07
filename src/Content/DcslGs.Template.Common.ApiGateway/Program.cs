using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("ocelot.json");
builder.Host
	   .ConfigureLogging(b => b.ClearProviders())
	   .UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
var configuration = builder.Configuration;
var authProviderKey = "keyCloak";

builder.Services
	   .AddAuthentication()
	   .AddJwtBearer(authProviderKey,
					 options =>
					 {
						 options.Authority = configuration["SSO:Authority"];
						 options.Audience = configuration["SSO:Audience"];
						 options.RequireHttpsMetadata = bool.Parse(configuration["SSO:RequireHttpsMetadata"] ?? "false");
					 });

builder.Services.AddCors(x => x.AddDefaultPolicy(p => p.AllowAnyHeader()
													   .AllowAnyMethod()
													   .AllowAnyOrigin()));
builder.Services.AddEndpointsApiExplorer();
builder.WebHost.ConfigureServices((context, services) =>
								  {
									  services.AddOcelot(context.Configuration);
									  services.AddSwaggerForOcelot(context.Configuration);
								  });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
	app.UseDeveloperExceptionPage();

app.UseCors()
   .UseSwaggerForOcelotUI(opt =>
						  {
							  opt.PathToSwaggerGenerator = "/swagger/docs";
							  opt.OAuthConfigObject.UsePkceWithAuthorizationCodeGrant = true;
							  opt.OAuthConfigObject.ScopeSeparator = " ";
						  })
   .UseOcelot()
   .Wait();

app.Run();
