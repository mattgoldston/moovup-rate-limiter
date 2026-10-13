// Author: Mathew Goldston
using MoovupRateLimiter;

public class SlidingWindowHelperTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstRequest_FromNewUser_IsAllowed()
    {
        var limiter = SlidingWindowHelper.CreateLimiter(3, 10.0);

        var (result, _) = SlidingWindowHelper.AllowRequest(limiter, "user1", BaseTime);

        Assert.True(result.Allowed);
        Assert.Equal("ok", result.Reason);
    }

    [Fact]
    public void RequestsBeyondLimit_WithinWindow_AreRejected()
    {
        var limiter = SlidingWindowHelper.CreateLimiter(2, 10.0);

        var (_, l1) = SlidingWindowHelper.AllowRequest(limiter, "user1", BaseTime);
        var (_, l2) = SlidingWindowHelper.AllowRequest(l1, "user1", BaseTime.AddSeconds(1));
        var (result, _) = SlidingWindowHelper.AllowRequest(l2, "user1", BaseTime.AddSeconds(2));

        Assert.False(result.Allowed);
        Assert.Equal("window_limit_exceeded", result.Reason);
    }

    [Fact]
    public void OldRequests_SlideOutOfWindow()
    {
        var limiter = SlidingWindowHelper.CreateLimiter(2, 10.0);

        var (_, l1) = SlidingWindowHelper.AllowRequest(limiter, "user1", BaseTime);
        var (_, l2) = SlidingWindowHelper.AllowRequest(l1, "user1", BaseTime.AddSeconds(5));
        // first request has expired, second is still in the window
        var (allowed, l3) = SlidingWindowHelper.AllowRequest(l2, "user1", BaseTime.AddSeconds(11));
        Assert.True(allowed.Allowed);

        // window now holds the requests at +5 and +11, so it is full again
        var (denied, _) = SlidingWindowHelper.AllowRequest(l3, "user1", BaseTime.AddSeconds(12));
        Assert.False(denied.Allowed);
    }

    [Fact]
    public void RequestExactlyWindowSecondsOld_IsExpired()
    {
        var limiter = SlidingWindowHelper.CreateLimiter(1, 10.0);

        var (_, l1) = SlidingWindowHelper.AllowRequest(limiter, "user1", BaseTime);
        var (result, _) = SlidingWindowHelper.AllowRequest(l1, "user1", BaseTime.AddSeconds(10));

        Assert.True(result.Allowed);
    }

    [Fact]
    public void RejectedRequests_AreNotRecorded()
    {
        var limiter = SlidingWindowHelper.CreateLimiter(1, 10.0);

        var (_, l1) = SlidingWindowHelper.AllowRequest(limiter, "user1", BaseTime);
        var (_, l2) = SlidingWindowHelper.AllowRequest(l1, "user1", BaseTime.AddSeconds(9));

        var window = SlidingWindowHelper.GetWindowState(l2, "user1");
        Assert.Equal(1, window!.Count);
        Assert.Equal(BaseTime.AddSeconds(9), window.LastUpdatedAt);

        // only the allowed request at +0 counts, so +10 is allowed even though a rejection happened at +9
        var (result, _) = SlidingWindowHelper.AllowRequest(l2, "user1", BaseTime.AddSeconds(10));
        Assert.True(result.Allowed);
    }

    [Fact]
    public void MultipleUsers_HaveIndependentWindows()
    {
        var limiter = SlidingWindowHelper.CreateLimiter(1, 10.0);

        var (_, l1) = SlidingWindowHelper.AllowRequest(limiter, "user1", BaseTime);
        var (result, _) = SlidingWindowHelper.AllowRequest(l1, "user2", BaseTime);

        Assert.True(result.Allowed);
    }

    [Fact]
    public void BackwardTimestamp_IsClampedToLastUpdatedAt()
    {
        var limiter = SlidingWindowHelper.CreateLimiter(5, 10.0);

        var (_, l1) = SlidingWindowHelper.AllowRequest(limiter, "user1", BaseTime);
        var (result, l2) = SlidingWindowHelper.AllowRequest(l1, "user1", BaseTime.AddSeconds(-10));

        Assert.True(result.Allowed);
        var window = SlidingWindowHelper.GetWindowState(l2, "user1");
        Assert.Equal(2, window!.Count);
        Assert.Equal(BaseTime, window.LastUpdatedAt);
        Assert.All(window.Timestamps, t => Assert.Equal(BaseTime, t));
    }

    [Fact]
    public void GetWindowState_ReturnsNull_ForUnknownUser()
    {
        var limiter = SlidingWindowHelper.CreateLimiter(3, 10.0);

        Assert.Null(SlidingWindowHelper.GetWindowState(limiter, "unknown"));
    }

    [Fact]
    public void AllowRequest_DoesNotMutateOriginalLimiter()
    {
        var limiter = SlidingWindowHelper.CreateLimiter(3, 10.0);

        SlidingWindowHelper.AllowRequest(limiter, "user1", BaseTime);

        Assert.Empty(limiter.Windows);
    }
}
