using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Net;

namespace Monaco.Template.Backend.Common.Api.Swagger;

public class AuthorizeCheckOperationFilter : IOperationFilter
{
	private readonly string _audience;

	public AuthorizeCheckOperationFilter(string audience)
	{
		_audience = audience;
	}

	public void Apply(OpenApiOperation operation, OperationFilterContext context)
	{
		var allowAnonymous = context.MethodInfo
									.DeclaringType?
									.GetCustomAttributes(true)
									.OfType<AllowAnonymousAttribute>()
									.Any() ??
							 false;
		if (allowAnonymous)
			return;

		operation.Responses.Add(((int)HttpStatusCode.Unauthorized).ToString(),
								new OpenApiResponse { Description = HttpStatusCode.Unauthorized.ToString() });
		operation.Responses.Add(((int)HttpStatusCode.Forbidden).ToString(),
								new OpenApiResponse { Description = HttpStatusCode.Forbidden.ToString() });

		var oAuthScheme = new OpenApiSecurityScheme
						  {
							  Reference = new OpenApiReference
										  {
											  Id = "oauth2",
											  Type = ReferenceType.SecurityScheme
										  }
						  };

		operation.Security = new List<OpenApiSecurityRequirement>
							 {
								 new() { [oAuthScheme] = new List<string> { _audience } }
							 };
	}
}