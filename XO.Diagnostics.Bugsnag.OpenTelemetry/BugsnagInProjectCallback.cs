using System.Diagnostics;

namespace XO.Diagnostics.Bugsnag.OpenTelemetry;

public delegate bool BugsnagInProjectCallback(
    ActivityEvent activity,
    string file,
    string method,
    bool inProjectNamespace);
