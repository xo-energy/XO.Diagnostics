using System.Text.Json.Serialization;

namespace XO.Diagnostics.Bugsnag.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BugsnagSeverity
{
    error,
    warning,
    info,
}
