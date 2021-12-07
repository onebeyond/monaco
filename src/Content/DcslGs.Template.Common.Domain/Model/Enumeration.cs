using System.Reflection;

namespace DcslGs.Template.Common.Domain.Model;

public abstract class Enumeration : Entity, IComparable
{
    public string Name { get; protected set; }

    protected Enumeration(Guid id, string name) : base(id)
    {
        Name = name;
    }

    public override string ToString() => Name;

    public static IEnumerable<T> GetAll<T>() where T : Enumeration
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        return fields.Select(f => f.GetValue(null)).Cast<T>();
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static T From<T>(Guid value) where T : Enumeration
    {
        var matchingItem = Parse<Guid, T>(value,
                                          "value",
                                          item => item.Id == value);
        return matchingItem;
    }

    public static T From<T>(string displayName) where T : Enumeration
    {
        var matchingItem = Parse<string, T>(displayName,
                                            "display name",
                                            item => string.Equals(item.Name,
                                                                  displayName,
                                                                  StringComparison.CurrentCultureIgnoreCase));
        return matchingItem;
    }

    private static TResult Parse<T, TResult>(T value, string description, Func<TResult, bool> predicate) where TResult : Enumeration
    {
        var matchingItem = GetAll<TResult>().FirstOrDefault(predicate);
        return matchingItem ?? throw new InvalidOperationException($"'{value}' is not a valid {description} in {typeof(TResult)}");
    }

    public int CompareTo(object other) => Id.CompareTo(((Enumeration)other).Id);
}