// Author: Mathew Goldston
namespace MoovupRateLimiter;

public record UserWindowResponse(string UserId, SlidingWindow Window);
