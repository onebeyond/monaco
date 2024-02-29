using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
#if (auth)
using Monaco.Template.Backend.Api.Auth;
#endif
using Monaco.Template.Backend.Application.Features.File;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Api.MinimalApi;

namespace Monaco.Template.Backend.Api.Endpoints;

public static class Files
{
	public static IEndpointRouteBuilder AddFiles(this IEndpointRouteBuilder builder, ApiVersionSet versionSet)
	{
		var files = builder.CreateApiGroupBuilder(versionSet, "Files");

		files.MapPost("",
					  Task<Results<Created<Guid>, NotFound, ValidationProblem>> ([FromServices] ISender sender,
																				 [FromForm] IFormFile file,
																				 HttpContext context) =>
						  sender.ExecuteCommandAsync(new CreateFile.Command(file.OpenReadStream(),
																			file.FileName,
																			file.ContentType),
													 "api/v{0}/Files/{1}",
													 context.GetRequestedApiVersion()!),
					  "CreateFile",
					  "Upload and create a new file")
#if (!auth)
			 .DisableAntiforgery();
#else
			 .DisableAntiforgery()
			 .RequireAuthorization(Scopes.FilesWrite);
#endif

		return builder;
	}
}