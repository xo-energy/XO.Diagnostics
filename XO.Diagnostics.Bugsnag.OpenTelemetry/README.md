# XO.Diagnostics.Bugsnag.OpenTelemetry

Exports OpenTelemetry traces to [BugSnag](https://www.bugsnag.com/).

## Usage

1. Configure your BugSnag API key; for example, in development:

    ```
    > dotnet user-secrets set 'Bugsnag:ApiKey' 'YOUR_API_KEY'
    ```

2. Add the `BugsnagExporter` to your OpenTelemetry configuration:

    ```csharp
    .ConfigureServices((context, services) =>
    {
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder.AddBugsnagExporter();
            });
    });
    ```
