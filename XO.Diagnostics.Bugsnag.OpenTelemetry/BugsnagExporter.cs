using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using XO.Diagnostics.Bugsnag.Models;

namespace XO.Diagnostics.Bugsnag.OpenTelemetry;

internal sealed partial class BugsnagExporter : BaseExporter<Activity>
{
    private static readonly Regex StacktraceFrameRegex = GetStacktraceFrameRegex();
    private static readonly Regex StacktraceMessageRegex = GetStacktraceMessageRegex();

    private readonly BugsnagClient _client;
    private readonly BugsnagExporterOptions _options;
    private readonly IHostEnvironment? _environment;
    private readonly DateTimeOffset _appStartTime;

    private readonly Stack<NotifyEventException> _exceptions = new();

    private string[]? _projectNamespacePrefixes;
    private NotifyEventApp? _notifyEventApp;
    private NotifyEventDevice? _notifyEventDevice;
    private NotifyRequest? _notifyRequest;
    private Dictionary<string, SessionTracker>? _sessions;
    private SessionsRequest? _sessionsRequest;

    public BugsnagExporter(BugsnagClient client, IOptions<BugsnagExporterOptions> optionsAccessor)
        : this(client, optionsAccessor, null) { }

    public BugsnagExporter(BugsnagClient client, IOptions<BugsnagExporterOptions> optionsAccessor, IHostEnvironment? environment)
    {
        _client = client;
        _options = optionsAccessor.Value;
        _environment = environment;

        using (var process = Process.GetCurrentProcess())
            _appStartTime = process.StartTime.ToUniversalTime();
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        using var suppress = SuppressInstrumentationScope.Begin();

        var now = DateTimeOffset.Now;

        // initialize the requests
        _projectNamespacePrefixes ??= DetectProjectNamespaces();
        _notifyEventApp ??= DetectAppFromResource();
        _notifyEventApp.Duration = (int)(now - _appStartTime).TotalMilliseconds;
        _notifyEventDevice ??= DetectDeviceFromResource();
        _notifyEventDevice.Time = now;
        _notifyRequest ??= new(_client.Notifier);
        _sessions ??= new Dictionary<string, SessionTracker>();
        _sessionsRequest ??= new(_client.Notifier)
        {
            App = _notifyEventApp,
            Device = _notifyEventDevice,
            Sessions = new(),
        };

        try
        {
            foreach (var activity in batch)
            {
                var root = activity;
                while (root.Parent is not null)
                    root = root.Parent;

                if (!_sessions.TryGetValue(activity.RootId!, out var sessionTracker))
                {
                    var session = new Session(activity.RootId!)
                    {
                        StartedAt = new DateTimeOffset(root.StartTimeUtc, TimeSpan.Zero),
                    };

                    var notifyEventSession = new NotifyEventSession(session.Id, session.StartedAt);

                    sessionTracker = new(session, notifyEventSession);
                    _sessions.Add(session.Id, sessionTracker);
                    _sessionsRequest.Sessions!.Add(session);
                }

                var activityEventEnumerator = activity.EnumerateEvents();

                while (activityEventEnumerator.MoveNext())
                {
                    var notifyEvent = new NotifyEvent()
                    {
                        App = _notifyEventApp,
                        Device = _notifyEventDevice,
                    };

                    notifyEvent.Context = activity.DisplayName;
                    notifyEvent.Session = sessionTracker.NotifyEventSession;

                    // mark the event as unhandled if the exception is logged on the local root activity
                    notifyEvent.Unhandled = activity.Parent is null || activity.HasRemoteParent;

                    // translate data from baggage and tags
                    TranslateTraceData(activity, notifyEvent, sessionTracker.Session);

                    // translate events to exceptions and breadcrumbs
                    do
                    {
                        var activityEvent = activityEventEnumerator.Current;
                        if (activityEvent.Name == "exception")
                        {
                            AddNotifyEventException(notifyEvent, activityEvent);

                            // multiple exceptions on the same activity are NOT necessarily related; report a separate event for each
                            _notifyRequest.Events.Add(notifyEvent);

                            // increment the session counts
                            if (notifyEvent.Unhandled)
                                sessionTracker.NotifyEventSession.Events.Unhandled++;
                            else
                                sessionTracker.NotifyEventSession.Events.Handled++;

                            // start the next event
                            break;
                        }
                        else
                        {
                            AddNotifyEventBreadcrumb(notifyEvent, activityEvent);
                        }
                    }
                    while (activityEventEnumerator.MoveNext());
                }
            }

            if (_sessionsRequest.Sessions!.Count > 0)
                _client.PostSessionsAsync(_sessionsRequest).GetAwaiter().GetResult();

            if (_notifyRequest.Events.Count > 0)
                _client.PostEventsAsync(_notifyRequest).GetAwaiter().GetResult();

            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
        finally
        {
            _exceptions.Clear();
            _notifyRequest.Events.Clear();
            _sessions.Clear();
            _sessionsRequest.Sessions?.Clear();
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
        string? stacktrace = null;

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
                    stacktrace = valueString;
                    break;
            }
        }

        AddNotifyEventExceptionStacktrace(notifyEvent, activityEvent, errorClass, message, stacktrace);
    }

