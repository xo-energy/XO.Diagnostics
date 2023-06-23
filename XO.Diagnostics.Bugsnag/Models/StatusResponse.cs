namespace XO.Diagnostics.Bugsnag.Models;

public sealed class StatusResponse
{
    public StatusResponse(string status)
    {
        this.Status = status;
    }

    public string Status { get; set; }
    public string[] Errors { get; set; } = Array.Empty<string>();
    public string[] Warnings { get; set; } = Array.Empty<string>();

    public override string ToString()
    {
        if (Errors.Length > 0)
            return String.Join("; ", Errors);

        if (Warnings.Length > 0)
            return String.Join("; ", Warnings);

        return Status;
    }
}
