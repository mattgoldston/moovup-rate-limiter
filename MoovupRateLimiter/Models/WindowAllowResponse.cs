// Author: Mathew Goldston
namespace MoovupRateLimiter;

public record WindowAllowResponse(bool Allowed, string UserId, SlidingWindow? Window);
