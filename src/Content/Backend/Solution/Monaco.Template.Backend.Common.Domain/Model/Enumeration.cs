using System.Reflection;

namespace Monaco.Template.Backend.Common.Domain.Model;

public abstract class Enumeration : Entity, IComparable
{
	protected Enumeration(Guid id, string name) : base(id)
	{
		Name = name;
	}

	public string Name { get; protected set; }

	public override string ToString() =>
		Name;

	public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
		typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
				 .Select(f => f.GetValue(null)).Cast<T>();

	public override int GetHashCode() =>
		Id.GetHashCode();

	public static T From<T>(Guid value) where T : Enumeration =>
		Parse<Guid, T>(value,
					   "value",
					   item => item.Id == value);

	public static T From<T>(string displayName) where T : Enumeration =>
		Parse<string, T>(displayName,
						 "display name",
						 item => string.Equals(item.Name,
											   displayName,
											   StringComparison.CurrentCultureIgnoreCase));

	private static TResult Parse<T, TResult>(T value,
											 string description,
											 Func<TResult, bool> predicate) where TResult : Enumeration =>
		GetAll<TResult>().FirstOrDefault(predicate) ??
		throw new InvalidOperationException($"'{value}' is not a valid {description} in {typeof(TResult)}");

	public int CompareTo(object? other) =>
		Id.CompareTo(((Enumeration)other!).Id);
}