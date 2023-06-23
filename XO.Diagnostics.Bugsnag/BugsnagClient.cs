using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using XO.Diagnostics.Bugsnag.Models;

namespace XO.Diagnostics.Bugsnag;

public sealed class BugsnagClient
{
    private readonly HttpClient _httpClient;
    private readonly BugsnagClientOptions _options;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly BugsnagNotifier _notifier;

    public BugsnagClient(HttpClient httpClient, IOptions<BugsnagClientOptions> optionsAccessor)
    {
        _httpClient = httpClient;
        _options = optionsAccessor.Value;
        _jsonSerializerOptions = BugsnagJsonContext.Default.Options;
        _notifier = new BugsnagNotifier(ThisAssembly.AssemblyName, ThisAssembly.AssemblyInformationalVersion);
    }

    public BugsnagNotifier Notifier => _notifier;

    public async Task<StatusResponse> CreateBuildAsync(BuildRequest request, CancellationToken cancellationToken = default)
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

    public async Task<Guid> CreateSessionsAsync(SessionsRequest request, CancellationToken cancellationToken = default)
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

    public async Task<Guid> NotifyAsync(NotifyRequest request, CancellationToken cancellationToken = default)
    {
        using var message = CreateRequest(_options.Endpoints.Notify, request, payloadVersion: "5");

        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var firstEventId = response.Headers.GetValues("Bugsnag-Event-ID").Single();

        return Guid.Parse(firstEventId);
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

            if (!response.IsSuccessStatusCode)
                throw new BugsnagRequestException(statusResponse, statusCode: response.StatusCode);
        }
        catch (Exception ex)
        {
            throw new BugsnagRequestException(inner: ex, statusCode: response.StatusCode);
        }

        return statusResponse!;
    }
}
