using System.Runtime.InteropServices;
using System.Xml.Linq;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class ScheduledTasksProvider : ISnapshotProvider
{
    public string Id => "scheduled-tasks";

    public string DisplayName => "Задачи планировщика";

    public bool RequiresAdministrator => false;

    public Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new ProviderSnapshotResult
            {
                ProviderId = Id,
                DisplayName = DisplayName,
                Status = ProviderStatus.Skipped,
                StartedAtUtc = started,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Warnings = ["Провайдер доступен только в Windows."]
            });
        }

        object? serviceObject = null;
        object? rootObject = null;
        try
        {
            Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType is null)
            {
                throw new InvalidOperationException("COM API планировщика задач недоступен.");
            }

            serviceObject = Activator.CreateInstance(serviceType);
            dynamic service = serviceObject
                ?? throw new InvalidOperationException("Не удалось создать Schedule.Service.");

            service.Connect();
            rootObject = service.GetFolder("\\");
            ScanFolder((dynamic)rootObject, artifacts, warnings, context.Progress, cancellationToken);
        }
        catch (Exception exception) when (
            exception is COMException
            or InvalidOperationException
            or UnauthorizedAccessException)
        {
            warnings.Add(exception.Message);
        }
        finally
        {
            ReleaseCom(rootObject);
            ReleaseCom(serviceObject);
        }

        return Task.FromResult(new ProviderSnapshotResult
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            Status = warnings.Count > 0 ? ProviderStatus.Partial : ProviderStatus.Success,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            ArtifactCount = artifacts.Count,
            Artifacts = artifacts,
            Warnings = warnings
        });
    }

    private static void ScanFolder(
        dynamic folder,
        List<SystemArtifact> artifacts,
        List<string> warnings,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        object? taskCollection = null;
        object? folderCollection = null;

        try
        {
            taskCollection = folder.GetTasks(1);
            dynamic tasks = taskCollection;

            for (int index = 1; index <= tasks.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                object? taskObject = null;
                try
                {
                    taskObject = tasks.Item(index);
                    dynamic task = taskObject;
                    string path = Convert.ToString(task.Path, System.Globalization.CultureInfo.InvariantCulture)
                        ?? string.Empty;
                    string xml = Convert.ToString(task.Xml, System.Globalization.CultureInfo.InvariantCulture)
                        ?? string.Empty;

                    artifacts.Add(CreateArtifact(path, xml, task));
                    progress?.Report(new SnapshotProgress(
                        "scheduled-tasks",
                        "Сканирование задач планировщика",
                        artifacts.Count,
                        path));
                }
                catch (Exception exception) when (
                    exception is COMException
                    or InvalidOperationException)
                {
                    warnings.Add(exception.Message);
                }
                finally
                {
                    ReleaseCom(taskObject);
                }
            }

            folderCollection = folder.GetFolders(0);
            dynamic folders = folderCollection;

            for (int index = 1; index <= folders.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                object? childObject = null;
                try
                {
                    childObject = folders.Item(index);
                    ScanFolder(
                        (dynamic)childObject,
                        artifacts,
                        warnings,
                        progress,
                        cancellationToken);
                }
                catch (Exception exception) when (
                    exception is COMException
                    or InvalidOperationException)
                {
                    warnings.Add(exception.Message);
                }
                finally
                {
                    ReleaseCom(childObject);
                }
            }
        }
        finally
        {
            ReleaseCom(taskCollection);
            ReleaseCom(folderCollection);
        }
    }

    private static SystemArtifact CreateArtifact(string path, string xml, dynamic task)
    {
        XDocument? document = null;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (System.Xml.XmlException)
        {
            // XML сохраняется как данные; отсутствие разбора не ломает снимок.
        }

        string actions = document is null
            ? string.Empty
            : string.Join(
                " | ",
                document.Descendants()
                    .Where(x => x.Name.LocalName is "Exec" or "ComHandler")
                    .Select(x => string.Join(
                        ' ',
                        x.Descendants().Select(y => y.Value).Where(x => !string.IsNullOrWhiteSpace(x)))));

        string triggers = document is null
            ? string.Empty
            : string.Join(
                ", ",
                document.Descendants()
                    .Where(x => x.Name.LocalName.EndsWith("Trigger", StringComparison.Ordinal))
                    .Select(x => x.Name.LocalName)
                    .Distinct(StringComparer.OrdinalIgnoreCase));

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (triggers.Contains("LogonTrigger", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("Persistence");
            tags.Add("LogonTrigger");
        }

        if (triggers.Contains("BootTrigger", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("Persistence");
            tags.Add("BootTrigger");
        }

        return new SystemArtifact
        {
            ProviderId = "scheduled-tasks",
            ArtifactType = "ScheduledTask",
            Identity = $"task://{path.TrimStart('\\').Replace('\\', '/')}",
            DisplayName = path,
            Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["Path"] = ArtifactValue.From(path),
                ["Name"] = ArtifactValue.From(
                    Convert.ToString(task.Name, System.Globalization.CultureInfo.InvariantCulture)),
                ["State"] = ArtifactValue.From(
                    Convert.ToString(task.State, System.Globalization.CultureInfo.InvariantCulture)),
                ["Enabled"] = ArtifactValue.From(
                    Convert.ToString(task.Enabled, System.Globalization.CultureInfo.InvariantCulture)),
                ["LastRunTime"] = ArtifactValue.From(
                    Convert.ToString(task.LastRunTime, System.Globalization.CultureInfo.InvariantCulture)),
                ["NextRunTime"] = ArtifactValue.From(
                    Convert.ToString(task.NextRunTime, System.Globalization.CultureInfo.InvariantCulture)),
                ["Triggers"] = ArtifactValue.From(triggers),
                ["Actions"] = ArtifactValue.From(actions),
                ["Xml"] = ArtifactValue.From(xml)
            },
            Tags = tags
        };
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
