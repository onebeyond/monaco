namespace Monaco.Template.Backend.IntegrationTests.Auth;

internal static class Auth
{
	public const string AudienceClientId = "monaco-template-api";

	public static string[] Scopes => ["email", "profile", "roles", .. Api.Auth.Scopes.List];

	public record Roles
	{
		public const string Administrator = nameof(Administrator);
		public const string Customer = nameof(Customer);

		public static string[] List = [Administrator, Customer];
	}
}