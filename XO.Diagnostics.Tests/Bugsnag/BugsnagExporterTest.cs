using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;
using XO.Diagnostics.Bugsnag.Models;
using XO.Diagnostics.Bugsnag.OpenTelemetry;

namespace XO.Diagnostics.Bugsnag;

public sealed class BugsnagExporterTest : BugsnagTest
{
    private static ActivitySource Source = new ActivitySource(ThisAssembly.AssemblyName);

    private readonly List<NotifyEvent> _notifyEvents = new();
    private readonly Dictionary<string, Session> _sessions = new();

    private IOptions<BugsnagExporterOptions> ExporterOptions { get; }
        = new OptionsWrapper<BugsnagExporterOptions>(new BugsnagExporterOptions());

    protected override void DefaultConfigure(IApplicationBuilder app)
    {
        app.Run(async context =>
        {
            switch (context.Request.Host.Value)
            {
                case "notify.bugsnag.com" when context.Request.Path.Value == "/":
                    var notifyRequest = await context.Request.ReadFromJsonAsync<NotifyRequest>();

                    Assert.NotNull(notifyRequest);
                    Assert.NotNull(notifyRequest.Events);

                    foreach (var notifyEvent in notifyRequest.Events)
                        _notifyEvents.Add(notifyEvent);

                    context.Response.Headers["Bugsnag-Event-ID"] = BugsnagEventId.ToString();
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    break;

                case "sessions.bugsnag.com" when context.Request.Path.Value == "/":
                    var sessionsRequest = await context.Request.ReadFromJsonAsync<SessionsRequest>();

                    Assert.NotNull(sessionsRequest);
                    Assert.NotNull(sessionsRequest.Sessions);

                    foreach (var session in sessionsRequest.Sessions)
                        _sessions[session.Id] = session;

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
    public async Task Export_DetectsDeviceFromResource()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(
            host,
            builder =>
            {
                builder.ConfigureResource(this.ConfigureResource);
            });

        var activity = CreateActivity("foo", _ => throw new InvalidOperationException());

        var notifyEvent = Assert.Single(_notifyEvents);

        Assert.Multiple(
            () => Assert.Equal(UserAgent, notifyEvent.Device?.UserAgent),
            () => Assert.Equal(DeviceManufacturer, notifyEvent.Device?.Manufacturer),
            () => Assert.Equal(DeviceModelIdentifier, notifyEvent.Device?.ModelNumber),
            () => Assert.Equal(DeviceModelName, notifyEvent.Device?.Model),
            () => Assert.Equal(HostId, notifyEvent.Device?.Id)
            );
    }

    [Fact]
    public async Task Export_DetectsRequestFromTags()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity = CreateActivity(
            "foo",
            activity =>
            {
                activity.SetTag(TraceSemanticConventions.AttributeHttpClientIp, "127.0.0.1");
                activity.SetTag(TraceSemanticConventions.AttributeHttpMethod, "GET");
                activity.SetTag(TraceSemanticConventions.AttributeHttpUrl, "http://localhost/foo");
                activity.SetTag("http.request.header.content_type", new string[] { "application/json" });
                activity.SetTag("http.request.header.referer", new string[] { "http://localhost/bar" });

                throw new InvalidOperationException();
            });

        var notifyEvent = Assert.Single(_notifyEvents);

        Assert.Multiple(
            () => Assert.Equal("127.0.0.1", notifyEvent.Request?.ClientIp),
            () => Assert.Equal("GET", notifyEvent.Request?.HttpMethod),
            () => Assert.Equal("http://localhost/foo", notifyEvent.Request?.Url),
            () => Assert.Equal("http://localhost/bar", notifyEvent.Request?.Referer),
            () => Assert.Equal("application/json", notifyEvent.Request?.Headers?["content-type"])
            );
    }

    [Fact]
    public async Task Export_ReportsEvent()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity = CreateActivity("foo", _ => throw new InvalidOperationException("bar"));

        Assert.Collection(
            _notifyEvents,
            notifyEvent => Assert.Multiple(
                () => Assert.Single(notifyEvent.Exceptions),
                () => Assert.Equal(typeof(InvalidOperationException).FullName, notifyEvent.Exceptions[0].ErrorClass),
                () => Assert.Equal("bar", notifyEvent.Exceptions[0].Message)
                )
            );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_ReportsEvent_InProject(bool inProject)
    {
        if (inProject)
        {
            ExporterOptions.Value.ProjectNamespaces = new[] { this.GetType().Namespace! };
        }

        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity = CreateActivity("foo", _ => throw new InvalidOperationException("bar"));

        Assert.Collection(
            _notifyEvents,
            notifyEvent => Assert.All(
                notifyEvent.Exceptions.SelectMany(x => x.Stacktrace),
                stack =>
                {
                    if (inProject)
                        Assert.True(stack.InProject);
                    else
                        Assert.Null(stack.InProject);
                })
            );
    }

    [Fact]
    public async Task Export_ReportsEvent_WithBreadcrumbs()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity = CreateActivity(
            "foo",
            activity =>
            {
                var tags = new ActivityTagsCollection
                {
                    { "foo", "bar" },
                    { "baz", "qux" },
                };

                activity.AddEvent(
                    new ActivityEvent("starting bar", tags: tags));

                throw new InvalidOperationException("bar");
            });

        Assert.Collection(
            _notifyEvents,
            notifyEvent => Assert.Multiple(
                () => Assert.Single(notifyEvent.Exceptions),
                () => Assert.Equal(typeof(InvalidOperationException).FullName, notifyEvent.Exceptions[0].ErrorClass),
                () => Assert.Equal("bar", notifyEvent.Exceptions[0].Message),
                () => Assert.Single(notifyEvent.Breadcrumbs!),
                () => Assert.Equal("starting bar", notifyEvent.Breadcrumbs![0].Name),
                () => Assert.Collection(
                    notifyEvent.Breadcrumbs![0].MetaData!,
                    entry => Assert.Equal(KeyValuePair.Create("foo", "bar"), entry),
                    entry => Assert.Equal(KeyValuePair.Create("baz", "qux"), entry)
                    )
                )
            );
    }

    [Fact]
    public async Task Export_ReportsEvent_WithInnerException()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity = CreateActivity(
            "foo",
            _ =>
            {
                try
                {
                    throw new ArgumentException();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("bar", ex);
                }
            });

        Assert.Collection(
            _notifyEvents,
            notifyEvent => Assert.Collection(
                notifyEvent.Exceptions,
                notifyEventException => Assert.Multiple(
                    () => Assert.Equal(typeof(InvalidOperationException).FullName, notifyEventException.ErrorClass),
                    () => Assert.Equal("bar", notifyEventException.Message)
                    ),
                notifyEventException => Assert.Equal(typeof(ArgumentException).FullName, notifyEventException.ErrorClass)
                )
            );
    }

