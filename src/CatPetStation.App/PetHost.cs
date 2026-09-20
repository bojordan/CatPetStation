using System.IO;
using System.Windows;
using System.Windows.Threading;
using CatPetStation.Core;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace CatPetStation.App;

/// <summary>
/// Owns the pack library, the live pets, the shared animation clock, and the
/// persisted settings. One 30 Hz timer drives every pet; when all pets nap,
/// the clock slows right down so a sleeping cat costs almost nothing.
/// </summary>
public sealed class PetHost : IDisposable
{
    private static readonly TimeSpan ActiveInterval = TimeSpan.FromMilliseconds(33);
    private static readonly TimeSpan NapInterval = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer _clock;
    private readonly List<PetController> _pets = [];
    private DateTime _lastTick = DateTime.UtcNow;

    public AppSettings Settings { get; }

    /// <summary>Read-only packs shipped with the app.</summary>
    public string BuiltInPacksRoot { get; } =
        Path.Combine(AppContext.BaseDirectory, "assets", "pets");

    public PetHost()
    {
        Settings = AppSettings.Load();
        Directory.CreateDirectory(AppSettings.PacksDirectory);

        _clock = new DispatcherTimer(DispatcherPriority.Render) { Interval = ActiveInterval };
        _clock.Tick += OnTick;
        _clock.Start();
    }

    public IReadOnlyList<InstalledPack> AvailablePacks =>
        PackLibrary.Enumerate(BuiltInPacksRoot, AppSettings.PacksDirectory);

    public int ActivePetCount => _pets.Count;

    public event Action? PetsChanged;

    /// <summary>Brings back the pets from the last session, or one Station Cat on first run.</summary>
    public void RestoreOrSpawnDefault()
    {
        var packsByDir = AvailablePacks.ToDictionary(
            p => Path.GetFileName(p.Directory), p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var dirName in Settings.ActivePets)
            if (packsByDir.TryGetValue(dirName, out var pack))
                Spawn(pack, save: false);

        if (_pets.Count == 0 && packsByDir.TryGetValue("station-cat", out var cat))
            Spawn(cat, save: false);

        if (Settings.AllAsleep)
            foreach (var pet in _pets)
                pet.SetSleeping(true);

        SaveActivePets();
    }

    public void Spawn(InstalledPack pack, bool save = true)
    {
        try
        {
            _pets.Add(new PetController(pack, Settings.Scale, Remove));
        }
        catch (Exception ex) when (ex is ManifestException or IOException or NotSupportedException
            or FileFormatException or ArgumentException)
        {
            MessageBox.Show(
                $"Could not load \"{pack.Definition.Name}\": {ex.Message}",
                "CatPetStation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (save) SaveActivePets();
        PetsChanged?.Invoke();
    }

    public void Remove(PetController pet)
    {
        pet.Dispose();
        _pets.Remove(pet);
        SaveActivePets();
        PetsChanged?.Invoke();
    }

    public void RemoveAll()
    {
        foreach (var pet in _pets) pet.Dispose();
        _pets.Clear();
        SaveActivePets();
        PetsChanged?.Invoke();
    }

    public void SetAllSleeping(bool sleeping)
    {
        Settings.AllAsleep = sleeping;
        Settings.Save();
        foreach (var pet in _pets) pet.SetSleeping(sleeping);
        _clock.Interval = sleeping ? NapInterval : ActiveInterval;
    }

    public void SetScale(double scale)
    {
        Settings.Scale = scale;
        Settings.Save();

        // Respawn pets at the new size.
        var packs = _pets.Select(p => p.Pack).ToList();
        foreach (var pet in _pets) pet.Dispose();
        _pets.Clear();
        foreach (var pack in packs) Spawn(pack, save: false);
    }

    /// <summary>Runs the safe importer and reports, in plain language, what was and wasn't taken.</summary>
    public void ImportPackInteractive()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import pet pack (.zip)",
            Filter = "Pet packs (*.zip)|*.zip",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true) return;

        ImportResult result;
        try
        {
            result = PackImporter.ImportZip(dialog.FileName, AppSettings.PacksDirectory);
        }
        catch (Exception ex) when (ex is ManifestException or IOException or InvalidDataException)
        {
            MessageBox.Show($"Import failed: {ex.Message}", "CatPetStation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var skipped = result.Entries.Where(e => !e.Imported).ToList();
        var lines = new List<string>();
        lines.Add(result.AnyImported
            ? $"Imported: {string.Join(", ", result.ImportedPets)}"
            : "No pets could be imported from this archive.");
        if (skipped.Count > 0)
        {
            lines.Add("");
            lines.Add("Skipped (CatPetStation only ever extracts .png and .json):");
            lines.AddRange(skipped.Take(12).Select(e => $"  • {e.EntryName}: {e.Reason}"));
            if (skipped.Count > 12) lines.Add($"  … and {skipped.Count - 12} more");
        }

        MessageBox.Show(string.Join(Environment.NewLine, lines), "Pack import",
            MessageBoxButton.OK,
            result.AnyImported ? MessageBoxImage.Information : MessageBoxImage.Warning);

        foreach (var name in result.ImportedPets)
        {
            var pack = AvailablePacks.FirstOrDefault(p => p.Definition.Name == name);
            if (pack is not null) Spawn(pack);
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var dt = Math.Min((now - _lastTick).TotalSeconds, 0.1); // clamp hitches
        _lastTick = now;
        foreach (var pet in _pets)
            pet.Tick(dt);
    }

    private void SaveActivePets()
    {
        Settings.ActivePets = _pets
            .Select(p => Path.GetFileName(p.Pack.Directory))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList()!;
        Settings.Save();
    }

    public void Dispose()
    {
        _clock.Stop();
        foreach (var pet in _pets) pet.Dispose();
        _pets.Clear();
    }
}
