using XO.Diagnostics.Bugsnag.Models;

namespace XO.Diagnostics.Bugsnag;

internal static class BugsnagClientExtensions
{
    public static NotifyRequest CreateSampleNotifyRequest(this BugsnagClient bugsnagClient)
    {
        var request = new NotifyRequest(bugsnagClient.Notifier)
        {
            Events = {
                new() {
                    App = bugsnagClient.CreateDefaultApp(),
                    Device = bugsnagClient.CreateDefaultDevice(),
                    Exceptions = {
                        new(
                            "Exception",
                            "An error occurred",
                            stacktrace: new () {
                                new NotifyEventStacktrace("source.cs", 42, "Program.Main()"),
                            },
                            type: BugsnagStacktraceType.csharp),
                    },
                    Unhandled = true,
                },
            },
        };

        return request;
    }

    public static SessionsRequest CreateSampleSessionsRequest(this BugsnagClient bugsnagClient)
    {
        var request = new SessionsRequest(bugsnagClient.Notifier)
        {
            App = bugsnagClient.CreateDefaultApp(),
            Device = bugsnagClient.CreateDefaultDevice(),
            Sessions = new() {
                new(Guid.NewGuid().ToString()) {
                    StartedAt = DateTimeOffset.UtcNow,
                },
            },
        };

        return request;
    }
}
