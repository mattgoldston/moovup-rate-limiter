// Author: Mathew Goldston
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostAllow_HappyPath_Returns200WithCorrectShape()
    {
        var response = await _client.PostAsJsonAsync("/allow", new { user_id = "api-happy", timestamp = 1710000000.0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("allowed").GetBoolean());
        Assert.Equal("api-happy", json.GetProperty("user_id").GetString());
        var bucket = json.GetProperty("bucket");
        Assert.Equal(1.0, bucket.GetProperty("level").GetDouble());
        Assert.Equal(5, bucket.GetProperty("capacity").GetInt32());
        Assert.Equal(1.0, bucket.GetProperty("leak_rate").GetDouble());
        Assert.Equal(1710000000.0, bucket.GetProperty("last_updated_at").GetDouble());
    }

    [Fact]
    public async Task PostAllow_FullBucket_Returns429WithCorrectBody()
    {
        var userId = "api-full";
        var timestamp = 1710000000.0;

        for (var i = 0; i < 5; i++)
            await _client.PostAsJsonAsync("/allow", new { user_id = userId, timestamp });

        var response = await _client.PostAsJsonAsync("/allow", new { user_id = userId, timestamp });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("allowed").GetBoolean());
        Assert.Equal(userId, json.GetProperty("user_id").GetString());
        Assert.Equal("bucket_capacity_exceeded", json.GetProperty("reason").GetString());
        Assert.True(json.TryGetProperty("bucket", out _));
    }

    [Fact]
    public async Task PostAllow_MissingUserId_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/allow", new { user_id = "", timestamp = 1710000000.0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAllow_NullTimestamp_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/allow", new { user_id = "api-null-ts", timestamp = (double?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAllow_MissingTimestamp_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/allow", new { user_id = "api-missing-ts" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBucket_KnownUser_Returns200WithWrapper()
    {
        var userId = "api-get-known";
        await _client.PostAsJsonAsync("/allow", new { user_id = userId, timestamp = 1710000000.0 });

        var response = await _client.GetAsync($"/users/{userId}/bucket");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId, json.GetProperty("user_id").GetString());
        Assert.True(json.TryGetProperty("bucket", out _));
    }

    [Fact]
    public async Task GetBucket_UnknownUser_Returns404()
    {
        var response = await _client.GetAsync("/users/no-such-user-xyz/bucket");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBucket_BlankUserId_Returns400()
    {
        var response = await _client.GetAsync("/users/%20/bucket");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAllow_FloatTimestamp_IsHandledCorrectly()
    {
        var response = await _client.PostAsJsonAsync("/allow", new { user_id = "api-float-ts", timestamp = 1710000000.5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1710000000.5, json.GetProperty("bucket").GetProperty("last_updated_at").GetDouble());
    }

    [Fact]
    public async Task GetBucket_KnownUser_Returns200WithCorrectBucketShape()
    {
        var userId = "api-get-shape";
        await _client.PostAsJsonAsync("/allow", new { user_id = userId, timestamp = 1710000000.0 });

        var response = await _client.GetAsync($"/users/{userId}/bucket");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var bucket = json.GetProperty("bucket");

        Assert.Equal(1.0, bucket.GetProperty("level").GetDouble());
        Assert.Equal(5, bucket.GetProperty("capacity").GetInt32());
        Assert.Equal(1.0, bucket.GetProperty("leak_rate").GetDouble());
        Assert.Equal(1710000000.0, bucket.GetProperty("last_updated_at").GetDouble());
    }

    [Fact]
    public async Task PostAllow_NegativeTimestamp_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/allow", new { user_id = "api-negative-ts", timestamp = -1.0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAllow_TimestampOutOfRange_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/allow", new { user_id = "api-huge-ts", timestamp = 1e20 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAllow_BackwardTimestamp_IsAllowed()
    {
        var userId = "api-backward-ts";
        await _client.PostAsJsonAsync("/allow", new { user_id = userId, timestamp = 1710000000.0 });

        var response = await _client.PostAsJsonAsync("/allow", new { user_id = userId, timestamp = 1709999990.0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
