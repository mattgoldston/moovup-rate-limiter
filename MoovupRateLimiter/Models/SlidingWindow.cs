// Author: Mathew Goldston
using System.Collections.Immutable;

namespace MoovupRateLimiter;

public record SlidingWindow(ImmutableList<DateTimeOffset> Timestamps, int Limit, double WindowSeconds, DateTimeOffset LastUpdatedAt)
{
    public int Count => Timestamps.Count;
}
