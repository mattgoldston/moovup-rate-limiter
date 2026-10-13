// Author: Mathew Goldston
namespace MoovupRateLimiter;

public record WindowRejectedResponse(bool Allowed, string UserId, string Reason, SlidingWindow? Window);
