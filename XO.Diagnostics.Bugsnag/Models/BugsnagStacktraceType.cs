using System.Text.Json.Serialization;

namespace XO.Diagnostics.Bugsnag.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BugsnagStacktraceType
{
    android,
    browserjs,
    c,
    cocoa,
    csharp,
    electronnodejs,
    electronrendererjs,
    expojs,
    go,
    java,
    nodejs,
    php,
    python,
    reactnativejs,
    ruby,
}
