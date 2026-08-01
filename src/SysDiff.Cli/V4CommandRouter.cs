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
            Console.WriteLine("SysDiff 0.4.0");
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
            PrintV4Help();
        }

        return result;
    }

    private static void PrintV4Help()
    {
        Console.WriteLine(
            """

            TERMINAL CONTROL CENTER 0.4
              sysdiff                         открыть полноэкранную панель
              sysdiff --tui-smoke             вывести CI-preview панели и завершиться

            УПРАВЛЕНИЕ TUI
              ↑ / ↓                           навигация
              Enter                           открыть выбранный пункт
              Esc                             назад
              /                               поиск в Change Explorer
              F                               severity filter
              S                               сортировка
              R                               raw changes
              E                               экспорт
              F5                              обновить dashboard
              Q                               выход

            При перенаправленном stdin/stdout интерактивная панель не запускается.
            Обычные CLI-команды остаются доступными для PowerShell, CI и автоматизации.
            """);
    }
}
