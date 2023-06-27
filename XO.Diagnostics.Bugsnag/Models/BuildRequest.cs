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

public sealed class SourceControl : IEquatable<SourceControl>
{
    public SourceControl(string repository, string revision)
    {
        Repository = repository;
        Revision = revision;
    }

    public string? Provider { get; set; }
    public string Repository { get; set; }
    public string Revision { get; set; }

    public bool Equals(SourceControl? other)
    {
        return other != null
            && other.Repository == this.Repository
            && other.Revision == this.Revision
            && other.Provider == this.Provider;
    }

    public override bool Equals(object? obj)
        => Equals(obj as SourceControl);

    public override int GetHashCode()
        => HashCode.Combine(Repository, Revision);

    public override string ToString()
        => $"{Repository}@{Revision}";
}
