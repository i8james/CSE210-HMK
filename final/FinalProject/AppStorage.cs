using System;
using System.IO;
using System.Text.Json;

#nullable enable

internal static class FizbanStorage
{
    public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

    public static string GetPath(string fileName)
    {
        return Path.Combine(BaseDirectory, fileName);
    }
}

internal sealed class FizbanAppSettings
{
    public string CommanderText { get; set; } = string.Empty;
    public string DeckText { get; set; } = string.Empty;
    public string GamesSelection { get; set; } = "100k";
    public string ArchetypeSelection { get; set; } = "Auto Detect";
    public string EdhrecThemeSelection { get; set; } = "Auto Theme";
    public bool OnDraw { get; set; }
    public bool Cedh { get; set; }
}

internal static class FizbanSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
    private static readonly string FilePath = FizbanStorage.GetPath("fizban_settings.json");

    public static FizbanAppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new FizbanAppSettings();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<FizbanAppSettings>(json, JsonOptions) ?? new FizbanAppSettings();
        }
        catch
        {
            return new FizbanAppSettings();
        }
    }

    public static void Save(FizbanAppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}