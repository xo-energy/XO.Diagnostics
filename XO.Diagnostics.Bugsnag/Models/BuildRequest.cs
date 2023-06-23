namespace XO.Diagnostics.Bugsnag.Models;

public sealed class BuildRequest
{
    public BuildRequest(string apiKey, string appVersion, SourceControl sourceControl)
    {
        ApiKey = apiKey;
        AppVersion = appVersion;
        this.SourceControl = sourceControl;
    }

    public string ApiKey { get; set; }
    public string AppVersion { get; set; }
    public int? AppVersionCode { get; set; }
    public string? AppBundleVersion { get; set; }
    public string? BuilderName { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public SourceControl SourceControl { get; set; }
    public string? ReleaseStage { get; set; }
    public bool AutoAssignRelease { get; set; }
}

public sealed class SourceControl
{
    public SourceControl(string repository, string revision)
    {
        Repository = repository;
        Revision = revision;
    }

    public string? Provider { get; set; }
    public string Repository { get; set; }
    public string Revision { get; set; }
}
