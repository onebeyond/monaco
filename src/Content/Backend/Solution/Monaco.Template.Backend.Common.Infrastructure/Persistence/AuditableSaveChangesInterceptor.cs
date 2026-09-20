using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Monaco.Template.Backend.Common.Domain.Model.Contracts;

namespace Monaco.Template.Backend.Common.Infrastructure.Persistence;

public sealed class AuditableSaveChangesInterceptor : SaveChangesInterceptor
{
	private static readonly HashSet<string> StampPropertyNames =
	[
		nameof(IAuditable.CreatedAtUtc),
		nameof(IAuditable.CreatedBy),
		nameof(IAuditable.ModifiedAtUtc),
		nameof(IAuditable.ModifiedBy)
	];

	private readonly IPersistenceActorProvider _actorProvider;
	private readonly PersistenceActorFallbackDiagnostics _diagnostics;
	private readonly TimeProvider _timeProvider;

	public AuditableSaveChangesInterceptor(IPersistenceActorProvider actorProvider,
										   PersistenceActorFallbackDiagnostics diagnostics,
										   TimeProvider timeProvider)
	{
		_actorProvider = actorProvider;
		_diagnostics = diagnostics;
		_timeProvider = timeProvider;
	}

	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		ApplyStamps(eventData.Context);
		return result;
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
																		  InterceptionResult<int> result,
																		  CancellationToken cancellationToken = default)
	{
		ApplyStamps(eventData.Context);
		return ValueTask.FromResult(result);
	}

	private void ApplyStamps(DbContext? context)
	{
		if (context is null)
			return;

		var entries = context.ChangeTracker
							 .Entries()
							 .ToList();
		DateTimeOffset? utcNow = null;
		string? actor = null;
		var resolved = false;

		foreach (var entry in entries)
		{
			if (entry.Entity is not IAuditable || entry.Metadata.IsOwned())
				continue;

			switch (entry.State)
			{
				case EntityState.Deleted:
					continue;
				case EntityState.Added:
					EnsureResolved(ref utcNow, ref actor, ref resolved);
					ApplyCreationStamps(entry, utcNow!.Value, actor);
					continue;
				case EntityState.Detached:
				case EntityState.Unchanged:
				case EntityState.Modified:
				default:
					break;
			}

			if (!HasQualifyingScalarChange(entry) && !HasEligibleOwnedChange(entry, entries))
				continue;

			EnsureResolved(ref utcNow, ref actor, ref resolved);
			ApplyModificationStamps(entry, utcNow!.Value, actor);
		}
	}

	private void EnsureResolved(ref DateTimeOffset? utcNow, ref string? actor, ref bool resolved)
	{
		if (resolved)
			return;

		utcNow = _timeProvider.GetUtcNow()
							  .ToUniversalTime();
		actor = PersistenceActor.Resolve(_actorProvider, _diagnostics);
		resolved = true;
	}

	private static void ApplyCreationStamps(EntityEntry entry, DateTimeOffset utcNow, string? actor)
	{
		entry.Property(nameof(IAuditable.CreatedAtUtc))
			 .CurrentValue = utcNow;
		entry.Property(nameof(IAuditable.CreatedBy))
			 .CurrentValue = actor;
	}

	private static void ApplyModificationStamps(EntityEntry entry, DateTimeOffset utcNow, string? actor)
	{
		entry.Property(nameof(IAuditable.ModifiedAtUtc))
			 .CurrentValue = utcNow;
		entry.Property(nameof(IAuditable.ModifiedBy))
			 .CurrentValue = actor;
	}

	private static bool HasEligibleOwnedChange(EntityEntry owner, List<EntityEntry> entries)
	{
		foreach (var candidate in entries.Where(candidate => IsOwnedByPrincipal(candidate, owner)))
			switch (candidate.State)
			{
				case EntityState.Added or EntityState.Deleted:
				case EntityState.Modified when HasQualifyingScalarChange(candidate):
					return true;
			}

		return false;
	}

	private static bool IsOwnedByPrincipal(EntityEntry owned, EntityEntry owner)
	{
		var ownership = owned.Metadata.FindOwnership();
		if (ownership is null || !ownership.PrincipalEntityType.ClrType.IsInstanceOfType(owner.Entity))
			return false;

		var principalNavigation = ownership.DependentToPrincipal;
		if (principalNavigation is not null &&
			ReferenceEquals(owned.Reference(principalNavigation.Name)
								 .CurrentValue,
							owner.Entity))
			return true;

		if (ownership.Properties.Count == 0)
			return false;

		for (var i = 0; i < ownership.Properties.Count; i++)
		{
			var principalValue = owner.Property(ownership.PrincipalKey.Properties[i].Name)
									  .CurrentValue;
			var foreignKey = owned.Property(ownership.Properties[i].Name);
			var dependentValue = owned.State == EntityState.Deleted
									 ? foreignKey.OriginalValue
									 : foreignKey.CurrentValue ?? foreignKey.OriginalValue;
			if (!Equals(principalValue, dependentValue))
				return false;
		}

		return true;
	}

	private static bool HasQualifyingScalarChange(EntityEntry entry) =>
		entry.Properties
			 .Where(property => property.IsModified &&
								!StampPropertyNames.Contains(property.Metadata.Name) &&
								!property.Metadata.IsConcurrencyToken &&
								property.Metadata.ValueGenerated != ValueGenerated.OnAddOrUpdate)
			 .Any(property => !property.Metadata
									   .GetValueComparer()
									   .Equals(property.OriginalValue, property.CurrentValue));
}