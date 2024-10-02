using System.Text.Json.Serialization;

namespace Monaco.Template.Backend.IntegrationTests;

public record AccessTokenDto
{
	public AccessTokenDto(string accessToken, int expiresIn)
	{
		AccessToken = accessToken;
	}

	[JsonPropertyName("access_token")]
	public string AccessToken { get; init; }
}