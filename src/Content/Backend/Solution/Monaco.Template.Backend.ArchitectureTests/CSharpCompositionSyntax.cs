using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Monaco.Template.Backend.ArchitectureTests;

internal static class CSharpCompositionSyntax
{
	public static bool Invokes(string source, string methodName) =>
		CountInvocations(source, methodName) > 0;

	public static int CountInvocations(string source, string methodName) =>
		Invocations(source).Count(invocation => MethodName(invocation) == methodName);

	public static int CountGenericInvocations(string source, string methodName, params string[] typeArgumentNames) =>
		Invocations(source).Count(invocation => MatchesGeneric(invocation, methodName, typeArgumentNames));

	public static bool InvokesWithStringArgument(string source, string methodName, string argument) =>
		Invocations(source).Any(invocation => MethodName(invocation) == methodName && HasStringArgument(invocation, argument));

	public static int CountAuditableInterceptorRegistrations(string source) =>
		Invocations(source).Count(invocation => MethodName(invocation) == "AddInterceptors" &&
												invocation.ArgumentList.Arguments is [ { Expression: InvocationExpressionSyntax required } ] &&
												MatchesGeneric(required, "GetRequiredService", "AuditableSaveChangesInterceptor"));

	public static bool ContainsIdentifier(string source, string identifier) =>
		Root(source)
			.DescendantNodes()
			.Any(node => node switch
						 {
							 IdentifierNameSyntax name => name.Identifier.ValueText == identifier,
							 GenericNameSyntax generic => generic.Identifier.ValueText == identifier,
							 _ => false
						 });

	public static bool ContainsIdentifierFragment(string source, string fragment) =>
		Root(source)
			.DescendantNodes()
			.Any(node => node switch
						 {
							 IdentifierNameSyntax name => name.Identifier.ValueText.Contains(fragment, StringComparison.Ordinal),
							 GenericNameSyntax generic => generic.Identifier.ValueText.Contains(fragment, StringComparison.Ordinal),
							 _ => false
						 });

	private static bool MatchesGeneric(InvocationExpressionSyntax invocation, string methodName, params string[] typeArgumentNames)
	{
		if (GenericName(invocation) is not { } generic ||
			generic.Identifier.ValueText != methodName)
			return false;

		if (generic.TypeArgumentList.Arguments.Count != typeArgumentNames.Length)
			return false;

		return !typeArgumentNames.Where((t, index) => TypeName(generic.TypeArgumentList.Arguments[index]) != t)
								 .Any();
	}

	private static GenericNameSyntax? GenericName(InvocationExpressionSyntax invocation) =>
		invocation.Expression switch
		{
			GenericNameSyntax generic => generic,
			MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic,
			_ => null
		};

	private static string? MethodName(InvocationExpressionSyntax invocation) =>
		invocation.Expression switch
		{
			IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
			GenericNameSyntax generic => generic.Identifier.ValueText,
			MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
			MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.Identifier.ValueText,
			_ => null
		};

	private static string? TypeName(TypeSyntax type) =>
		type switch
		{
			IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
			GenericNameSyntax generic => generic.Identifier.ValueText,
			QualifiedNameSyntax qualified => TypeName(qualified.Right),
			_ => null
		};

	private static bool HasStringArgument(InvocationExpressionSyntax invocation, string argument) =>
		invocation.ArgumentList.Arguments.Any(argumentSyntax => argumentSyntax.Expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal &&
																literal.Token.ValueText == argument);

	private static IEnumerable<InvocationExpressionSyntax> Invocations(string source) =>
		Root(source)
			.DescendantNodes()
			.OfType<InvocationExpressionSyntax>();

	private static CompilationUnitSyntax Root(string source) =>
		CSharpSyntaxTree.ParseText(source)
						.GetCompilationUnitRoot();
}