namespace Monaco.Template.Backend.Common.Domain.Model;

/// <summary>
/// Represents a base class for value objects, providing equality comparison based on the values of the object's
/// components rather than its reference or identity.
/// </summary>
/// <remarks>A value object is an immutable type that is defined by its properties rather than its identity. This
/// class provides a framework for implementing value objects by overriding equality and hash code generation based on
/// the components returned by the <see cref="GetEqualityComponents"/> method.  To implement a value object, derive from
/// this class and override the <see cref="GetEqualityComponents"/> method to return the sequence of components that
/// uniquely define the value object. These components will be used for equality comparison and hash code
/// generation.</remarks>
public abstract class ValueObject : IEquatable<ValueObject>
{
	protected abstract IEnumerable<object?> GetEqualityComponents();

	public bool Equals(ValueObject? other)
	{
		if (other is null || other.GetType() != GetType())
			return false;

		return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
	}

	public override bool Equals(object? obj) =>
		Equals(obj as ValueObject);

	public static bool operator ==(ValueObject? left, ValueObject? right)
	{
		if (left is null && right is null)
			return true;

		if (left is null || right is null)
			return false;

		return left.Equals(right);
	}

	public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);

	public override int GetHashCode() =>
		GetEqualityComponents()
			.Select(x => x is not null ? x.GetHashCode() : 0)
			.Aggregate((x, y) => x ^ y);
}