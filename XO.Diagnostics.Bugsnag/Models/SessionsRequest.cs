namespace XO.Diagnostics.Bugsnag.Models;

public sealed class SessionsRequest
{
    public SessionsRequest(
        BugsnagNotifier notifier,
        List<Session>? sessions = null,
        List<SessionCount>? sessionCounts = null)
    {
        this.Notifier = notifier;
        this.Sessions = sessions ?? new(0);
        this.SessionCounts = sessionCounts ?? new(0);
    }

    public BugsnagNotifier Notifier { get; set; }
    public SessionApp? App { get; set; }
    public SessionDevice? Device { get; set; }
    public List<Session> Sessions { get; set; }
    public List<SessionCount> SessionCounts { get; set; }
}

public class SessionApp
{
    public string? Type { get; set; }
    public string? ReleaseStage { get; set; }
    public string? Version { get; set; }
    public int? VersionCode { get; set; }
    public string? BundleVersion { get; set; }
    public string? CodeBundleId { get; set; }
}

public class SessionDevice
{
    public string? Hostname { get; set; }
    public string? Id { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? ModelNumber { get; set; }
    public bool? Jailbroken { get; set; }
    public string? OsName { get; set; }
    public string? OsVersion { get; set; }
    public string? UserAgent { get; set; }
    public Dictionary<BugsnagRuntimeName, string>? RuntimeVersions { get; set; }
}

public class Session
{
    public Session(string id)
    {
        this.Id = id;
    }

    public string Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public BugsnagUser? User { get; set; }
}

public class SessionCount
{
    public DateTimeOffset StartedAt { get; set; }
    public int SessionsStarted { get; set; }
}

