// Author: Mathew Goldston
namespace MoovupRateLimiter;

public record AllowResult(bool Allowed, string Reason);
