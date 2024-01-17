using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Monaco.Template.Backend.Common.Api.MinimalApi;

public static class MinimalApiExtensions
{
	public static RouteGroupBuilder CreateApiGroupBuilder(this IEndpointRouteBuilder builder,
														  ApiVersionSet versionSet,
														  string collectionName,
														  int version = 1) =>
		builder.MapGroup(string.Concat("api/v{apiVersion:apiVersion}/", collectionName))
			   .WithName(collectionName)
			   .WithDisplayName(collectionName)
			   .WithTags(collectionName)
			   .WithApiVersionSet(versionSet)
			   .HasApiVersion(version)
			   .RequireAuthorization();

	public static RouteHandlerBuilder MapGet(this IEndpointRouteBuilder builder,
											 string pattern,
											 Delegate handler,
											 string name,
											 string summary) =>
		builder.MapGet(pattern,
					   handler,
					   name,
					   summary,
					   string.Empty);

	public static RouteHandlerBuilder MapGet(this IEndpointRouteBuilder builder,
											 string pattern,
											 Delegate handler,
											 string name,
											 string summary,
											 string description) =>
		builder.MapGet(pattern,
					   handler)
			   .WithOpenApi()
			   .WithName(name)
			   .WithSummary(summary)
			   .WithDescription(description);

	public static RouteHandlerBuilder MapPost(this IEndpointRouteBuilder builder,
											  string pattern,
											  Delegate handler,
											  string name,
											  string summary) =>
		builder.MapPost(pattern,
						handler,
						name,
						summary,
						string.Empty);

	public static RouteHandlerBuilder MapPost(this IEndpointRouteBuilder builder,
											  string pattern,
											  Delegate handler,
											  string name,
											  string summary,
											  string description) =>
		builder.MapPost(pattern,
						handler)
			   .WithOpenApi()
			   .WithName(name)
			   .WithSummary(summary)
			   .WithDescription(description);

	public static RouteHandlerBuilder MapPut(this IEndpointRouteBuilder builder,
											 string pattern,
											 Delegate handler,
											 string name,
											 string summary) =>
		builder.MapPut(pattern,
					   handler,
					   name,
					   summary,
					   string.Empty);

	public static RouteHandlerBuilder MapPut(this IEndpointRouteBuilder builder,
											 string pattern,
											 Delegate handler,
											 string name,
											 string summary,
											 string description) =>
		builder.MapPut(pattern,
					   handler)
			   .WithOpenApi()
			   .WithName(name)
			   .WithSummary(summary)
			   .WithDescription(description);

	public static RouteHandlerBuilder MapDelete(this IEndpointRouteBuilder builder,
												string pattern,
												Delegate handler,
												string name,
												string summary) =>
		builder.MapDelete(pattern,
						  handler,
						  name,
						  summary,
						  string.Empty);

	public static RouteHandlerBuilder MapDelete(this IEndpointRouteBuilder builder,
												string pattern,
												Delegate handler,
												string name,
												string summary,
												string description) =>
		builder.MapDelete(pattern,
						  handler)
			   .WithOpenApi()
			   .WithName(name)
			   .WithSummary(summary)
			   .WithDescription(description);
}