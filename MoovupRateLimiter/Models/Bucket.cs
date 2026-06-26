// Author: Mathew Goldston
namespace MoovupRateLimiter;

public record Bucket(double Level, int Capacity, double LeakRate, DateTimeOffset LastUpdatedAt);
