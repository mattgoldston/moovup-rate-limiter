// Author: Mathew Goldston
using System.Collections.Immutable;

namespace MoovupRateLimiter;

public record SlidingWindowLimiter(int Limit, double WindowSeconds, ImmutableDictionary<string, SlidingWindow> Windows);
