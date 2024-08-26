using ArchUnitNET.Domain;

namespace Monaco.Template.Backend.ArchitectureTests.Extensions;

public static class ArchUnitExtensions
{
	public static bool IsNestedWithin(this IType type, params IType[] types) =>
		types.Any(t => type.FullName.StartsWith($"{t.FullName}+"));

	public static IType? NestType(this IType type, Architecture architecture) =>
		architecture.Types
					.SingleOrDefault(t => type.IsNestedWithin(t));
}