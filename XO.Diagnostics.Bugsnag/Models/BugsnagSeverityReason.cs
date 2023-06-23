using System.Text.Json.Serialization;

namespace XO.Diagnostics.Bugsnag.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BugsnagSeverityReason
{
    /// <summary>
    /// Whenever an uncaught exception is discovered (generic).
    /// </summary>
    unhandledException,

    /// <summary>
    /// When an error is discovered (PHP).
    /// </summary>
    unhandledError,

    /// <summary>
    /// Whenever a log message is sent (generic).
    /// </summary>
    log,

    /// <summary>
    /// Whenever a "fatal" signal is discovered (iOS).
    /// </summary>
    signal,

    /// <summary>
    /// Whenever a strictMode issue is discovered (Android).
    /// </summary>
    strictMode,

    /// <summary>
    /// Whenever an unhandled promise rejection is discovered (JS/Node JS/React Native).
    /// </summary>
    unhandledPromiseRejection,

    /// <summary>
    /// callbackErrorIntercept (Node JS).
    /// </summary>
    callbackErrorIntercept,

    /// <summary>
    /// Whenever an exception with a particular class is automatically sent (Ruby).
    /// </summary>
    errorClass,

    /// <summary>
    /// Whenever an exception with a particular class is automatically sent (Java).
    /// </summary>
    exceptionClass,

    /// <summary>
    /// When a panic is unhandled and crashes the app (Go).
    /// </summary>
    unhandledPanic,

    /// <summary>
    /// Unhandled errors in ${Negroni} middleware have a default severity:error.
    /// </summary>
    unhandledErrorMiddleware,

    /// <summary>
    /// Unhandled exceptions in ${Rack} middleware have a default severity:error.
    /// </summary>
    unhandledExceptionMiddleware,

    /// <summary>
    /// Whenever a callback changes a report's severity (generic).
    /// </summary>
    userCallbackSetSeverity,

    /// <summary>
    /// Whenever a severity is set through a manual notify call (generic).
    /// </summary>
    userSpecifiedSeverity,

    /// <summary>
    /// Whenever a handled exception is sent through (generic).
    /// </summary>
    handledException,

    /// <summary>
    /// Whenever a handled error is sent through (PHP).
    /// </summary>
    handledError,

    /// <summary>
    /// Whenever a panic is handled through AutoNotify or Recover (Go).
    /// </summary>
    handledPanic,

    /// <summary>
    /// When a user creates a context which changes the severity (Python).
    /// </summary>
    userContextSetSeverity,

    /// <summary>
    /// Whenever an ANR is detected (Android).
    /// </summary>
    anrError,

    /// <summary>
    /// Whenever an app hang is detected (Cocoa).
    /// </summary>
    appHang,

    /// <summary>
    /// When an app is terminated because it used too much memory (Cocoa).
    /// </summary>
    outOfMemory,

    /// <summary>
    /// When an app is terminated because the device is overheating (Cocoa).
    /// </summary>
    thermalKill,
}
