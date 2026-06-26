// Author: Mathew Goldston
using System.Collections.Immutable;

namespace MoovupRateLimiter;

public static class RateLimitHelper
{
    public static RateLimiter CreateLimiter(int capacity, double leakRate)
    {
        return new RateLimiter(capacity, leakRate, ImmutableDictionary<string, Bucket>.Empty);
    }

    public static (AllowResult result, RateLimiter newState) AllowRequest(RateLimiter limiter, string userId, DateTimeOffset timestamp)
    {
        var bucket = limiter.Buckets.GetValueOrDefault(userId)
            ?? new Bucket(0, limiter.Capacity, limiter.LeakRate, timestamp);

        var elapsed = Math.Max(0, (timestamp - bucket.LastUpdatedAt).TotalSeconds);
        var leaked = bucket.LeakRate * elapsed;
        var leakedLevel = Math.Max(0, bucket.Level - leaked);
        var allowed = leakedLevel + 1 <= bucket.Capacity;

        var result = new AllowResult(allowed, allowed ? "ok" : "bucket_capacity_exceeded");
        var updatedBucket = new Bucket(allowed ? leakedLevel + 1 : leakedLevel, bucket.Capacity, bucket.LeakRate, timestamp);

        var updatedBuckets = limiter.Buckets.SetItem(userId, updatedBucket);
        return (result, new RateLimiter(limiter.Capacity, limiter.LeakRate, updatedBuckets));
    }

    public static Bucket? GetBucketState(RateLimiter limiter, string userId)
    {
        limiter.Buckets.TryGetValue(userId, out Bucket? bucket);
        return bucket;
    }
}
