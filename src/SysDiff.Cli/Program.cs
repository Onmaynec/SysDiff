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
        bool databaseExisted = File.Exists(paths.DatabasePath);

        string[] cleanArgs;
        IReadOnlyList<ISnapshotProvider> pluginProviders;
        try
        {
            (string[] arguments, string[] pluginPaths) =
                PluginProviderLoader.ExtractArguments(args);
            cleanArgs = arguments;
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

        bool interactiveLaunch = cleanArgs.Length == 0 && TerminalCapabilities.IsInteractive;
        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        });
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(interactiveLaunch ? LogLevel.Error : LogLevel.Information);
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
        services.AddSingleton<ScaleLabService>();
        services.AddSingleton<DriftRiskEngine>();
        services.AddSingleton<ConsoleReportRenderer>();
        services.AddSingleton<JsonReportRenderer>();
        services.AddSingleton<MarkdownReportRenderer>();
        services.AddSingleton<HtmlReportRenderer>();

        services.AddSingleton<SchemaContractService>();
        services.AddSingleton<PortableUpgradeService>();
        services.AddSingleton(_ => new DatabaseMigrationService(
            paths.DatabasePath,
            Path.Combine(paths.DataDirectory, "backups", "migrations")));
        services.AddSingleton<ISnapshotStore>(_ =>
            new SqliteSnapshotStore(paths.DatabasePath));
        services.AddSingleton<IInvestigationStore>(_ =>
            new SqliteInvestigationStore(paths.DatabasePath));
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
        services.AddSingleton<DriftOperationsService>();
        services.AddSingleton<UpdateSettingsStore>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<UpdateInstaller>();
        services.AddSingleton<TerminalRenderer>();
        services.AddSingleton<TerminalControlCenter>();
        services.AddSingleton<V3CommandRouter>();
        services.AddSingleton<V4CommandRouter>();
        services.AddSingleton<V6CommandRouter>();
        services.AddSingleton<V7CommandRouter>();
        services.AddSingleton<V8CommandRouter>();
        services.AddSingleton<V9CommandRouter>();
        services.AddSingleton<V10CommandRouter>();
        services.AddSingleton<V11CommandRouter>();
        services.AddSingleton<V12CommandRouter>();
        services.AddSingleton<CommandApp>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            DatabaseMigrationService migrationService =
                provider.GetRequiredService<DatabaseMigrationService>();
            await migrationService.ValidateReadableAsync(cancellation.Token);

            ISnapshotStore store = provider.GetRequiredService<ISnapshotStore>();
            await store.InitializeAsync(cancellation.Token);
            IInvestigationStore investigationStore = provider.GetRequiredService<IInvestigationStore>();
            await investigationStore.InitializeAsync(cancellation.Token);

            if (!databaseExisted)
            {
                DatabaseMigrationResult bootstrap = await migrationService
                    .BootstrapNewDatabaseAsync(cancellation.Token);
                if (!bootstrap.Success)
                {
                    throw new InvalidDataException(
                        $"Не удалось подготовить новую базу Migration Lab: {bootstrap.Message}");
                }
            }

            if (interactiveLaunch && !IsContinuousIntegration())
            {
                using var autoUpdateTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await provider.GetRequiredService<UpdateService>()
                        .TryAutoCheckAsync(autoUpdateTimeout.Token);
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                }
            }

            CommandApp fallback = provider.GetRequiredService<CommandApp>();
            return await provider.GetRequiredService<V12CommandRouter>()
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
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine($"Ошибка данных: {exception.Message}");
            return 9;
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

    private static bool IsContinuousIntegration() =>
        Environment.GetEnvironmentVariable("CI")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
}
