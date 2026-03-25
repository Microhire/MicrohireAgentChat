namespace MicrohireAgentChat;

/// <summary>
/// Optional NDJSON-style debug files for local Development only (never Azure paths or repo-relative paths).
/// </summary>
internal static class DevelopmentDebugLog
{
    private static bool IsDevelopment =>
        string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);

    private static string DebugDir =>
        Path.Combine(Path.GetTempPath(), "MicrohireAgentChat-debug");

    /// <summary>Appends one line to &lt;TEMP&gt;/MicrohireAgentChat-debug/{fileName} when ASPNETCORE_ENVIRONMENT=Development.</summary>
    public static void TryAppendLine(string fileName, string line)
    {
        if (!IsDevelopment || string.IsNullOrWhiteSpace(fileName)) return;
        try
        {
            Directory.CreateDirectory(DebugDir);
            File.AppendAllText(Path.Combine(DebugDir, fileName), line + Environment.NewLine);
        }
        catch
        {
            /* never throw from debug instrumentation */
        }
    }
}
