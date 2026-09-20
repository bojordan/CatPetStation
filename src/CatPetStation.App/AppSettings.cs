using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatPetStation.App;

/// <summary>
/// User settings, stored as plain JSON under %APPDATA%\CatPetStation.
/// Nothing here ever leaves the machine: CatPetStation makes no network calls.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Pack directory names of the pets on screen (duplicates = multiple copies).</summary>
    public List<string> ActivePets { get; set; } = [];

    /// <summary>Display scale applied to every pet (1.0 = the sprite's native size).</summary>
    public double Scale { get; set; } = 1.0;

    public bool AllAsleep { get; set; }

    /// <summary>Whether pets may sit on the top edges of other windows (read-only window tracking).</summary>
    public bool WindowLedges { get; set; } = true;

    [JsonIgnore]
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CatPetStation");

    [JsonIgnore]
    public static string PacksDirectory { get; } = Path.Combine(DataDirectory, "packs");

    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(SettingsPath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A corrupt settings file must never stop the app from starting.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
