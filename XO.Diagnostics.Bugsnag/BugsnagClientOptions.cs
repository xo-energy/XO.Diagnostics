using System.ComponentModel.DataAnnotations;

namespace XO.Diagnostics.Bugsnag;

public class BugsnagClientOptions
{
    public const string Section = "Bugsnag";

    [Required(AllowEmptyStrings = false)]
    public required string ApiKey { get; set; }

    public BugsnagClientEndpoints Endpoints { get; set; } = new();

    public string? Proxy { get; set; }
}

public sealed class BugsnagClientEndpoints
{
    public string Build { get; set; } = "https://build.bugsnag.com/";

    public string Notify { get; set; } = "https://notify.bugsnag.com/";

    public string Sessions { get; set; } = "https://sessions.bugsnag.com/";
}
