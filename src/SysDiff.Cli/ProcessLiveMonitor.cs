using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SysDiff.Core;
using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed class ProcessLiveMonitor
{
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly nint InvalidHandleValue = new(-1);
    private readonly PrivacyRedactor _redactor;

    public ProcessLiveMonitor(PrivacyRedactor redactor)
    {
        _redactor = redactor;
    }

    public async Task<IReadOnlyList<LiveEvent>> MonitorAsync(
        TimeSpan duration,
        int? rootProcessId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + duration;
        Dictionary<int, ProcessInfo> previous = Capture();
        var events = new List<LiveEvent>();

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            Dictionary<int, ProcessInfo> current = Capture();

            foreach ((int processId, ProcessInfo process) in current)
            {
                if (previous.ContainsKey(processId)
                    || !MatchesRoot(process, current, rootProcessId))
                {
                    continue;
                }

                events.Add(ToEvent("Started", process));
            }

            foreach ((int processId, ProcessInfo process) in previous)
            {
                if (current.ContainsKey(processId)
                    || !MatchesRoot(process, previous, rootProcessId))
                {
                    continue;
                }

                events.Add(ToEvent("Stopped", process));
            }

            previous = current;
            if (events.Count >= 100_000)
            {
                break;
            }
        }

        return events;
    }

    private LiveEvent ToEvent(string eventType, ProcessInfo process) => new()
    {
        TimestampUtc = DateTimeOffset.UtcNow,
        Category = "process",
        EventType = eventType,
        Identity = $"process://{process.ProcessId}",
        DisplayName = process.Name,
        Properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProcessId"] = process.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ParentProcessId"] = process.ParentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Path"] = process.Path is null ? null : _redactor.Redact(process.Path)
        }
    };

    private static bool MatchesRoot(
        ProcessInfo process,
        IReadOnlyDictionary<int, ProcessInfo> processes,
        int? rootProcessId)
    {
        if (rootProcessId is null)
        {
            return true;
        }

        int current = process.ProcessId;
        var visited = new HashSet<int>();
        while (visited.Add(current)
               && processes.TryGetValue(current, out ProcessInfo? item)
               && item is not null)
        {
            if (current == rootProcessId.Value)
            {
                return true;
            }

            current = item.ParentProcessId;
        }

        return false;
    }

    private static Dictionary<int, ProcessInfo> Capture()
    {
        IReadOnlyDictionary<int, int> parents = EnumerateParentProcessIds();
        var result = new Dictionary<int, ProcessInfo>();

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    int id = process.Id;
                    parents.TryGetValue(id, out int parentId);
                    string name = SafeRead(() => process.ProcessName) ?? $"PID {id}";
                    string? path = SafeRead(() => process.MainModule?.FileName);
                    result[id] = new ProcessInfo(id, parentId, name, path);
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException
                    or Win32Exception
                    or NotSupportedException)
                {
                }
            }
        }

        return result;
    }

    private static string? SafeRead(Func<string?> reader)
    {
        try
        {
            return reader();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<int, int> EnumerateParentProcessIds()
    {
        nint snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var result = new Dictionary<int, int>();
            var entry = new ProcessEntry32
            {
                Size = (uint)Marshal.SizeOf<ProcessEntry32>(),
                ExecutableFile = string.Empty
            };

            if (!Process32First(snapshot, ref entry))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 18)
                {
                    throw new Win32Exception(error);
                }

                return result;
            }

            do
            {
                result[unchecked((int)entry.ProcessId)] = unchecked((int)entry.ParentProcessId);
                entry.Size = (uint)Marshal.SizeOf<ProcessEntry32>();
            }
            while (Process32Next(snapshot, ref entry));

            return result;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private sealed record ProcessInfo(
        int ProcessId,
        int ParentProcessId,
        string Name,
        string? Path);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
