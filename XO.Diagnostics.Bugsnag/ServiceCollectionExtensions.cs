using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace XO.Diagnostics.Bugsnag;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBugsnagClient(this IServiceCollection services)
    {
        services.AddOptions<BugsnagClientOptions>()
            .BindConfiguration(BugsnagClientOptions.Section)
            .ValidateDataAnnotations()
            ;

        services.AddHttpClient<BugsnagClient>(
            httpClient =>
            {
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{ThisAssembly.AssemblyName}/{ThisAssembly.AssemblyFileVersion}");
            })
            .ConfigurePrimaryHttpMessageHandler(services =>
            {
                var optionsAccessor = services.GetRequiredService<IOptions<BugsnagClientOptions>>();
                var options = optionsAccessor.Value;
                var handler = new SocketsHttpHandler();

                if (!String.IsNullOrWhiteSpace(options.Proxy))
                {
                    handler.Proxy = new WebProxy(options.Proxy);
                }

                return handler;
            })
            ;

        return services;
    }
}
