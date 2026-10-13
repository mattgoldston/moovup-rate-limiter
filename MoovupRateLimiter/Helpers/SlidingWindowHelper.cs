// Author: Mathew Goldston
using System.Collections.Immutable;

namespace MoovupRateLimiter;

public static class SlidingWindowHelper
{
    public static SlidingWindowLimiter CreateLimiter(int limit, double windowSeconds)
    {
        return new SlidingWindowLimiter(limit, windowSeconds, ImmutableDictionary<string, SlidingWindow>.Empty);
    }

    public static (AllowResult result, SlidingWindowLimiter newState) AllowRequest(SlidingWindowLimiter limiter, string userId, DateTimeOffset timestamp)
    {
        var window = limiter.Windows.GetValueOrDefault(userId)
            ?? new SlidingWindow(ImmutableList<DateTimeOffset>.Empty, limiter.Limit, limiter.WindowSeconds, timestamp);

        // Backward timestamps are clamped to the last recorded time so the log stays ordered
        var now = timestamp > window.LastUpdatedAt ? timestamp : window.LastUpdatedAt;
        var windowStart = now.AddSeconds(-window.WindowSeconds);
        var active = window.Timestamps.RemoveAll(t => t <= windowStart);
        var allowed = active.Count < window.Limit;

        var result = new AllowResult(allowed, allowed ? "ok" : "window_limit_exceeded");
        var updatedWindow = new SlidingWindow(allowed ? active.Add(now) : active, window.Limit, window.WindowSeconds, now);

        var updatedWindows = limiter.Windows.SetItem(userId, updatedWindow);
        return (result, new SlidingWindowLimiter(limiter.Limit, limiter.WindowSeconds, updatedWindows));
    }

    public static SlidingWindow? GetWindowState(SlidingWindowLimiter limiter, string userId)
    {
        limiter.Windows.TryGetValue(userId, out SlidingWindow? window);
        return window;
    }
}
