# XO.Diagnostics.Bugsnag.OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/XO.Diagnostics.Bugsnag.OpenTelemetry)](https://www.nuget.org/packages/XO.Diagnostics.Bugsnag.OpenTelemetry)
[![GitHub Actions Status](https://img.shields.io/github/actions/workflow/status/xo-energy/XO.Diagnostics/ci.yml?branch=main&logo=github)](https://github.com/xo-energy/XO.Diagnostics/actions/workflows/ci.yml)

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