    [Fact]
    public async Task Export_ReportsEvent_WithMultipleExceptions()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity = CreateActivity(
            "foo",
            activity =>
            {
                activity.RecordException(new InvalidOperationException("bar"));
                activity.RecordException(new ArgumentException());
            });

        Assert.Collection(
            _notifyEvents,
            notifyEvent => Assert.Multiple(
                () => Assert.Single(notifyEvent.Exceptions),
                () => Assert.Equal(typeof(InvalidOperationException).FullName, notifyEvent.Exceptions[0].ErrorClass),
                () => Assert.Equal("bar", notifyEvent.Exceptions[0].Message)
                ),
            notifyEvent => Assert.Multiple(
                () => Assert.Single(notifyEvent.Exceptions),
                () => Assert.Equal(typeof(ArgumentException).FullName, notifyEvent.Exceptions[0].ErrorClass)
                )
            );
    }

    [Fact]
    public async Task Export_ReportsSession_WithEnduserId()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity = CreateActivity(
            "foo",
            activity =>
            {
                activity.SetTag(TraceSemanticConventions.AttributeDbUser, "foouser");
                activity.SetTag(TraceSemanticConventions.AttributeEnduserId, "foo@bar.com");
            });

        Assert.Collection(
            _sessions.Values,
            session => Assert.Multiple(
                () => Assert.Equal("foouser", session.User?.Name),
                () => Assert.Equal("foo@bar.com", session.User?.Id)
                )
            );
    }

    [Fact]
    public async Task Export_ReportsSession()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity = CreateActivity("foo");

        Assert.Multiple(
            () => Assert.Collection(
                _sessions.Values,
                session => Assert.Equal(activity.RootId, session.Id)
                ),
            () => Assert.Empty(_notifyEvents));
    }

    [Fact]
    public async Task Export_ReportsSessionPerRootActivity()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity1 = CreateActivity("foo");
        var activity2 = CreateActivity("bar");

        Assert.Multiple(
            () => Assert.Collection(
                _sessions.Values,
                session => Assert.Equal(activity1.RootId, session.Id),
                session => Assert.Equal(activity2.RootId, session.Id)
                ),
            () => Assert.Empty(_notifyEvents));
    }

    [Fact]
    public async Task Export_ReportsSessionPerRootActivity_WithNestedActivity()
    {
        using var host = await StartHostAsync();
        using var tracerProvider = CreateTracerProvider(host);

        var activity1 = CreateActivity("foo", activity => _ = CreateActivity("nested"));
        var activity2 = CreateActivity("bar");

        Assert.Multiple(
            () => Assert.Collection(
                _sessions.Values,
                session => Assert.Equal(activity1.RootId, session.Id),
                session => Assert.Equal(activity2.RootId, session.Id)
                ),
            () => Assert.Empty(_notifyEvents));
    }

    private static Activity CreateActivity(string name, Action<Activity>? configure = null)
    {
        var activity = Source.StartActivity(name);

        Assert.NotNull(activity);

        try
        {
            configure?.Invoke(activity);
        }
        catch (Exception ex)
        {
            activity.RecordException(ex);
        }
        finally
        {
            activity.Dispose();
        }

        return activity;
    }

    private TracerProvider CreateTracerProvider(IHost host, Action<TracerProviderBuilder>? configure = null)
    {
        var bugsnagClient = new BugsnagClient(host.GetTestClient(), this.Options);

        var exporter = new BugsnagExporter(bugsnagClient, this.ExporterOptions);

        var tracerProviderBuilder = new TracerProviderBuilderBase()
            .AddSource(Source.Name)
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .SetSampler(new AlwaysOnSampler())
            ;

        configure?.Invoke(tracerProviderBuilder);

        return tracerProviderBuilder.Build() ?? throw new InvalidOperationException($"{nameof(TracerProviderBuilderBase)} returned null");
    }
}
