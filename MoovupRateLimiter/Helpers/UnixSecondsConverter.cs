// Author: Mathew Goldston
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoovupRateLimiter;

public class UnixSecondsConverter : JsonConverter<DateTimeOffset>
{
    // Write-only: inbound timestamps arrive as a double? on AllowRequest and are converted
    // in the endpoint, so this converter is never used to deserialize a DateTimeOffset.
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteNumberValue((value - DateTimeOffset.UnixEpoch).TotalSeconds);
}
