using Microsoft.Extensions.DependencyInjection;
using XO.Diagnostics.Bugsnag;
using XO.Diagnostics.Bugsnag.OpenTelemetry;

namespace OpenTelemetry.Trace;

public static class TraceProviderBuilderExtensions
{
    public static TracerProviderBuilder AddBugsnagExporter(this TracerProviderBuilder builder)
    {
        builder.ConfigureServices(services => services.AddBugsnagClient());

        builder.AddProcessor(services =>
        {
            var exporter = ActivatorUtilities.CreateInstance<BugsnagExporter>(services);

            return new BatchActivityExportProcessor(exporter);
        });

        return builder;
    }
}
