// Author: Mathew Goldston
using MoovupRateLimiter;
using Scalar.AspNetCore;

// ------------------------------------ Application Setup ----------------------------------------------
var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration.GetSection("RateLimiter");
var limiter = RateLimitHelper.CreateLimiter(
    config.GetValue<int>("Capacity", 5),
    config.GetValue<double>("LeakRate", 1.0)
);

var windowConfig = builder.Configuration.GetSection("SlidingWindow");
var windowLimiter = SlidingWindowHelper.CreateLimiter(
    windowConfig.GetValue<int>("Limit", 5),
    windowConfig.GetValue<double>("WindowSeconds", 5.0)
);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.Converters.Add(new UnixSecondsConverter());
});

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();

// -------------------------------------- API Endpoints ------------------------------------------------
app.MapPost("/allow", (AllowRequest request) =>
{
    var userId = request.UserId;

    var error = ValidateAllowRequest(request);
    if (error is not null)
        return error;

    var timestamp = DateTimeOffset.UnixEpoch.AddSeconds(request.Timestamp!.Value);

    var (result, newState) = RateLimitHelper.AllowRequest(limiter, userId, timestamp);

    limiter = newState;

    var bucket = RateLimitHelper.GetBucketState(limiter, userId);

    return result.Allowed
        ? Results.Ok(new AllowResponse(true, userId, bucket))
        : Results.Json(new RejectedResponse(false, userId, result.Reason, bucket), statusCode: 429);
})
.WithName("Allow");

app.MapGet("/users/{userId}/bucket", (string userId) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(new { error = "user_id is required" });

    var bucket = RateLimitHelper.GetBucketState(limiter, userId);
    return bucket is null ? Results.NotFound() : Results.Ok(new UserBucketResponse(userId, bucket));
})
.WithName("GetUserBucket");

app.MapPost("/sliding-window/allow", (AllowRequest request) =>
{
    var userId = request.UserId;

    var error = ValidateAllowRequest(request);
    if (error is not null)
        return error;

    var timestamp = DateTimeOffset.UnixEpoch.AddSeconds(request.Timestamp!.Value);

    var (result, newState) = SlidingWindowHelper.AllowRequest(windowLimiter, userId, timestamp);

    windowLimiter = newState;

    var window = SlidingWindowHelper.GetWindowState(windowLimiter, userId);

    return result.Allowed
        ? Results.Ok(new WindowAllowResponse(true, userId, window))
        : Results.Json(new WindowRejectedResponse(false, userId, result.Reason, window), statusCode: 429);
})
.WithName("SlidingWindowAllow");

app.MapGet("/users/{userId}/sliding-window", (string userId) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest(new { error = "user_id is required" });

    var window = SlidingWindowHelper.GetWindowState(windowLimiter, userId);
    return window is null ? Results.NotFound() : Results.Ok(new UserWindowResponse(userId, window));
})
.WithName("GetUserSlidingWindow");

app.Run();

// ------------------------------------------ Validation -----------------------------------------------
static IResult? ValidateAllowRequest(AllowRequest request)
{
    if (string.IsNullOrWhiteSpace(request.UserId))
        return Results.BadRequest(new { error = "user_id is required" });

    if (request.Timestamp is null)
        return Results.BadRequest(new { error = "timestamp is required" });

    if (request.Timestamp < 0)
        return Results.BadRequest(new { error = "timestamp must be a positive Unix epoch value" });

    var maxTimestamp = (DateTimeOffset.MaxValue - DateTimeOffset.UnixEpoch).TotalSeconds;

    if (double.IsNaN(request.Timestamp.Value) || request.Timestamp > maxTimestamp)
        return Results.BadRequest(new { error = "timestamp is out of range" });

    return null;
}
