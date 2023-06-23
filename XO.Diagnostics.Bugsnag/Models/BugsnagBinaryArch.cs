using System.Text.Json.Serialization;

namespace XO.Diagnostics.Bugsnag.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BugsnagBinaryArch
{
    x86,
    x86_64,
    arm32,
    arm64,
    arm64e,
    armv6,
    armv7,
    armv7f,
    armv7k,
    armv7s,
    armv8,
    amd64,
}
