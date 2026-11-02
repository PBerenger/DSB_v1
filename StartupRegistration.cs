using Microsoft.Win32;

namespace DoubleSceensBlack;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DoubleSceensBlack";

    public static bool IsEnabled()
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        string? registeredCommand = runKey?.GetValue(ValueName) as string;
        string? executablePath = Environment.ProcessPath;
        return registeredCommand is not null
            && executablePath is not null
            && string.Equals(registeredCommand, Quote(executablePath), StringComparison.OrdinalIgnoreCase);
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

        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Le chemin de l'application est introuvable.");
        runKey.SetValue(ValueName, Quote(executablePath), RegistryValueKind.String);
    }

    private static string Quote(string value) => $"\"{value}\"";
}
