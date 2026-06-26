// Author: Mathew Goldston
using MoovupRateLimiter;

public class RateLimitHelperTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstRequest_FromNewUser_IsAllowed()
    {
        var limiter = RateLimitHelper.CreateLimiter(3, 1.0);

        var (result, _) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);

        Assert.True(result.Allowed);
    }

    [Fact]
    public void Requests_FillTheBucket()
    {
        var limiter = RateLimitHelper.CreateLimiter(5, 0.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        var (_, l2) = RateLimitHelper.AllowRequest(l1, "user1", BaseTime);
        var (_, l3) = RateLimitHelper.AllowRequest(l2, "user1", BaseTime);

        var bucket = RateLimitHelper.GetBucketState(l3, "user1");
        Assert.Equal(3.0, bucket!.Level);
    }

    [Fact]
    public void Request_IsRejected_WhenCapacityWouldBeExceeded()
    {
        var limiter = RateLimitHelper.CreateLimiter(3, 0.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        var (_, l2) = RateLimitHelper.AllowRequest(l1, "user1", BaseTime);
        var (_, l3) = RateLimitHelper.AllowRequest(l2, "user1", BaseTime);
        var (result, _) = RateLimitHelper.AllowRequest(l3, "user1", BaseTime);

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Bucket_Leaks_BasedOnElapsedTime()
    {
        var limiter = RateLimitHelper.CreateLimiter(5, 1.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        var (_, l2) = RateLimitHelper.AllowRequest(l1, "user1", BaseTime);
        var (_, l3) = RateLimitHelper.AllowRequest(l2, "user1", BaseTime);
        
        var (_, l4) = RateLimitHelper.AllowRequest(l3, "user1", BaseTime.AddSeconds(2));

        var bucket = RateLimitHelper.GetBucketState(l4, "user1");
        Assert.Equal(2.0, bucket!.Level);
    }

    [Fact]
    public void LargeTimeGap_DrainsTheBucket()
    {
        var limiter = RateLimitHelper.CreateLimiter(3, 1.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        var (_, l2) = RateLimitHelper.AllowRequest(l1, "user1", BaseTime);
        var (_, l3) = RateLimitHelper.AllowRequest(l2, "user1", BaseTime);
        // bucket full at level 3; next request at same time is denied
        var (denied, l4) = RateLimitHelper.AllowRequest(l3, "user1", BaseTime);
        Assert.False(denied.Allowed);

        // after 100 seconds the bucket is fully drained
        var (allowed, _) = RateLimitHelper.AllowRequest(l4, "user1", BaseTime.AddSeconds(100));
        Assert.True(allowed.Allowed);
    }

    [Fact]
    public void MultipleUsers_HaveIndependentBuckets()
    {
        var limiter = RateLimitHelper.CreateLimiter(1, 0.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        // user1 bucket is now full
        var (result, _) = RateLimitHelper.AllowRequest(l1, "user2", BaseTime);

        Assert.True(result.Allowed);
    }

    [Fact]
    public void BackwardTimestamp_DoesNotInflateBucketLevel()
    {
        var limiter = RateLimitHelper.CreateLimiter(5, 1.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        // elapsed clamps to 0 — level increases by 1, not more
        var (_, l2) = RateLimitHelper.AllowRequest(l1, "user1", BaseTime.AddSeconds(-10));

        var bucket = RateLimitHelper.GetBucketState(l2, "user1");
        Assert.Equal(2.0, bucket!.Level);
    }

    [Fact]
    public void GetBucketState_ReturnsNull_ForUnknownUser()
    {
        var limiter = RateLimitHelper.CreateLimiter(3, 1.0);

        var bucket = RateLimitHelper.GetBucketState(limiter, "unknown");

        Assert.Null(bucket);
    }

    [Fact]
    public void AllowedRequest_ReturnsOkReason()
    {
        var limiter = RateLimitHelper.CreateLimiter(5, 0.0);

        var (result, _) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);

        Assert.True(result.Allowed);
        Assert.Equal("ok", result.Reason);
    }

    [Fact]
    public void BucketFull_ReturnsBucketCapacityExceededReason()
    {
        var limiter = RateLimitHelper.CreateLimiter(2, 0.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        var (_, l2) = RateLimitHelper.AllowRequest(l1, "user1", BaseTime);
        var (result, _) = RateLimitHelper.AllowRequest(l2, "user1", BaseTime);

        Assert.False(result.Allowed);
        Assert.Equal("bucket_capacity_exceeded", result.Reason);
    }

    [Fact]
    public void BackwardTimestamp_IsProcessedWithZeroElapsedTime()
    {
        var limiter = RateLimitHelper.CreateLimiter(5, 1.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        // backward timestamp — elapsed clamps to 0, no leak, request still processed
        var (result, l2) = RateLimitHelper.AllowRequest(l1, "user1", BaseTime.AddSeconds(-10));

        Assert.True(result.Allowed);
        var bucket = RateLimitHelper.GetBucketState(l2, "user1");
        Assert.Equal(2.0, bucket!.Level);
    }

    [Fact]
    public void BucketLevel_NeverGoesBelowZero()
    {
        var limiter = RateLimitHelper.CreateLimiter(5, 1.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        // advance 100 seconds — without the floor the level would go negative
        var (_, l2) = RateLimitHelper.AllowRequest(l1, "user1", BaseTime.AddSeconds(100));

        var bucket = RateLimitHelper.GetBucketState(l2, "user1");
        Assert.Equal(1.0, bucket!.Level);
    }

    [Fact]
    public void RejectedRequest_StillUpdatesLastUpdatedAt()
    {
        var limiter = RateLimitHelper.CreateLimiter(1, 0.0);

        var (_, l1) = RateLimitHelper.AllowRequest(limiter, "user1", BaseTime);
        var laterTime = BaseTime.AddSeconds(5);
        var (result, l2) = RateLimitHelper.AllowRequest(l1, "user1", laterTime);

        Assert.False(result.Allowed);
        var bucket = RateLimitHelper.GetBucketState(l2, "user1");
        Assert.Equal(laterTime, bucket!.LastUpdatedAt);
    }
}
