using ArchUnitNET.Fluent.Syntax.Elements.Types.Classes;

namespace Monaco.Template.Backend.ArchitectureTests.Extensions;

public static class ArchUnitExtensions
{
	public static bool IsNestedWithin(this IType type, params IType[] types) =>
		types.Any(t => type.FullName.StartsWith($"{t.FullName}+"));

	public static IType? NestType(this IType type, Architecture architecture) =>
		architecture.Types
					.SingleOrDefault(t => type.IsNestedWithin(t));

	public static ClassesShouldConjunction HavePropertySetterWithVisibility(this ClassesShould should,
																			params Visibility[] visibility) =>
		should.FollowCustomCondition(c => c.GetPropertyMembers()
										   .Any(p => visibility.Contains(p.SetterVisibility)),
									 $"have properties setters with visibility {string.Join(", ", visibility)}",
									 $"does not have a property setter with visibility {string.Join(", ", visibility)}");

	public static ClassesShouldConjunction NotHavePropertySetterWithVisibility(this ClassesShould should,
																			   params Visibility[] visibility) =>
		should.FollowCustomCondition(c => c.GetPropertyMembers()
										   .All(p => !visibility.Contains(p.SetterVisibility)),
									 $"not have properties setters with visibility {string.Join(", ", visibility)}",
									 $"has property setter with visibility {string.Join(", ", visibility)}");
}