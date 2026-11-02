using System.IO;
using System.Text.Json;

namespace DoubleSceensBlack;

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string LegacySettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DoubleScreenBlack",
        "settings.json");
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DoubleSceensBlack",
        "settings.json");

    public static AppSettings Load()
    {
        string settingsPath = File.Exists(SettingsPath) ? SettingsPath : LegacySettingsPath;
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        string json = File.ReadAllText(settingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json)
            ?? throw new JsonException("Le fichier de paramètres est vide.");
    }

    public static void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (directory is null)
        {
            throw new InvalidOperationException("Le dossier des paramètres est introuvable.");
        }

        Directory.CreateDirectory(directory);
        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
