using System.Text.Json.Serialization;
using XO.Diagnostics.Bugsnag.Models;

namespace XO.Diagnostics.Bugsnag;

[JsonSerializable(typeof(BuildRequest))]
[JsonSerializable(typeof(NotifyRequest))]
[JsonSerializable(typeof(SessionsRequest))]
[JsonSerializable(typeof(StatusResponse))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
    )]
public sealed partial class BugsnagJsonContext : JsonSerializerContext
{
}
