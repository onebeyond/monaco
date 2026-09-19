using Monaco.Template.Backend.Common.Domain.Model.Contracts;

namespace Monaco.Template.Backend.Common.Infrastructure.Persistence;

public static class PersistenceActor
{
	public const string SubjectPrefix = "subject:";
	public const string Anonymous = "anonymous";
	public const string ApiHostRole = "api";
	public const string WorkerHostRole = "worker";
	public const string ResolutionStage = "resolution";
	public const string MissingCode = "PERSIST_ACTOR_MISSING";
	public const string MalformedCode = "PERSIST_ACTOR_MALFORMED";
	public const string OversizedCode = "PERSIST_ACTOR_OVERSIZED";
	public const string ThrewCode = "PERSIST_ACTOR_THREW";

	public static string? Resolve(IPersistenceActorProvider provider, PersistenceActorFallbackDiagnostics diagnostics)
	{
		string hostRole;
		try
		{
			hostRole = provider.HostRole;
		}
		catch
		{
			return null;
		}

		try
		{
			var actor = provider.GetActor();
			return actor is null
					   ? null
					   : Format(actor, hostRole, diagnostics);
		}
		catch
		{
			diagnostics.Report(ThrewCode, hostRole);
			return null;
		}
	}

	public static string? Format(string? candidate,
								 string hostRole,
								 PersistenceActorFallbackDiagnostics diagnostics)
	{
		if (string.IsNullOrWhiteSpace(candidate))
		{
			diagnostics.Report(MissingCode, hostRole);
			return null;
		}

		if (candidate.StartsWith(SubjectPrefix, StringComparison.Ordinal))
		{
			var suffix = candidate[SubjectPrefix.Length..];
			if (string.IsNullOrWhiteSpace(suffix))
			{
				diagnostics.Report(MissingCode, hostRole);
				return null;
			}

			if (ContainsControl(suffix))
			{
				diagnostics.Report(MalformedCode, hostRole);
				return null;
			}
		}

		if (candidate.Length > IAuditable.ActorMaxLength)
		{
			diagnostics.Report(OversizedCode, hostRole);
			return null;
		}

		return candidate;
	}

	private static bool ContainsControl(string value) =>
		value.Any(char.IsControl);
}