    private void AddNotifyEventExceptionStacktrace(NotifyEvent notifyEvent, ActivityEvent activityEvent, string? errorClass, string? message, string? stacktrace)
    {
        var stacktraceLines = stacktrace?.Split(StacktraceSeparators, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        var frames = false;
        int i;

        var notifyEventException = new NotifyEventException(
            errorClass ?? nameof(Exception),
            message,
            type: BugsnagStacktraceType.csharp);

        // if there is no stacktrace, create an exception from the error class and message parsed from tags
        if (stacktraceLines.Length == 0)
        {
            notifyEvent.Exceptions.Add(notifyEventException);
            return;
        }

        for (i = 0; i < stacktraceLines.Length; i++)
        {
            if (!frames)
            {
                var match = StacktraceMessageRegex.Match(stacktraceLines[i]);
                if (match.Success && stacktraceLines[i].StartsWith(StacktraceLineInnerExceptionPrefix))
                {
                    _exceptions.Push(notifyEventException);
                    notifyEventException = new(
                        match.Groups["error"].Value, // includes type name and optional system error code
                        match.Groups["message"].Value,
                        type: BugsnagStacktraceType.csharp);
                    continue;
                }
                else if (match.Success && i == 0)
                {
                    // allow values from stacktrace to replace the tag-parsed values
                    notifyEventException.ErrorClass = match.Groups["error"].Value;
                    notifyEventException.Message = match.Groups["message"].Value;
                    continue;
                }

                // if the pattern did not match, fall through to check whether this is a stackframe
            }

            // on an "end of inner exception" line, output the current exception and pop the stack
            if (stacktraceLines[i] == StacktraceLineInnerExceptionSeparator)
            {
                // extraneous separator without an outer exception; ignore it so we don't lose any subsequent frames
                if (_exceptions.Count == 0)
                    continue;

                notifyEvent.Exceptions.Insert(0, notifyEventException);
                notifyEventException = _exceptions.Pop();
                continue;
            }

            var stackframeMatch = StacktraceFrameRegex.Match(stacktraceLines[i]);
            if (stackframeMatch.Success)
            {
                frames = true;

                var method = stackframeMatch.Groups["method"];
                var file = stackframeMatch.Groups["file"];
                var line = stackframeMatch.Groups["line"];

                var stacktraceLine = new NotifyEventStacktrace(
                    file.Value,
                    line.Success ? int.Parse(line.Value) : 0,
                    method.Value);

                if (IsInProject(activityEvent, file.Value, method.Value))
                    stacktraceLine.InProject = true;

                notifyEventException.Stacktrace.Add(stacktraceLine);
            }
            else if (!frames)
            {
                // if the line is not a stack frame and we haven't seen any stack frames yet, add it to the current message

                if (String.IsNullOrWhiteSpace(notifyEventException.Message))
                    notifyEventException.Message = stacktraceLines[i];
                else
                    notifyEventException.Message += '\n' + stacktraceLines[i];
            }
        }

        // insert the current exception and any exceptions remaining on the stack
        do
        {
            notifyEvent.Exceptions.Insert(0, notifyEventException);
        }
        while (_exceptions.TryPop(out notifyEventException));
    }

    private NotifyEventApp DetectAppFromResource()
    {
        var app = _client.CreateDefaultApp();

        // use hosting environment as release stage if not explicitly set
        app.ReleaseStage ??= _environment?.EnvironmentName;

        var resource = this.ParentProvider?.GetResource()
            ?? ResourceBuilder.CreateDefault().Build();

        // use configured resource as authoritative to align with telemetry exported to other providers
        foreach (var (key, value) in resource.Attributes)
        {
            if (value is not string valueString)
                continue;

            switch (key)
            {
                case ResourceSemanticConventions.AttributeDeploymentEnvironment:
                    app.ReleaseStage = valueString;
                    break;
                case ResourceSemanticConventions.AttributeServiceName:
                    app.Id = valueString;
                    break;
                case ResourceSemanticConventions.AttributeServiceVersion:
                    app.Version = valueString;
                    break;
            }
        }

        // bugsnag's defaults use lower case for release stage
        app.ReleaseStage = app.ReleaseStage?.ToLowerInvariant();

        return app;
    }

    private NotifyEventDevice DetectDeviceFromResource()
    {
        var device = _client.CreateDefaultDevice();

        var resource = this.ParentProvider?.GetResource()
            ?? ResourceBuilder.CreateDefault().Build();

        // use configured resource as authoritative to align with telemetry exported to other providers
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

    private string[] DetectProjectNamespaces()
    {
        var projectNamespaces = _options.ProjectNamespaces;

        if (projectNamespaces is null && Assembly.GetEntryAssembly()?.GetName()?.Name is string entryAssemblyName)
            projectNamespaces = new[] { entryAssemblyName };

        if (projectNamespaces is null)
            projectNamespaces = Array.Empty<string>();

        return projectNamespaces.Select(x => x + ".").ToArray();
    }

    private bool IsInProject(ActivityEvent activityEvent, string file, string method)
    {
        var inProject = false;

        for (int i = 0; i < _projectNamespacePrefixes!.Length; ++i)
        {
            if (method.StartsWith(_projectNamespacePrefixes[i]))
            {
                inProject = true;
                break;
            }
        }

        return _options.InProjectCallback(activityEvent, file, method, inProject);
    }

    private const string StacktraceLineInnerExceptionPrefix = " ---> ";
    private const string StacktraceLineInnerExceptionSeparator = "   --- End of inner exception stack trace ---";
    private const string StacktraceRegexFramePattern = @"^\s+?at (?<method>.+?)( in (?<file>.+?):line (?<line>\d+))?\s*$";
    private const string StacktraceRegexMessagePattern = $@"^({StacktraceLineInnerExceptionPrefix})?(?<error>(?<type>.*?)(\((?<code>.*?)\))?): (?<message>.*)$";

    private readonly char[] StacktraceSeparators = new[] { '\r', '\n' };

#if NET7_0_OR_GREATER
    [GeneratedRegex(StacktraceRegexFramePattern, RegexOptions.ExplicitCapture)]
    private static partial Regex GetStacktraceFrameRegex();

    [GeneratedRegex(StacktraceRegexMessagePattern, RegexOptions.ExplicitCapture)]
    private static partial Regex GetStacktraceMessageRegex();
#else
    private static Regex GetStacktraceFrameRegex()
        => new Regex(StacktraceRegexFramePattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private static Regex GetStacktraceMessageRegex()
        => new Regex(StacktraceRegexMessagePattern, RegexOptions.Compiled | RegexOptions.ExplicitCapture);
#endif

    private readonly struct SessionTracker
    {
        public readonly Session Session;
        public readonly NotifyEventSession NotifyEventSession;

        public SessionTracker(Session session, NotifyEventSession notifyEventSession)
        {
            Session = session;
            NotifyEventSession = notifyEventSession;
        }
    }
}
