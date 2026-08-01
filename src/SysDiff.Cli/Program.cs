using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SysDiff.Core;
using SysDiff.Domain;
using SysDiff.Providers;
using SysDiff.Reporting;
using SysDiff.Storage;

namespace SysDiff.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        AppPaths paths = AppPaths.Resolve();

        string[] cleanArgs;
        IReadOnlyList<ISnapshotProvider> pluginProviders;
        try
        {
            (cleanArgs, string[] pluginPaths) = PluginProviderLoader.ExtractArguments(args);
            pluginProviders = PluginProviderLoader.Load(pluginPaths);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or FileNotFoundException
            or InvalidDataException
            or BadImageFormatException)
        {
            Console.Error.WriteLine($"Ошибка плагина: {exception.Message}");
            return 2;
        }

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });

        services.AddSingleton<ProfileCatalog>();
        services.AddSingleton<ProfileLoader>();
        services.AddSingleton<PrivacyRedactor>();
        services.AddSingleton<ISeverityEngine, SeverityEngine>();
        services.AddSingleton<INoiseFilterEngine, NoiseFilterEngine>();
        services.AddSingleton<ComparisonEngine>();
        services.AddSingleton<ConsoleReportRenderer>();
        services.AddSingleton<JsonReportRenderer>();
        services.AddSingleton<MarkdownReportRenderer>();
        services.AddSingleton<HtmlReportRenderer>();

        services.AddSingleton<ISnapshotStore>(_ =>
            new SqliteSnapshotStore(paths.DatabasePath));
        services.AddSingleton<SnapshotArchiveService>();

        services.AddSingleton<ISnapshotProvider, FileSystemProvider>();
        services.AddSingleton<ISnapshotProvider, RegistryProvider>();
        services.AddSingleton<ISnapshotProvider, ServicesProvider>();
        services.AddSingleton<ISnapshotProvider, ScheduledTasksProvider>();
        services.AddSingleton<ISnapshotProvider, StartupProvider>();
        services.AddSingleton<ISnapshotProvider, EnvironmentProvider>();
        services.AddSingleton<ISnapshotProvider, FirewallProvider>();
        services.AddSingleton<ISnapshotProvider, InstalledAppsProvider>();
        services.AddSingleton<ISnapshotProvider, DriversProvider>();
        services.AddSingleton<ISnapshotProvider, CertificatesProvider>();
        services.AddSingleton<ISnapshotProvider, NetworkConfigurationProvider>();
        foreach (ISnapshotProvider pluginProvider in pluginProviders)
        {
            services.AddSingleton(typeof(ISnapshotProvider), pluginProvider);
        }

        services.AddSingleton<SnapshotCoordinator>();
        services.AddSingleton<ProcessLiveMonitor>();
        services.AddSingleton<NetworkLiveMonitor>();
        services.AddSingleton<InvestigationBundleService>();
        services.AddSingleton<V3CommandRouter>();
        services.AddSingleton<CommandApp>();

        await using ServiceProvider provider = services.BuildServiceProvider();

        ISnapshotStore store = provider.GetRequiredService<ISnapshotStore>();
        await store.InitializeAsync(CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            CommandApp fallback = provider.GetRequiredService<CommandApp>();
            return await provider.GetRequiredService<V3CommandRouter>()
                .RunAsync(cleanArgs, fallback, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Операция отменена.");
            return 8;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Ошибка аргументов: {exception.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"Доступ запрещён: {exception.Message}");
            return 5;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Ошибка: {exception.Message}");
            return 1;
        }
    }
}
