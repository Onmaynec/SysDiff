using SysDiff.Domain;

namespace SysDiff.Core;

public sealed class SeverityEngine : ISeverityEngine
{
    public (Severity Severity, string Explanation, string WhyThisMatters) Evaluate(
        ChangeType changeType,
        SystemArtifact? before,
        SystemArtifact? after,
        IReadOnlyCollection<PropertyChange> changedProperties)
    {
        SystemArtifact artifact = after ?? before
            ?? throw new InvalidOperationException("Отсутствует объект изменения.");

        string provider = artifact.ProviderId;
        string identity = artifact.Identity;
        string path = GetValue(after ?? before, "Path")
            ?? GetValue(after ?? before, "BinaryPath")
            ?? string.Empty;

        if (provider.Equals("startup", StringComparison.OrdinalIgnoreCase))
        {
            if (identity.Contains("winlogon", StringComparison.OrdinalIgnoreCase))
            {
                return (
                    Severity.Critical,
                    "Изменён чувствительный механизм Winlogon.",
                    "Winlogon запускается в системном контексте и напрямую влияет на вход пользователя.");
            }

            return (
                Severity.High,
                $"{Describe(changeType)} элемент автозагрузки.",
                "Автозагрузка позволяет программе запускаться при входе пользователя.");
        }

        if (provider.Equals("services", StringComparison.OrdinalIgnoreCase))
        {
            if (path.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase)
                || path.Contains(@"\AppData\", StringComparison.OrdinalIgnoreCase))
            {
                return (
                    Severity.Critical,
                    "Служба ссылается на исполняемый файл в пользовательской или временной директории.",
                    "Службы могут работать с повышенными правами до входа пользователя.");
            }

            bool sensitiveProperty = changedProperties.Any(x =>
                x.Name.Equals("BinaryPath", StringComparison.OrdinalIgnoreCase)
                || x.Name.Equals("Account", StringComparison.OrdinalIgnoreCase)
                || x.Name.Equals("StartType", StringComparison.OrdinalIgnoreCase));

            if (changeType == ChangeType.Added || sensitiveProperty)
            {
                return (
                    Severity.High,
                    $"{Describe(changeType)} служба Windows.",
                    "Службы могут запускаться автоматически и работать с повышенными правами.");
            }

            return (
                Severity.Info,
                "Изменилось текущее состояние службы.",
                "Смена состояния может быть обычным результатом запуска или остановки приложения.");
        }

        if (provider.Equals("scheduled-tasks", StringComparison.OrdinalIgnoreCase))
        {
            return (
                Severity.High,
                $"{Describe(changeType)} задача планировщика.",
                "Задачи могут автоматически запускать программы по расписанию, при входе или старте системы.");
        }

        if (provider.Equals("registry", StringComparison.OrdinalIgnoreCase))
        {
            return (
                Severity.Low,
                $"{Describe(changeType)} значение реестра.",
                "Реестр хранит настройки Windows и приложений; важность зависит от конкретного раздела.");
        }

        if (provider.Equals("environment", StringComparison.OrdinalIgnoreCase))
        {
            Severity severity = identity.Contains("/path/", StringComparison.OrdinalIgnoreCase)
                ? Severity.Medium
                : Severity.Low;

            return (
                severity,
                $"{Describe(changeType)} переменная окружения.",
                "Изменения PATH и других переменных могут влиять на поиск программ и поведение процессов.");
        }

        if (provider.Equals("filesystem", StringComparison.OrdinalIgnoreCase))
        {
            string extension = Path.GetExtension(path);
            if (changeType == ChangeType.Added
                && extension is ".exe" or ".dll" or ".sys" or ".ps1" or ".bat" or ".cmd")
            {
                return (
                    Severity.Medium,
                    "Создан новый исполняемый или сценарный файл.",
                    "Новые исполняемые файлы могут добавлять функции, службы или механизмы запуска.");
            }

            return (
                Severity.Low,
                $"{Describe(changeType)} объект файловой системы.",
                "Файловые изменения обычно ожидаемы при установке, но требуют контекста.");
        }

        return (
            Severity.Info,
            $"{Describe(changeType)} системный объект.",
            "Изменение сохранено для анализа и не означает вредоносность.");
    }

    private static string Describe(ChangeType changeType) => changeType switch
    {
        ChangeType.Added => "Добавлен",
        ChangeType.Removed => "Удалён",
        ChangeType.Modified => "Изменён",
        _ => "Обнаружен"
    };

    private static string? GetValue(SystemArtifact? artifact, string name) =>
        artifact?.Properties.TryGetValue(name, out ArtifactValue? value) == true
            ? value.Value
            : null;
}
