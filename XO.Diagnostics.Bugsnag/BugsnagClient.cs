using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using XO.Diagnostics.Bugsnag.Models;

namespace XO.Diagnostics.Bugsnag;

public sealed class BugsnagClient
{
    private static readonly Assembly? _entryAssembly = Assembly.GetEntryAssembly();
    private static readonly AssemblyName? _entryAssemblyName = _entryAssembly?.GetName();

    private readonly HttpClient _httpClient;
    private readonly BugsnagClientOptions _options;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly BugsnagNotifier _notifier;

    public BugsnagClient(HttpClient httpClient, BugsnagClientOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        _jsonSerializerOptions = BugsnagJsonContext.Default.Options;
        _notifier = new BugsnagNotifier(ThisAssembly.AssemblyName, ThisAssembly.AssemblyInformationalVersion);
    }

    /// <summary>
    /// Gets an instance of <see cref="BugsnagNotifier"/> that describes this client.
    /// </summary>
    public BugsnagNotifier Notifier => _notifier;

    /// <summary>
    /// Creates a new instance of <see cref="NotifyEventApp"/> with default values populated from configuration, the
    /// entry assembly, and the runtime environment.
    /// </summary>
    public NotifyEventApp CreateDefaultApp()
    {
        var app = new NotifyEventApp();

        // prioritize values provided by the user
        if (_options.App is not null)
        {
            app.Id = _options.App.Id;
            app.BuildUUID = _options.App.BuildUUID;
            app.DsymUUIDs = _options.App.DsymUUIDs;
            app.Type = _options.App.Type;
            app.ReleaseStage = _options.App.ReleaseStage;
            app.Version = _options.App.Version;
            app.VersionCode = _options.App.VersionCode;
            app.BundleVersion = _options.App.BundleVersion;
            app.CodeBundleId = _options.App.CodeBundleId;
        }

        // fill in missing app values from the entry assembly
        if (app.Id is null || app.Version is null)
        {
            var version = _entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (version is null)
                version = _entryAssemblyName?.Version?.ToString();

            app.Id ??= _entryAssemblyName?.Name;
            app.Version ??= version;
        }

        // fill in missing values from the runtime environment
        app.BinaryArch ??= RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => BugsnagBinaryArch.x86,
            Architecture.X64 => BugsnagBinaryArch.amd64,
            Architecture.Arm => BugsnagBinaryArch.arm32,
            Architecture.Arm64 => BugsnagBinaryArch.arm64,
#if NET7_0_OR_GREATER
            Architecture.Armv6 => BugsnagBinaryArch.armv6,
#endif
            _ => null,
        };

        return app;
    }

    /// <summary>
    /// Creates a new instance of <see cref="NotifyEventDevice"/> with default values populated from configuration and
    /// the runtime environment.
    /// </summary>
    public NotifyEventDevice CreateDefaultDevice()
    {
        var device = new NotifyEventDevice();

        // prioritize values provided by the user
        if (_options.Device is not null)
        {
            device.Hostname = _options.Device.Hostname;
            device.Id = _options.Device.Id;
            device.Manufacturer = _options.Device.Manufacturer;
            device.Model = _options.Device.Model;
            device.ModelNumber = _options.Device.ModelNumber;
            device.Jailbroken = _options.Device.Jailbroken;
            device.OsName = _options.Device.OsName;
            device.OsVersion = _options.Device.OsVersion;
            device.UserAgent = _options.Device.UserAgent;

            if (_options.Device.RuntimeVersions is not null)
                device.RuntimeVersions = new(_options.Device.RuntimeVersions);
        }

        // fill in any missing values from the runtime environment
        device.Hostname ??= Dns.GetHostEntry("localhost").HostName;
        device.OsName ??= RuntimeInformation.RuntimeIdentifier;
        device.OsVersion ??= Environment.OSVersion.Version.ToString();
        device.RuntimeVersions ??= new();
        device.RuntimeVersions.TryAdd(BugsnagRuntimeName.dotnet, Environment.Version.ToString());

        return device;
    }

    public async Task<StatusResponse> PostBuildAsync(BuildRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            _options.Endpoints.Build,
            request,
            options: _jsonSerializerOptions,
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return await ReadStatusResponseAsync(response, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Guid> PostEventsAsync(NotifyRequest request, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(_options.Endpoints.Notify, request, payloadVersion: "5");

        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var firstEventId = response.Headers.GetValues("Bugsnag-Event-ID").Single();

        return Guid.Parse(firstEventId);
    }

    public async Task<Guid> PostSessionsAsync(SessionsRequest request, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(_options.Endpoints.Sessions, request, payloadVersion: "1.0");

        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        // read the response body to throw a descriptive exception if we can parse it
        _ = await ReadStatusResponseAsync(response, cancellationToken)
            .ConfigureAwait(false);

        var sessionGuid = response.Headers.GetValues("Bugsnag-Session-UUID").Single();

        return Guid.Parse(sessionGuid);
    }

    private HttpRequestMessage CreateRequest<TContent>(string endpoint, TContent payload, string payloadVersion)
    {
        return new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload, options: _jsonSerializerOptions),
            Headers = {
                { "Bugsnag-Api-Key", _options.ApiKey },
                { "Bugsnag-Payload-Version", payloadVersion },
                { "Bugsnag-Sent-At", DateTimeOffset.UtcNow.ToString("O") },
            },
        };
    }

    private async Task<StatusResponse> ReadStatusResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        StatusResponse? statusResponse;
        try
        {
            statusResponse = await response.Content.ReadFromJsonAsync<StatusResponse>(
                options: _jsonSerializerOptions,
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new BugsnagRequestException(inner: ex, statusCode: response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
            throw new BugsnagRequestException(statusResponse, statusCode: response.StatusCode);

        return statusResponse!;
    }
}
