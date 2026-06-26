# Leaky Bucket Rate Limiter API

Per-user leaky bucket rate limiter built with C# and ASP.NET Core minimal APIs.

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

Capacity and leak rate can be configured in `MoovupRateLimiter/appsettings.json`. Defaults are `capacity = 5` and `leak_rate = 1.0`.

## How to run tests

```bash
dotnet test
```

## Project structure

The implementation is organised around two core files:

| File                                           | Responsibility                                                                |
|------------------------------------------------|-------------------------------------------------------------------------------|
| `MoovupRateLimiter/Helpers/RateLimitHelper.cs` | The rate-limiting algorithm: leak calculation, capacity check, and state update. |
| `MoovupRateLimiter/Program.cs`                 | The API layer: endpoint routing, request validation, and response shaping.    |

The remaining files (`Models/` and the JSON converter) provide the supporting types used by these two.

## Design decisions

The core logic in `RateLimitHelper` is purely functional, so every call returns a new limiter state rather than mutating the existing one. This keeps the core easy to test without any mocking and separates it clearly from the API layer.

`RateLimiter` and `Bucket` are C# records so immutability is enforced by the type system. Internally timestamps use `DateTimeOffset` to keep the time arithmetic readable. A `JsonConverter` handles converting to Unix floats at the API boundary.

Backward timestamps (where the new timestamp is earlier than the last recorded one) are handled by clamping elapsed time to zero rather than rejecting the request. No leak occurs but the request is still processed normally. This is more tolerant of clock skew than returning an error.

Negative timestamps are rejected with `400 Bad Request` at the API layer as they represent dates before the Unix epoch and are not meaningful in this context.

## Assumptions and trade-offs

The spec states single-threaded execution is assumed, so no locking is implemented. The shared `limiter` variable in `Program.cs` is not thread-safe. Concurrent requests would cause a race condition where one request overwrites another's updated state. In a production system this would need a lock.

Timestamps come from the client, not the server. This is per spec but means a client could send a future timestamp to manipulate their leak rate.


Buckets are never evicted so memory grows with unique user count. Restarting the server clears all state.
