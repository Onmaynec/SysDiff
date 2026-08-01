namespace SysDiff.Cli;

public sealed record AppPaths(
    string BaseDirectory,
    string DataDirectory,
    string DatabasePath,
    string ReportsDirectory,
    string LogsDirectory,
    bool Portable)
{
    public string UpdatesDirectory => Path.Combine(DataDirectory, "updates");

    public string UpdateSettingsPath => Path.Combine(DataDirectory, "update-settings.json");

    public string UpdateStatePath => Path.Combine(DataDirectory, "update-state.json");

    public static AppPaths Resolve()
    {
        string baseDirectory = AppContext.BaseDirectory;
        bool portable = File.Exists(Path.Combine(baseDirectory, "portable.mode"));

        string dataDirectory = portable
            ? Path.Combine(baseDirectory, "data")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SysDiff");

        string reports = portable
            ? Path.Combine(baseDirectory, "reports")
            : Path.Combine(dataDirectory, "reports");

        string logs = portable
            ? Path.Combine(baseDirectory, "logs")
            : Path.Combine(dataDirectory, "logs");

        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(reports);
        Directory.CreateDirectory(logs);
        Directory.CreateDirectory(Path.Combine(dataDirectory, "updates"));

        return new AppPaths(
            baseDirectory,
            dataDirectory,
            Path.Combine(dataDirectory, "sysdiff.db"),
            reports,
            logs,
            portable);
    }
}
