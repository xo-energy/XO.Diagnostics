using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using XO.Diagnostics.Bugsnag.Models;

namespace XO.Diagnostics.Bugsnag;

public abstract class BugsnagTest
{
    protected string AppName { get; } = nameof(BugsnagTest);
    protected string AppVersion { get; } = "1.2.3";
    protected SourceControl AppSourceControl { get; } = new SourceControl("https://github.com/foo/bar", "123abc");
    protected Guid BugsnagEventId { get; } = Guid.NewGuid();
    protected Guid BugsnagSessionId { get; } = Guid.NewGuid();
    protected string DeviceId { get; } = Guid.NewGuid().ToString();
    protected string DeviceManufacturer { get; } = "ACME Corp";
    protected string DeviceModelIdentifier { get; } = "BFG-9000";
    protected string DeviceModelName { get; } = "Big Friendly Giant";
    protected string HostId { get; } = "ec2";
    protected string UserAgent { get; } = "Mozilla/5.0";

    protected BugsnagClientOptions Options { get; }
        = new BugsnagClientOptions
        {
            ApiKey = Guid.NewGuid().ToString(),
        };

    protected virtual void ConfigureResource(ResourceBuilder builder)
    {
        builder.AddService(AppName, serviceVersion: AppVersion);
        builder.AddAttributes(new Dictionary<string, object>()
        {
            [ResourceSemanticConventions.AttributeBrowserUserAgent] = UserAgent,
            [ResourceSemanticConventions.AttributeDeviceId] = DeviceId,
            [ResourceSemanticConventions.AttributeDeviceManufacturer] = DeviceManufacturer,
            [ResourceSemanticConventions.AttributeDeviceModelIdentifier] = DeviceModelIdentifier,
            [ResourceSemanticConventions.AttributeDeviceModelName] = DeviceModelName,
            [ResourceSemanticConventions.AttributeHostId] = HostId,
        });
    }

    protected async Task<IHost> StartHostAsync(Action<IApplicationBuilder>? configure = null)
    {
        return await new HostBuilder()
            .ConfigureWebHost(builder =>
            {
                configure ??= DefaultConfigure;

                builder.UseTestServer();
                builder.Configure(configure);
            })
            .StartAsync();
    }

    protected abstract void DefaultConfigure(IApplicationBuilder app);
}
