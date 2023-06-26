# XO.Diagnostics.BugSnag

A simple HTTP client library for the [BugSnag](https://www.bugsnag.com/) API. For a more complete solution, see [XO.Diagnostics.Bugsnag.OpenTelemetry](../XO.Diagnostics.Bugsnag.OpenTelemetry).

## Usage

1. Populate an instance of `BugsnagClientOptions` with your API key and any information about the host application or device that you want to report.

    ```csharp
    var options = new BugsnagClientOptions
    {
        ApiKey = "abcxyz",
    };
    ```

1. Create an instance of `BugsnagClient` using the options.

    ```csharp
    var client = new BugsnagClient(options);
    ```

1. Create instances of `NotifyEventApp` and `NotifyEventDevice` and customize them as needed.

    ```csharp
    var app = client.CreateDefaultApp();
    var device = client.CreateDefaultDevice();
    ```

1. Use the client to send events.

    ```csharp
    var notifyEvent = new NotifyEvent {
        App = app,
        Device = device,
        Exceptions = {
            new NotifyEventException {
                "System.Exception",
                "Something went wrong",
                stacktrace: new() {
                     new NotifyEventStacktrace("Program.cs", 42, "Program.Main()") { InProject = true },
                },
            },
        },
    };

    var request = new NotifyRequest(client.Notifier) {
        Events = { notifyEvent },
    };

    await client.PostEventsAsync(request, cancellationToken);
    ```
