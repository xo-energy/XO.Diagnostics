# XO.Diagnostics.Bugsnag.OpenTelemetry

Exports OpenTelemetry traces to [BugSnag](https://www.bugsnag.com/).

## Usage

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
