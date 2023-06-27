using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using XO.Diagnostics.Bugsnag.Models;

namespace XO.Diagnostics.Bugsnag;

public sealed class BugsnagClientTest
{
    private string AppVersion { get; } = "1.2.3";
    private SourceControl AppSourceControl { get; } = new SourceControl("https://github.com/foo/bar", "123abc");
    private Guid BugsnagEventId { get; } = Guid.NewGuid();
    private Guid BugsnagSessionId { get; } = Guid.NewGuid();

    private BugsnagClientOptions Options { get; }
        = new BugsnagClientOptions
        {
            ApiKey = Guid.NewGuid().ToString(),
        };

    private StatusResponse StatusResponseError { get; }
        = new StatusResponse("error")
        {
            Errors = new[] {
                "error1",
                "error2",
            },
            Warnings = new[] {
                "warning1",
                "warning2",
            },
        };

    private async Task<IHost> StartHostAsync(Action<IApplicationBuilder>? configure = null)
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

    private void DefaultConfigure(IApplicationBuilder app)
    {
        app.Run(async context =>
        {
            switch (context.Request.Host.Value)
            {
                case "build.bugsnag.com" when context.Request.Path.Value == "/":
                    var buildRequest = await context.Request.ReadFromJsonAsync<BuildRequest>();

                    Assert.Multiple(
                        () => Assert.Equal("application/json; charset=utf-8", context.Request.ContentType),
                        () => Assert.Equal(Options.ApiKey, buildRequest?.ApiKey),
                        () => Assert.Equal(AppVersion, buildRequest?.AppVersion),
                        () => Assert.Equal(AppSourceControl, buildRequest?.SourceControl));

                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsJsonAsync(
                        new StatusResponse("ok"));
                    break;

                case "notify.bugsnag.com" when context.Request.Path.Value == "/":
                    var notifyRequest = await context.Request.ReadFromJsonAsync<NotifyRequest>();

                    Assert.Multiple(
                        () => Assert.Equal("application/json; charset=utf-8", context.Request.ContentType),
                        () => Assert.Equal(Options.ApiKey, context.Request.Headers["Bugsnag-Api-Key"]),
                        () => Assert.Equal(NotifyRequest.BugsnagPayloadVersion, context.Request.Headers["Bugsnag-Payload-Version"]),
                        () => Assert.Contains("Bugsnag-Sent-At", context.Request.Headers));

                    context.Response.Headers["Bugsnag-Event-ID"] = BugsnagEventId.ToString();
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    break;

                case "sessions.bugsnag.com" when context.Request.Path.Value == "/":
                    var sessionsRequest = await context.Request.ReadFromJsonAsync<SessionsRequest>();

                    Assert.Multiple(
                        () => Assert.Equal("application/json; charset=utf-8", context.Request.ContentType),
                        () => Assert.Equal(Options.ApiKey, context.Request.Headers["Bugsnag-Api-Key"]),
                        () => Assert.Equal(SessionsRequest.BugsnagPayloadVersion, context.Request.Headers["Bugsnag-Payload-Version"]),
                        () => Assert.Contains("Bugsnag-Sent-At", context.Request.Headers));

                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Bugsnag-Session-UUID"] = BugsnagSessionId.ToString();
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsJsonAsync(
                        new StatusResponse("ok"));
                    break;

                default:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    break;
            }
        });
    }

    [Fact]
    public async Task PostBuildAsync_ParsesStatusResponse()
    {
        using var host = await StartHostAsync();

        var bugsnagClient = new BugsnagClient(host.GetTestClient(), this.Options);
        var request = new BuildRequest(Options.ApiKey, AppVersion, AppSourceControl);

        var response = await bugsnagClient.PostBuildAsync(request);

        Assert.Equal("ok", response.Status);
    }

