using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace XO.Diagnostics.Bugsnag.Models;

public sealed class NotifyRequest
{
    public NotifyRequest(BugsnagNotifier notifier)
    {
        Notifier = notifier;
        Events = new();
    }

    public string PayloadVersion { get; } = "5";
    public BugsnagNotifier Notifier { get; set; }
    public List<NotifyEvent> Events { get; set; }
}

public sealed class NotifyEvent
{
    public NotifyEvent()
        : this(new()) { }

    [JsonConstructor]
    public NotifyEvent(
        List<NotifyEventException> exceptions,
        List<NotifyEventBreadcrumb>? breadcrumbs = null,
        List<NotifyEventThread>? threads = null)
    {
        Exceptions = exceptions;
        Breadcrumbs = breadcrumbs ?? new(0);
        Threads = threads ?? new(0);
    }

    public List<NotifyEventException> Exceptions { get; set; }
    public List<NotifyEventBreadcrumb>? Breadcrumbs { get; set; }
    public NotifyEventRequest? Request { get; set; }
    public List<NotifyEventThread>? Threads { get; set; }
    public string? Context { get; set; }
    public string? GroupingHash { get; set; }
    public bool Unhandled { get; set; }
    public BugsnagSeverity Severity { get; set; }
    public NotifyEventSeverityReason? SeverityReason { get; set; }
    public List<string>? ProjectPackages { get; set; }
    public BugsnagUser? User { get; set; }
    public NotifyEventApp? App { get; set; }
    public NotifyEventDevice? Device { get; set; }
    public NotifyEventSession? Session { get; set; }
    public List<NotifyEventFeatureFlag>? FeatureFlags { get; set; }
    public Dictionary<string, JsonObject>? MetaData { get; set; }
}

public sealed class NotifyEventRequest
{
    public string? ClientIp { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public string? HttpMethod { get; set; }
    public string? Url { get; set; }
    public string? Referer { get; set; }
}

public sealed class NotifyEventSeverityReason
{
    public NotifyEventSeverityReason(BugsnagSeverityReason type)
    {
        Type = type;
    }

    public BugsnagSeverityReason Type { get; set; }
    public NotifyEventSeverityReasonAttributes? Attributes { get; set; }
    public bool UnhandledOverridden { get; set; }
}

public sealed class NotifyEventSeverityReasonAttributes
{
    public string? ErrorType { get; set; }
    public string? Level { get; set; }
    public string? SignalType { get; set; }
    public string? ViolationType { get; set; }
    public string? ErrorClass { get; set; }
    public string? Framework { get; set; }
    public string? ExceptionClass { get; set; }
}

public sealed class NotifyEventApp : SessionApp
{
    public string? Id { get; set; }
    public string? BuildUUID { get; set; }
    public string[]? DsymUUIDs { get; set; }
    public int? Duration { get; set; }
    public int? DurationInForeground { get; set; }
    public bool? InForeground { get; set; }
    public bool? IsLaunching { get; set; }
    public BugsnagBinaryArch? BinaryArch { get; set; }
    public bool? RunningOnRosetta { get; set; }
}

public sealed class NotifyEventDevice : SessionDevice
{
    public int? FreeMemory { get; set; }
    public int? TotalMemory { get; set; }
    public long? FreeDisk { get; set; }
    public string? BrowserName { get; set; }
    public string? BrowserVersion { get; set; }
    public string? Orientation { get; set; }
    public DateTimeOffset Time { get; set; }
    public string[]? CpuAbi { get; set; }
    public string? MacCatalystiOSVersion { get; set; }
}

public sealed class NotifyEventSession
{
    public NotifyEventSession(string id, DateTimeOffset startedAt, NotifyEventSessionEvents? events = null)
    {
        Id = id;
        StartedAt = startedAt;
        Events = events ?? new();
    }

    public string Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public NotifyEventSessionEvents Events { get; }
}

public sealed class NotifyEventSessionEvents
{
    public int Handled { get; set; }
    public int Unhandled { get; set; }
}

public class NotifyEventException
{
    public NotifyEventException(
        string errorClass,
        string? message = null,
        List<NotifyEventStacktrace>? stacktrace = null,
        BugsnagStacktraceType? type = null)
    {
        ErrorClass = errorClass;
        Message = message;
        Stacktrace = stacktrace ?? new(0);
        Type = type;
    }

    public string ErrorClass { get; set; }
    public string? Message { get; set; }
    public List<NotifyEventStacktrace> Stacktrace { get; set; }
    public BugsnagStacktraceType? Type { get; set; }
}

public sealed class NotifyEventBreadcrumb
{
    public NotifyEventBreadcrumb(DateTimeOffset timestamp, string name, NotifyEventBreadcrumbType type)
    {
        Timestamp = timestamp;
        Name = name;
        Type = type;
    }

    public DateTimeOffset Timestamp { get; set; }
    public string Name { get; set; }
    public NotifyEventBreadcrumbType Type { get; set; }
    public Dictionary<string, string>? MetaData { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotifyEventBreadcrumbType
{
    navigation,
    request,
    process,
    log,
    user,
    state,
    error,
    manual,
}

public class NotifyEventThread
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public bool ErrorReportingThread { get; set; }
    public List<NotifyEventStacktrace>? Stacktrace { get; set; }
    public string? State { get; set; }
    public BugsnagStacktraceType? Type { get; set; }
}

public class NotifyEventStacktrace
{
    public NotifyEventStacktrace(string file, int lineNumber, string method)
    {
        File = file;
        LineNumber = lineNumber;
        Method = method;
    }

    public string File { get; set; }
    public int LineNumber { get; set; }
    public int? ColumnNumber { get; set; }
    public string Method { get; set; }
    public bool? InProject { get; set; }
    public Dictionary<int, string>? Code { get; set; }
    public string? FrameAddress { get; set; }
    public string? LoadAddress { get; set; }
    public bool? IsLR { get; set; }
    public bool? IsPC { get; set; }
    public string? SymbolAddress { get; set; }
    public string? MachoFile { get; set; }
    public string? MachoLoadAddress { get; set; }
    public string? MachoUUID { get; set; }
    public string? MachoVMAddress { get; set; }
    public string? CodeIdentifier { get; set; }
}

public sealed class NotifyEventFeatureFlag
{
    public NotifyEventFeatureFlag(string featureFlag)
    {
        FeatureFlag = featureFlag;
    }

    public string FeatureFlag { get; set; }
    public string? Variant { get; set; }
}

