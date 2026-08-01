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

        if (provider.Equals("firewall", StringComparison.OrdinalIgnoreCase))
        {
            bool inbound = GetValue(artifact, "Direction")?.Equals(
                "Inbound",
                StringComparison.OrdinalIgnoreCase) == true;
            bool allow = GetValue(artifact, "Action")?.Equals(
                "Allow",
                StringComparison.OrdinalIgnoreCase) == true;
            bool enabled = GetValue(artifact, "Enabled")?.Equals(
                "True",
                StringComparison.OrdinalIgnoreCase) == true;

            Severity severity = inbound && allow && enabled
                ? Severity.High
                : Severity.Medium;

            return (
                severity,
                $"{Describe(changeType)} правило Windows Firewall.",
                inbound && allow
                    ? "Входящее разрешающее правило может открыть доступ к программе или службе из сети."
                    : "Правила Firewall изменяют доступ приложений и служб к сети.");
        }

        if (provider.Equals("drivers", StringComparison.OrdinalIgnoreCase))
        {
            bool unsigned = GetValue(artifact, "Signature")?.Equals(
                "MissingOrInvalid",
                StringComparison.OrdinalIgnoreCase) == true;
            bool persistenceChange = changeType is ChangeType.Added or ChangeType.Removed
                || changedProperties.Any(x =>
                    x.Name.Equals("BinaryPath", StringComparison.OrdinalIgnoreCase)
                    || x.Name.Equals("StartMode", StringComparison.OrdinalIgnoreCase)
                    || x.Name.Equals("Signature", StringComparison.OrdinalIgnoreCase));

            if (unsigned && persistenceChange)
            {
                return (
                    Severity.Critical,
                    "Добавлен или изменён драйвер без подтверждённой цифровой подписи.",
                    "Драйверы работают в ядре Windows; отсутствие подписи требует особенно внимательной проверки.");
            }

            return persistenceChange
                ? (
                    Severity.High,
                    $"{Describe(changeType)} системный драйвер.",
                    "Драйверы работают с высокими привилегиями и могут загружаться до входа пользователя.")
                : (
                    Severity.Info,
                    "Изменилось текущее состояние драйвера.",
                    "Состояние драйвера может меняться при обычной работе оборудования и Windows.");
        }

        if (provider.Equals("certificates", StringComparison.OrdinalIgnoreCase))
        {
            string storeName = GetValue(artifact, "StoreName") ?? string.Empty;
            bool trustStore = storeName.Equals("Root", StringComparison.OrdinalIgnoreCase)
                || storeName.Equals("AuthRoot", StringComparison.OrdinalIgnoreCase);

            return (
                trustStore ? Severity.High : Severity.Medium,
                $"{Describe(changeType)} сертификат в хранилище {storeName}.",
                trustStore
                    ? "Доверенный корневой сертификат может влиять на проверку подписей и защищённых соединений."
                    : "Сертификаты используются для идентификации, шифрования и проверки подписей.");
        }

        if (provider.Equals("installed-apps", StringComparison.OrdinalIgnoreCase))
        {
            return (
                changeType == ChangeType.Modified ? Severity.Low : Severity.Medium,
                $"{Describe(changeType)} запись установленного приложения.",
                "Запись показывает установку, удаление или обновление программы в пользовательской либо системной области.");
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
            string extension = Path.GetExtension(path).ToLowerInvariant();
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
