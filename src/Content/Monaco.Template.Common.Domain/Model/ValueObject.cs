namespace Monaco.Template.Common.Domain.Model;

public abstract class ValueObject
{
	protected abstract IEnumerable<object?> GetEqualityComponents();

	public override bool Equals(object? obj)
	{
		if (obj == null || obj.GetType() != GetType())
			return false;

		var other = (ValueObject)obj;
		return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
	}

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