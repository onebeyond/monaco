using System.Text.Json.Serialization;

namespace Monaco.Template.Backend.IntegrationTests.Auth;

public record AccessTokenDto([property: JsonPropertyName("access_token")] string AccessToken);