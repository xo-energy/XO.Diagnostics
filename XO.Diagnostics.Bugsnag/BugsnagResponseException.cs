using System.Net;
using XO.Diagnostics.Bugsnag.Models;

namespace XO.Diagnostics.Bugsnag;

public class BugsnagRequestException : HttpRequestException
{
    public BugsnagRequestException(StatusResponse? response = null, Exception? inner = null, HttpStatusCode? statusCode = null)
        : base(response?.ToString() ?? $"Request failed with status code {statusCode}", inner, statusCode)
    {
        this.Response = response;
    }

    public StatusResponse? Response { get; }
}
