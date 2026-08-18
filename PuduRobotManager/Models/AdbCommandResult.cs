namespace PuduRobotManager.Models;

public sealed class AdbCommandResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public int ExitCode { get; init; }

    public string Combined
    {
        get
        {
            var output = Output.Trim();
            var error = Error.Trim();
            if (string.IsNullOrEmpty(error))
            {
                return output;
            }

            if (string.IsNullOrEmpty(output))
            {
                return error;
            }

            return $"{output}{Environment.NewLine}{error}";
        }
    }
}
