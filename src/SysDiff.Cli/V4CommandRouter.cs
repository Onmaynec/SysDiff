namespace SysDiff.Cli;

internal sealed class V4CommandRouter
{
    private readonly V3CommandRouter _v3;
    private readonly TerminalControlCenter _terminal;

    public V4CommandRouter(V3CommandRouter v3, TerminalControlCenter terminal)
    {
        _v3 = v3;
        _terminal = terminal;
    }

    public async Task<int> RunAsync(
        string[] args,
        CommandApp fallback,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await _terminal.RunAsync(cancellationToken);
        }

        if (args[0] is "--version" or "-v")
        {
            Console.WriteLine("SysDiff 0.5.0");
            return 0;
        }

        if (args[0].Equals("--tui-smoke", StringComparison.OrdinalIgnoreCase))
        {
            _terminal.PrintSmokeFrame();
            return 0;
        }

        int result = await _v3.RunAsync(args, fallback, cancellationToken);
        if (args[0] is "--help" or "-h" or "help")
        {
            PrintV5Help();
        }

        return result;
    }

    private static void PrintV5Help()
    {
        Console.WriteLine(
            """

            SYSDIFF CYBER CONSOLE 0.5
              sysdiff                         открыть Cyber Control Node
              sysdiff --tui-smoke             вывести CI-preview панели и завершиться

            COMMAND DECK
              1-9                             открыть модуль по номеру
              P / B / A                       Snapshot Node
              C                               Diff Lab
              W                               Watch Operations
              L                               Live Signal Monitor
              D                               Diagnostics

            УПРАВЛЕНИЕ TUI
              ↑ / ↓                           навигация
              Enter                           выполнить выбранное действие
              Esc                             назад
              /                               поиск в Change Explorer
              F                               severity filter
              S                               сортировка
              R                               raw changes
              E                               экспорт
              F5                              обновить Control Node
              Q                               выход

            БЕЗОПАСНЫЙ РЕЖИМ
              SYSDIFF_NO_ANIMATIONS=1         отключить boot/action animations
              NO_COLOR=1                      отключить цветовую палитру

            При перенаправленном stdin/stdout интерактивная панель не запускается.
            Обычные CLI-команды остаются доступными для PowerShell, CI и автоматизации.
            """);
    }
}
