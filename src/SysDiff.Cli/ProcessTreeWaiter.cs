using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SysDiff.Cli;

internal static class ProcessTreeWaiter
{
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly nint InvalidHandleValue = new(-1);

    public static async Task<ProcessTreeWaitResult> WaitAsync(
        Process root,
        bool waitForChildren,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var knownProcessIds = new HashSet<int> { root.Id };
        int? exitCode = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (waitForChildren)
            {
                IReadOnlyDictionary<int, int> parents = EnumerateParentProcessIds();
                bool added;
                do
                {
                    added = false;
                    foreach ((int processId, int parentId) in parents)
                    {
                        if (knownProcessIds.Contains(parentId) && knownProcessIds.Add(processId))
                        {
                            added = true;
                        }
                    }
                }
                while (added);
            }

            bool anyRunning = false;
            foreach (int processId in knownProcessIds.ToArray())
            {
                try
                {
                    using Process process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                    {
                        anyRunning = true;
                    }
                    else if (processId == root.Id)
                    {
                        exitCode = process.ExitCode;
                    }
                }
                catch (ArgumentException)
                {
                    if (processId == root.Id && root.HasExited)
                    {
                        exitCode = root.ExitCode;
                    }
                }
                catch (InvalidOperationException)
                {
                }
                catch (Win32Exception)
                {
                    anyRunning = true;
                }
            }

            if (!anyRunning)
            {
                return new ProcessTreeWaitResult(
                    TimedOut: false,
                    ExitCode: exitCode,
                    ObservedProcesses: knownProcessIds.Count,
                    Duration: DateTimeOffset.UtcNow - started);
            }

            if (timeout is not null && DateTimeOffset.UtcNow - started >= timeout.Value)
            {
                return new ProcessTreeWaitResult(
                    TimedOut: true,
                    ExitCode: exitCode,
                    ObservedProcesses: knownProcessIds.Count,
                    Duration: DateTimeOffset.UtcNow - started);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
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
                Size = (uint)Marshal.SizeOf<ProcessEntry32>()
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

internal sealed record ProcessTreeWaitResult(
    bool TimedOut,
    int? ExitCode,
    int ObservedProcesses,
    TimeSpan Duration);
