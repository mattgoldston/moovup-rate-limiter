// Author: Mathew Goldston
namespace MoovupRateLimiter;

public record UserBucketResponse(string UserId, Bucket Bucket);
