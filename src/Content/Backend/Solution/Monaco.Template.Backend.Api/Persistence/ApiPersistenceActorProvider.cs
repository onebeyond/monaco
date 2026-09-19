using Monaco.Template.Backend.Common.Infrastructure.Persistence;

namespace Monaco.Template.Backend.Api.Persistence;

/// <summary>
/// Resolves a persistence actor from the current HTTP request, or a system actor when no request exists.
/// </summary>
public sealed class ApiPersistenceActorProvider : IPersistenceActorProvider
{
	/// <summary>
	/// Canonical actor for API work with no HTTP request (<c>system:</c> plus the API root namespace).
	/// </summary>
	public static readonly string SystemActor = $"system:{typeof(Program).Namespace}";

	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly PersistenceActorFallbackDiagnostics _diagnostics;

	/// <summary>
	/// Creates a provider that reads the current request from <paramref name="httpContextAccessor"/>.
	/// </summary>
	public ApiPersistenceActorProvider(IHttpContextAccessor httpContextAccessor,
									   PersistenceActorFallbackDiagnostics diagnostics)
	{
		_httpContextAccessor = httpContextAccessor;
		_diagnostics = diagnostics;
	}

	/// <inheritdoc />
	public string HostRole =>
		PersistenceActor.ApiHostRole;

	/// <inheritdoc />
	public string? GetActor()
	{
		var httpContext = _httpContextAccessor.HttpContext;
		return PersistenceActor.Format(httpContext switch
									   {
										   null => SystemActor,
										   { User.Identity.IsAuthenticated: true } => PersistenceActor.SubjectPrefix + httpContext.User
																																  .FindFirst("sub")
																																  ?.Value,
										   _ => PersistenceActor.Anonymous
									   },
									   HostRole,
									   _diagnostics);
	}
}