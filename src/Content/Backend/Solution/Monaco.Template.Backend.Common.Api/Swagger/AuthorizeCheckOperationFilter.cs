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
		if (context.ApiDescription
				   .ActionDescriptor
				   .EndpointMetadata
				   .Any(m => m is IAllowAnonymous))
			return;

		if (!operation.Responses.ContainsKey(((int)HttpStatusCode.Unauthorized).ToString()))
			operation.Responses.Add(((int)HttpStatusCode.Unauthorized).ToString(),
									new OpenApiResponse { Description = HttpStatusCode.Unauthorized.ToString() });
		if (!operation.Responses.ContainsKey(((int)HttpStatusCode.Forbidden).ToString()))
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

		operation.Security = [new OpenApiSecurityRequirement { [oAuthScheme] = new List<string> { _audience } }];
	}
}