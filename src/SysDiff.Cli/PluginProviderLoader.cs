using System.Reflection;
using System.Runtime.Loader;
using SysDiff.Domain;
using SysDiff.ProviderSdk;

namespace SysDiff.Cli;

internal static class PluginProviderLoader
{
    public static IReadOnlyList<ISnapshotProvider> Load(IEnumerable<string> paths)
    {
        var result = new List<ISnapshotProvider>();
        foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Файл плагина не найден.", fullPath);
            }

            Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            SysDiffProviderPluginAttribute? metadata =
                assembly.GetCustomAttribute<SysDiffProviderPluginAttribute>();
            if (metadata is null)
            {
                throw new InvalidDataException(
                    $"Assembly {fullPath} не содержит SysDiffProviderPluginAttribute.");
            }

            if (!string.Equals(
                metadata.SdkVersion,
                ProviderSdkInfo.CurrentVersion,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Плагин {fullPath} использует SDK {metadata.SdkVersion}; требуется {ProviderSdkInfo.CurrentVersion}.");
            }

            Type[] providerTypes = assembly.GetTypes()
                .Where(type =>
                    !type.IsAbstract
                    && !type.IsInterface
                    && typeof(ISnapshotProvider).IsAssignableFrom(type)
                    && type.GetConstructor(Type.EmptyTypes) is not null)
                .ToArray();

            if (providerTypes.Length == 0)
            {
                throw new InvalidDataException(
                    $"В assembly {fullPath} не найден публичный ISnapshotProvider с конструктором без параметров.");
            }

            foreach (Type type in providerTypes)
            {
                result.Add((ISnapshotProvider)Activator.CreateInstance(type)!);
            }
        }

        return result;
    }

    public static (string[] Arguments, string[] PluginPaths) ExtractArguments(string[] args)
    {
        var remaining = new List<string>();
        var pluginPaths = new List<string>();

        for (int index = 0; index < args.Length; index++)
        {
            string current = args[index];
            if (current.Equals("--plugin", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("После --plugin требуется путь к DLL.");
                }

                pluginPaths.Add(args[++index]);
                continue;
            }

            if (current.StartsWith("--plugin=", StringComparison.OrdinalIgnoreCase))
            {
                pluginPaths.Add(current[(current.IndexOf('=') + 1)..]);
                continue;
            }

            remaining.Add(current);
        }

        return ([.. remaining], [.. pluginPaths]);
    }
}
