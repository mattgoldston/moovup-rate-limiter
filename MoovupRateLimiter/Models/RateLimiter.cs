// Author: Mathew Goldston
using System.Collections.Immutable;

namespace MoovupRateLimiter;

public record RateLimiter(int Capacity, double LeakRate, ImmutableDictionary<string, Bucket> Buckets);
