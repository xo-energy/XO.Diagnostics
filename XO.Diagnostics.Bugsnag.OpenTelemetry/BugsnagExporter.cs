using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using XO.Diagnostics.Bugsnag.Models;

namespace XO.Diagnostics.Bugsnag.OpenTelemetry;

internal sealed partial class BugsnagExporter : BaseExporter<Activity>
{
    private static readonly Regex StacktraceRegex = GetStacktraceRegex();

    private readonly BugsnagClient _client;
    private readonly DateTimeOffset _appStartTime;

    private BugsnagNotifier? _notifier;
    private NotifyEventDevice? _device;
    private NotifyEventApp? _app;

    private NotifyRequest? _notifyRequest;
    private SessionsRequest? _sessionsRequest;

    public BugsnagExporter(BugsnagClient client)
    {
        _client = client;

        using (var process = Process.GetCurrentProcess())
            _appStartTime = process.StartTime.ToUniversalTime();
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        using var suppress = SuppressInstrumentationScope.Begin();

        var now = DateTimeOffset.Now;

        // initialize static information
        _app ??= GetApp();
        _app.Duration = (int)(now - _appStartTime).TotalMilliseconds;
        _device ??= GetDevice();
        _device.Time = now;
        _notifier ??= GetNotifier();

        // initialize the requests
        _notifyRequest ??= new(_notifier);
        _sessionsRequest ??= new(_notifier)
        {
            App = _app,
            Device = _device,
        };

        try
        {
            foreach (var activity in batch)
            {
                Session? session = null;

                var root = activity;
                while (root.Parent is not null)
                    root = root.Parent;

                var captureSession = activity.Parent is null || activity.HasRemoteParent;
                var status = activity.GetStatus();

                if (captureSession)
                {
                    session = new Session(root.Id!)
                    {
                        StartedAt = new DateTimeOffset(root.StartTimeUtc, TimeSpan.Zero),
                    };

                    _sessionsRequest.Sessions.Add(session);
                }

                var notifyEvent = new NotifyEvent()
                {
                    App = _app,
                    Context = activity.DisplayName,
                    Device = _device,
                    Session = new(root.Id!, new DateTimeOffset(root.StartTimeUtc, TimeSpan.Zero)),
                };

                // mark the event as unhandled if it is the session root
                notifyEvent.Unhandled = captureSession;

                // translate data from baggage and tags
                TranslateTraceData(activity, notifyEvent, session);

                // translate events to exceptions and breadcrumbs
                foreach (var activityEvent in activity.EnumerateEvents())
                {
                    if (activityEvent.Name == "exception")
                        AddNotifyEventException(notifyEvent, activityEvent);
                    else
                        AddNotifyEventBreadcrumb(notifyEvent, activityEvent);
                }

                // at least one exception is required
                if (notifyEvent.Exceptions.Count > 0)
                    _notifyRequest.Events.Add(notifyEvent);
            }

            if (_sessionsRequest.Sessions.Count > 0)
                _client.CreateSessionsAsync(_sessionsRequest).GetAwaiter().GetResult();

            if (_notifyRequest.Events.Count > 0)
                _client.NotifyAsync(_notifyRequest).GetAwaiter().GetResult();

            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
        finally
        {
            _notifyRequest.Events.Clear();
            _sessionsRequest.Sessions.Clear();
            _sessionsRequest.SessionCounts.Clear();
        }
    }

    private void TranslateTraceData(Activity activity, NotifyEvent notifyEvent, Session? session)
    {
        const string RequestHeaderPrefix = "http.request.header.";

        var user = new BugsnagUser();
        var request = new NotifyEventRequest();

        foreach (var (key, value) in activity.Baggage)
        {
            if (String.IsNullOrWhiteSpace(value)) continue;

            switch (key)
            {
                case TraceSemanticConventions.AttributeDbUser:
                    user.Name = value;
                    break;
                case TraceSemanticConventions.AttributeEnduserId:
                    user.Id = value;
                    break;
                case TraceSemanticConventions.AttributeEnduserRole when user.Name == null:
                    user.Name = value;
                    break;
            }
        }

        foreach (var (key, value) in activity.EnumerateTagObjects())
        {
            if (value is string valueString)
            {
                switch (key)
                {
                    case TraceSemanticConventions.AttributeDbUser:
                        user.Name = valueString;
                        break;
                    case TraceSemanticConventions.AttributeEnduserId:
                        user.Id = valueString;
                        break;
                    case TraceSemanticConventions.AttributeEnduserRole when user.Name == null:
                        user.Name = valueString;
                        break;
                    case TraceSemanticConventions.AttributeHttpClientIp:
                        request.ClientIp = valueString;
                        break;
                    case TraceSemanticConventions.AttributeHttpMethod:
                        request.HttpMethod = valueString;
                        break;
                    case TraceSemanticConventions.AttributeHttpUrl:
                        request.Url = valueString;
                        break;
                }
            }
            else if (value is string[] valueStringArray && valueStringArray.Length > 0)
            {
                switch (key)
                {
                    case var _ when key.StartsWith(RequestHeaderPrefix):
                        var header = key.Substring(RequestHeaderPrefix.Length).Replace('_', '-');
                        var headerValue = String.Join(", ", valueStringArray);

                        request.Headers ??= new();
                        request.Headers[header] = headerValue;

                        if (header.Equals("referer", StringComparison.OrdinalIgnoreCase))
                            request.Referer = headerValue;

                        break;
                }
            }
        }

        if (user.Id != null ||
            user.Name != null ||
            user.Email != null)
        {
            notifyEvent.User = user;
            if (session != null) session.User = user;
        }

        if (request.ClientIp != null ||
            request.HttpMethod != null ||
            request.Url != null ||
            request.Headers != null)
        {
            notifyEvent.Request = request;
        }
    }

    private void AddNotifyEventBreadcrumb(NotifyEvent notifyEvent, ActivityEvent activityEvent)
    {
        var breadcrumb = new NotifyEventBreadcrumb(
            activityEvent.Timestamp,
            activityEvent.Name,
            NotifyEventBreadcrumbType.log);

        foreach (var (key, value) in activityEvent.Tags)
        {
            if (value is string valueString)
            {
                breadcrumb.MetaData ??= new();
                breadcrumb.MetaData[key] = valueString;
            }
        }

        notifyEvent.Breadcrumbs ??= new();
        notifyEvent.Breadcrumbs.Add(breadcrumb);
    }

    private void AddNotifyEventException(NotifyEvent notifyEvent, ActivityEvent activityEvent)
    {
        string? errorClass = null;
        string? message = null;
        string? stackTrace = null;

        foreach (var (key, value) in activityEvent.Tags)
        {
            if (value is not string valueString)
                continue;

            switch (key)
            {
                case TraceSemanticConventions.AttributeExceptionEscaped when valueString.Equals("true", StringComparison.OrdinalIgnoreCase):
                    notifyEvent.Unhandled = true;
                    break;
                case TraceSemanticConventions.AttributeExceptionType:
                    errorClass = valueString;
                    break;
                case TraceSemanticConventions.AttributeExceptionMessage:
                    message = valueString;
                    break;
                case TraceSemanticConventions.AttributeExceptionStacktrace:
                    stackTrace = valueString;
                    break;
            }
        }

        if (String.IsNullOrWhiteSpace(errorClass))
            errorClass = nameof(Exception);

        var notifyEventException = new NotifyEventException(errorClass, message, type: BugsnagStacktraceType.csharp);

        if (!String.IsNullOrWhiteSpace(stackTrace))
        {
            var matches = StacktraceRegex.Matches(stackTrace);

            notifyEventException.Stacktrace.EnsureCapacity(matches.Count);

            foreach (Match match in matches)
            {
                var method = match.Groups["method"];
                var file = match.Groups["file"];
                var line = match.Groups["line"];

                var stacktraceLine = new NotifyEventStacktrace(
                    file.Value,
                    line.Success ? int.Parse(line.Value) : 0,
                    method.Value);

                stacktraceLine.InProject = file.Success;

                notifyEventException.Stacktrace.Add(stacktraceLine);
            }
        }

        notifyEvent.Exceptions.Add(notifyEventException);
    }

    private NotifyEventApp GetApp()
    {
        var app = new NotifyEventApp();
        var resource = this.ParentProvider?.GetResource()
            ?? ResourceBuilder.CreateDefault().Build();

        foreach (var (key, value) in resource.Attributes)
        {
            if (value is not string valueString)
                continue;

            switch (key)
            {
                case ResourceSemanticConventions.AttributeServiceName:
                    app.Id = valueString;
                    break;
                case ResourceSemanticConventions.AttributeServiceVersion:
                    app.Version = valueString;
                    break;
            }
        }

        var entryAssembly = Assembly.GetEntryAssembly();

        if (String.IsNullOrWhiteSpace(app.Id))
            app.Id = entryAssembly?.GetName().Name ?? String.Empty;

        if (String.IsNullOrWhiteSpace(app.Version)
            && entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>() is { } versionAttribute)
            app.Version = versionAttribute.InformationalVersion;

        app.BinaryArch = RuntimeInformation.ProcessArchitecture switch
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

    private NotifyEventDevice GetDevice()
    {
        var resource = this.ParentProvider?.GetResource()
            ?? ResourceBuilder.CreateDefault().Build();

        var device = new NotifyEventDevice()
        {
            Hostname = Dns.GetHostEntry("localhost").HostName,
            OsName = RuntimeInformation.RuntimeIdentifier,
            OsVersion = Environment.OSVersion.Version.ToString(),
            RuntimeVersions = new()
            {
                [BugsnagRuntimeName.dotnet] = Environment.Version.ToString(),
            },
        };

        foreach (var (key, value) in resource.Attributes)
        {
            if (value is not string valueString)
                continue;

            switch (key)
            {
                case ResourceSemanticConventions.AttributeBrowserUserAgent:
                    device.UserAgent = valueString;
                    break;
                case ResourceSemanticConventions.AttributeDeviceId:
                    device.Id = valueString;
                    break;
                case ResourceSemanticConventions.AttributeDeviceManufacturer:
                    device.Manufacturer = valueString;
                    break;
                case ResourceSemanticConventions.AttributeDeviceModelIdentifier:
                    device.ModelNumber = valueString;
                    break;
                case ResourceSemanticConventions.AttributeDeviceModelName:
                    device.Model = valueString;
                    break;
                case ResourceSemanticConventions.AttributeHostId:
                    device.Id = valueString;
                    break;
            }
        }

        return device;
    }

    private BugsnagNotifier GetNotifier()
    {
        var resource = this.ParentProvider?.GetResource()
            ?? ResourceBuilder.CreateDefault().Build();

        var versionAttribute = resource.Attributes.FirstOrDefault(
            x => x.Key == ResourceSemanticConventions.AttributeTelemetrySdkVersion);

        return new BugsnagNotifier(
            ThisAssembly.AssemblyName,
            ThisAssembly.AssemblyInformationalVersion,
            dependencies: new[] {
                _client.Notifier,
                new BugsnagNotifier("OpenTelemetry", versionAttribute.Value as string ?? String.Empty),
            });
    }

    private const string StacktraceRegexPattern = @"^\s+?at (?<method>.+?)( in (?<file>.+?):line (?<line>\d+))?\s*$";

#if NET7_0_OR_GREATER
    [GeneratedRegex(StacktraceRegexPattern, RegexOptions.ExplicitCapture | RegexOptions.Multiline)]
    private static partial Regex GetStacktraceRegex();
#else
    private static Regex GetStacktraceRegex()
        => new Regex(StacktraceRegexPattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.Multiline);
#endif
}
