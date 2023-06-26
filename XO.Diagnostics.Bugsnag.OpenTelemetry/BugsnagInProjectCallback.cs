using System.Diagnostics;

namespace XO.Diagnostics.Bugsnag.OpenTelemetry;

public delegate bool BugsnagInProjectCallback(
    ActivityEvent activityEvent,
    string file,
    string method,
    bool inProjectNamespace);
