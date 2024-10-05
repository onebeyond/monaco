using System.Text.Json.Serialization;

namespace Monaco.Template.Backend.IntegrationTests;

public record AccessTokenDto([property: JsonPropertyName("access_token")] string AccessToken);