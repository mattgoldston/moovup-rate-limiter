// Author: Mathew Goldston
namespace MoovupRateLimiter;

public record RejectedResponse(bool Allowed, string UserId, string Reason, Bucket? Bucket);
