using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Monaco.Template.Backend.Application.Features.File;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Api.MinimalApi;
#if (auth)
using Monaco.Template.Backend.Api.Auth;
#endif

namespace Monaco.Template.Backend.Api.Endpoints;

internal static class Files
{
	extension(IEndpointRouteBuilder builder)
	{
		public IEndpointRouteBuilder AddFiles(ApiVersionSet versionSet)
		{
			var files = builder.CreateApiGroupBuilder(versionSet,
													  "Files");

			files.MapPost("",
						  Task<Results<Created<CreatedResponse>, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ([FromServices] ISender sender,
																															IFormFile file,
																															HttpContext context,
																															CancellationToken cancellationToken) =>
							  sender.ExecuteCommandCreatedAsync(new CreateFile.Command(file.OpenReadStream(),
																					   file.FileName,
																					   file.ContentType),
																"api/v{0}/Files/{1}",
																[context.GetRequestedApiVersion()!],
																cancellationToken),
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
}