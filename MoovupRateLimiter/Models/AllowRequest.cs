// Author: Mathew Goldston
namespace MoovupRateLimiter;

public record AllowRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("user_id")] string UserId,
    [property: System.Text.Json.Serialization.JsonPropertyName("timestamp")] double? Timestamp
);
