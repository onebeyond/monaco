using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Monaco.Template.Backend.Common.Application.Extensions;

public static class ClaimsPrincipalExtensions
{
	private const string ResourceAccessClaimName = "resource_access";
	private record Client(string[] Roles);

	/// <summary>
	/// Retrieves the User Id from the "sub" claim
	/// </summary>
	/// <param name="principal"></param>
	/// <returns></returns>
	public static Guid? GetUserId(this ClaimsPrincipal principal) =>
		principal.HasClaim(c => c.Type == JwtRegisteredClaimNames.Sub)
			? Guid.Parse(principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value)
			: null;

	/// <summary>
	/// Determines if the user has the specified role on the specified client
	/// </summary>
	/// <param name="principal"></param>
	/// <param name="clientName"></param>
	/// <param name="roleName"></param>
	/// <returns></returns>
	public static bool IsInClientRole(this ClaimsPrincipal principal, string clientName, string roleName)
	{
		var resourceAccessClaim = principal.FindFirst(ResourceAccessClaimName);
		if (resourceAccessClaim is null)
			return false;

		var clients = JsonSerializer.Deserialize<Dictionary<string, Client>>(resourceAccessClaim.Value,
																			 new JsonSerializerOptions(JsonSerializerDefaults.Web));
		return clients is not null &&
			   clients.ContainsKey(clientName) &&
			   clients[clientName].Roles.Contains(roleName);
	}

	/// <summary>
	/// Determines if the user has the specified role in the client specified by the Audience (aud) claim
	/// </summary>
	/// <param name="principal"></param>
	/// <param name="roleName"></param>
	/// <returns></returns>
	public static bool IsInClientRole(this ClaimsPrincipal principal, string roleName) =>
		principal.HasClaim(c => c.Type == JwtRegisteredClaimNames.Aud) &&
		principal.IsInClientRole(principal.FindFirst(JwtRegisteredClaimNames.Aud)!.Value, roleName);
}