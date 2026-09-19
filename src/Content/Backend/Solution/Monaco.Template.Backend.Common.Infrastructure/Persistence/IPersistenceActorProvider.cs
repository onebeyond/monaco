namespace Monaco.Template.Backend.Common.Infrastructure.Persistence;

/// <summary>
/// Host-owned source of the single nullable persistence actor for a scoped unit of work.
/// </summary>
public interface IPersistenceActorProvider
{
	/// <summary>
	/// Bounded host role used in fallback diagnostics (<c>api</c> or <c>worker</c>).
	/// </summary>
	string HostRole { get; }

	/// <summary>
	/// Returns the canonical actor for this invocation, or <see langword="null"/> when resolution failed open.
	/// </summary>
	string? GetActor();
}