    [Fact]
    public async Task PostBuildAsync_ThrowsBugsnagRequestException()
    {
        using var host = await StartHostAsync(
            app => app.Run(context =>
            {
                context.Response.StatusCode = 400;
                return Task.CompletedTask;
            }));

        var bugsnagClient = new BugsnagClient(host.GetTestClient(), this.Options);
        var request = new BuildRequest(Options.ApiKey, AppVersion, AppSourceControl);

        var ex = await Assert.ThrowsAsync<BugsnagRequestException>(
            async () => await bugsnagClient.PostBuildAsync(request));

        // should have an inner exception because we didn't return a valid JSON StatusResponse
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode),
            () => Assert.IsType<JsonException>(ex.InnerException));
    }

    [Fact]
    public async Task PostBuildAsync_ThrowsBugsnagRequestException_WithStatusResponse()
    {
        using var host = await StartHostAsync(
            app => app.Run(async context =>
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(StatusResponseError);
            }));

        var bugsnagClient = new BugsnagClient(host.GetTestClient(), this.Options);
        var request = new BuildRequest(Options.ApiKey, AppVersion, AppSourceControl);

        var ex = await Assert.ThrowsAsync<BugsnagRequestException>(
            async () => await bugsnagClient.PostBuildAsync(request));

        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode),
            () => Assert.Equal(StatusResponseError.Status, ex.Response?.Status),
            () => Assert.Equal<string>(StatusResponseError.Errors, ex.Response?.Errors ?? Array.Empty<string>()),
            () => Assert.Equal<string>(StatusResponseError.Warnings, ex.Response?.Warnings ?? Array.Empty<string>())
            );
    }

    [Fact]
    public async Task PostEventsAsync_ParsesEventIdHeader()
    {
        using var host = await StartHostAsync();

        var bugsnagClient = new BugsnagClient(host.GetTestClient(), this.Options);
        var request = bugsnagClient.CreateSampleNotifyRequest();

        var id = await bugsnagClient.PostEventsAsync(request);

        Assert.Equal(BugsnagEventId, id);
    }

    [Fact]
    public async Task PostEventsAsync_ThrowsHttpRequestException()
    {
        using var host = await StartHostAsync(
            app => app.Run(context =>
            {
                context.Response.StatusCode = 400;
                return Task.CompletedTask;
            }));

        var bugsnagClient = new BugsnagClient(host.GetTestClient(), this.Options);
        var request = bugsnagClient.CreateSampleNotifyRequest();

        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await bugsnagClient.PostEventsAsync(request));
    }

    [Fact]
    public async Task PostSessionsAsync_ParsesSessionUuidHeader()
    {
        using var host = await StartHostAsync();

        var bugsnagClient = new BugsnagClient(host.GetTestClient(), this.Options);
        var request = bugsnagClient.CreateSampleSessionsRequest();

        var id = await bugsnagClient.PostSessionsAsync(request);

        Assert.Equal(BugsnagSessionId, id);
    }

    [Fact]
    public async Task PostSessionsAsync_ThrowsBugsnagRequestException()
    {
        using var host = await StartHostAsync(
            app => app.Run(context =>
            {
                context.Response.StatusCode = 400;
                return Task.CompletedTask;
            }));

        var bugsnagClient = new BugsnagClient(host.GetTestClient(), this.Options);
        var request = bugsnagClient.CreateSampleSessionsRequest();

        var ex = await Assert.ThrowsAsync<BugsnagRequestException>(
            async () => await bugsnagClient.PostSessionsAsync(request));

        // should have an inner exception because we didn't return a valid JSON StatusResponse
        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode),
            () => Assert.IsType<JsonException>(ex.InnerException));
    }

    [Fact]
    public async Task PostSessionsAsync_ThrowsBugsnagRequestException_WithStatusResponse()
    {
        using var host = await StartHostAsync(
            app => app.Run(async context =>
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(StatusResponseError);
            }));

        var bugsnagClient = new BugsnagClient(host.GetTestClient(), this.Options);
        var request = bugsnagClient.CreateSampleSessionsRequest();

        var ex = await Assert.ThrowsAsync<BugsnagRequestException>(
            async () => await bugsnagClient.PostSessionsAsync(request));

        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode),
            () => Assert.Equal(StatusResponseError.Status, ex.Response?.Status),
            () => Assert.Equal<string>(StatusResponseError.Errors, ex.Response?.Errors ?? Array.Empty<string>()),
            () => Assert.Equal<string>(StatusResponseError.Warnings, ex.Response?.Warnings ?? Array.Empty<string>())
            );
    }
}
