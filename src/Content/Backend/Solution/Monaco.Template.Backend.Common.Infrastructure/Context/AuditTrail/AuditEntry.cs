using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Monaco.Template.Backend.Common.Infrastructure.Context.AuditTrail;

public record AuditEntry
{
	private readonly EntityEntry _entityEntry;

	public AuditEntry(EntityEntry entityEntry)
	{
		_entityEntry = entityEntry;

		Name = _entityEntry.Metadata.Name;
		Action = _entityEntry.State.ToString();
		_propertiesValues = _entityEntry.Properties
										.Where(p => _entityEntry.State == EntityState.Modified && p.CurrentValue != p.OriginalValue ||
												    _entityEntry.State != EntityState.Modified)
										.ToDictionary(p => p.Metadata.Name,
													  p => new PropertyValues(_entityEntry.State switch
																		      {
																			      EntityState.Added => p.CurrentValue,
																			      EntityState.Deleted => null,
																			      EntityState.Modified => p.CurrentValue,
																			      _ => null
																		      },
																		      _entityEntry.State switch
																		      {
																			      EntityState.Added => null,
																			      EntityState.Deleted => p.OriginalValue,
																			      EntityState.Modified => p.OriginalValue,
																			      _ => null
																		      }));
	}

	public string Name { get; }
	public string Action { get; }
	public IReadOnlyDictionary<string, object?> Keys => _entityEntry.Metadata
																	.FindPrimaryKey()?
																	.Properties
																	.ToDictionary(p => p.Name,
																				  p => _entityEntry.Property(p.Name).CurrentValue) ??
														new Dictionary<string, object?>();

	private readonly Dictionary<string, PropertyValues> _propertiesValues;
	public IReadOnlyDictionary<string, PropertyValues> PropertiesValues => _propertiesValues;

	public record PropertyValues(object? CurrentValue, object? OriginalValue);
}