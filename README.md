# Rate Limiter API

Per-user rate limiters built with C# and ASP.NET Core minimal APIs. Two independent algorithms are exposed, each with its own endpoints and state:

| Algorithm      | Allow endpoint               | State endpoint                       |
|----------------|------------------------------|--------------------------------------|
| Leaky bucket   | `POST /allow`                | `GET /users/{userId}/bucket`         |
| Sliding window | `POST /sliding-window/allow` | `GET /users/{userId}/sliding-window` |

Both allow endpoints take the same body (`{ "user_id": "...", "timestamp": 1710000000.0 }`), return `200` when allowed and `429` when rejected.

## Prerequisites

| Option | Requirement                                                      |
|--------|------------------------------------------------------------------|
| .NET   | [.NET 10 SDK](https://dotnet.microsoft.com/download)             |
| Docker | [Docker Desktop](https://www.docker.com/products/docker-desktop) |

Only one is needed depending on how you want to run it.

## How to run the app

**Option 1: .NET 10 SDK**

```bash
dotnet run --project MoovupRateLimiter
```

**Option 2: Docker**

```bash
docker build -t rate-limiter .
docker run -p 5064:8080 rate-limiter
```

API runs at `http://localhost:5064`. An interactive API explorer (Scalar) is available at `/scalar/v1`.

Limiter settings can be configured in `MoovupRateLimiter/appsettings.json`:

- Leaky bucket (`RateLimiter` section): defaults are `capacity = 5` and `leak_rate = 1.0`.
- Sliding window (`SlidingWindow` section): defaults are `limit = 5` requests per `window_seconds = 5.0`.

## How to run tests

```bash
dotnet test
```

## Project structure

The implementation is organised around three core files:

| File                                               | Responsibility                                                                      |
|----------------------------------------------------|-------------------------------------------------------------------------------------|
| `MoovupRateLimiter/Helpers/RateLimitHelper.cs`     | The leaky bucket algorithm: leak calculation, capacity check, and state update.     |
| `MoovupRateLimiter/Helpers/SlidingWindowHelper.cs` | The sliding window algorithm: expiring old requests, limit check, and state update. |
| `MoovupRateLimiter/Program.cs`                     | The API layer: endpoint routing, request validation, and response shaping.          |

The remaining files (`Models/` and the JSON converter) provide the supporting types used by these two.

## Design decisions

The core logic in `RateLimitHelper` is purely functional, so every call returns a new limiter state rather than mutating the existing one. This keeps the core easy to test without any mocking and separates it clearly from the API layer.

`RateLimiter` and `Bucket` are C# records so immutability is enforced by the type system. Internally timestamps use `DateTimeOffset` to keep the time arithmetic readable. A `JsonConverter` handles converting to Unix floats at the API boundary.

Backward timestamps (where the new timestamp is earlier than the last recorded one) are handled by clamping elapsed time to zero rather than rejecting the request. No leak occurs but the request is still processed normally. This is more tolerant of clock skew than returning an error.

The sliding window is a sliding window log: each user keeps the timestamps of their allowed requests, and a request is allowed if fewer than `limit` requests fall in the window `(now - window_seconds, now]`. Rejected requests are not recorded, so the log never holds more than `limit` entries per user. Backward timestamps are clamped to the last recorded time, matching the leaky bucket's tolerance of clock skew while keeping the log ordered.

Negative timestamps are rejected with `400 Bad Request` at the API layer as they represent dates before the Unix epoch and are not meaningful in this context.

## Assumptions and trade-offs

The spec states single-threaded execution is assumed, so no locking is implemented. The shared `limiter` and `windowLimiter` variables in `Program.cs` are not thread-safe. Concurrent requests would cause a race condition where one request overwrites another's updated state. In a production system this would need a lock.

Timestamps come from the client, not the server. This is per spec but means a client could send a future timestamp to manipulate their leak rate.


Buckets and windows are never evicted so memory grows with unique user count. Restarting the server clears all state.
