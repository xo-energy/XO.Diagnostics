namespace XO.Diagnostics.Bugsnag.Models;

public class BugsnagNotifier
{
    public BugsnagNotifier(string name, string version, string? url = null, BugsnagNotifier[]? dependencies = null)
    {
        this.Name = name;
        this.Version = version;
        this.Url = url;
        this.Dependencies = dependencies;
    }

    public string Name { get; }
    public string Version { get; }
    public string? Url { get; }
    public BugsnagNotifier[]? Dependencies { get; }
}

