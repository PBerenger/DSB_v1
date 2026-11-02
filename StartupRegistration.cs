using Microsoft.Win32;

namespace DoubleSceensBlack;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DoubleSceensBlack";
    private const string StartupArgument = "--startup";

    public static bool IsEnabled()
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        string? registeredCommand = runKey?.GetValue(ValueName) as string;
        string? executablePath = Environment.ProcessPath;
        if (registeredCommand is null || executablePath is null)
        {
            return false;
        }

        string legacyCommand = Quote(executablePath);
        return string.Equals(registeredCommand, legacyCommand, StringComparison.OrdinalIgnoreCase)
            || string.Equals(registeredCommand, BuildCommand(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("La clé de démarrage Windows n'a pas pu être créée.");

        if (!enabled)
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        runKey.SetValue(ValueName, BuildCommand(
            Environment.ProcessPath
                ?? throw new InvalidOperationException("Le chemin de l'application est introuvable.")),
            RegistryValueKind.String);
    }

    public static bool IsStartupLaunch() =>
        Environment.GetCommandLineArgs()
            .Skip(1)
            .Any(argument => string.Equals(argument, StartupArgument, StringComparison.OrdinalIgnoreCase));

    public static void UpgradeLegacyRegistration()
    {
        string? executablePath = Environment.ProcessPath;
        if (executablePath is null)
        {
            return;
        }

        string legacyCommand = Quote(executablePath);
        using RegistryKey? readKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (!string.Equals(
                readKey?.GetValue(ValueName) as string,
                legacyCommand,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using RegistryKey? writeKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (writeKey is null
            || !string.Equals(
                writeKey.GetValue(ValueName) as string,
                legacyCommand,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        writeKey.SetValue(ValueName, BuildCommand(executablePath), RegistryValueKind.String);
    }

    private static string BuildCommand(string executablePath) =>
        $"{Quote(executablePath)} {StartupArgument}";

    private static string Quote(string value) => $"\"{value}\"";
}
