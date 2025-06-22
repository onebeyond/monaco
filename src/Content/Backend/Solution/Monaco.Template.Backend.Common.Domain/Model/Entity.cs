namespace Monaco.Template.Backend.Common.Domain.Model;

/// <summary>
/// Represents the base class for all entities, providing a unique identifier and equality comparison functionality.
/// </summary>
/// <remarks>The <see cref="Entity"/> class serves as a base type for domain entities, ensuring that each entity
/// has a unique identifier and supports value-based equality comparison. Entities are compared based on their <see
/// cref="Id"/> property, which must be non-empty for equality to be valid. This class also provides operator overloads
/// for equality and inequality checks.</remarks>
public abstract class Entity : IEquatable<Entity>
{
	protected Entity()
	{
	}

	protected Entity(Guid id) : this()
	{
		Id = id;
	}

	/// <summary>
	/// The identifier of the entity
	/// </summary>
	public virtual Guid Id { get; }

	public override bool Equals(object? obj) =>
		Equals(obj as Entity);

	public bool Equals(Entity? other)
	{
		if (other is null)
			return false;

		if (ReferenceEquals(this, other))
			return true;

		if (GetRealType(GetType()) != GetRealType(other.GetType()))
			return false;

		return Id != Guid.Empty &&
			   other.Id != Guid.Empty &&
			   Id == other.Id;
	}

	public static bool operator ==(Entity? a, Entity? b) =>
		a is null
			? b is null
			: b is not null && a.Equals(b);

	public static bool operator !=(Entity? a, Entity? b) => !(a == b);

	public override int GetHashCode() =>
		HashCode.Combine(GetRealType(GetType()), Id);

	private static Type GetRealType(Type type) =>
		type.ToString().Contains("Castle.Proxies")
			? type.BaseType!
			: type;
}