namespace XO.Diagnostics.Bugsnag.OpenTelemetry;

public sealed class BugsnagExporterOptions
{
    public const string Section = "OpenTelemetry:Exporters:Bugsnag";

    public BugsnagInProjectCallback InProjectCallback { get; set; }
        = static (_, _, _, inProjectNamespace) => inProjectNamespace;

    public string[]? ProjectNamespaces { get; set; }

    public string[] TrimPathPrefixes { get; set; } = Array.Empty<string>();
}
