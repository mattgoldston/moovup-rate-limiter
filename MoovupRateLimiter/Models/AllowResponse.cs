// Author: Mathew Goldston
namespace MoovupRateLimiter;

public record AllowResponse(bool Allowed, string UserId, Bucket? Bucket);
