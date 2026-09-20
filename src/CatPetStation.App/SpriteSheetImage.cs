using System.IO;
using System.Windows.Media.Imaging;
using CatPetStation.Core;

namespace CatPetStation.App;

/// <summary>
/// Loads a pack's sprite sheet and serves per-frame bitmaps.
///
/// Decoding notes for imported (untrusted) art: the file is read fully into
/// memory and decoded with <see cref="BitmapCacheOption.OnLoad"/>, pixel
/// dimensions are checked against the manifest before use, and color profile
/// handling is disabled. The decoder only ever sees bytes that the importer
/// already verified start with the PNG signature.
/// </summary>
public sealed class SpriteSheetImage
{
    private const int MaxSheetPixels = 8192 * 8192;

    private readonly BitmapSource _sheet;
    private readonly PetDefinition _pet;
    private readonly Dictionary<(int Row, int Frame), CroppedBitmap> _cache = [];

    public SpriteSheetImage(InstalledPack pack)
    {
        _pet = pack.Definition;

        using var stream = new MemoryStream(File.ReadAllBytes(pack.SpriteSheetPath));
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.OnLoad);
        _sheet = decoder.Frames[0];
        _sheet.Freeze();

        if ((long)_sheet.PixelWidth * _sheet.PixelHeight > MaxSheetPixels)
            throw new ManifestException("Sprite sheet is unreasonably large.");

        var neededRows = _pet.Animations.Values.Max(a => a.Row) + 1;
        var neededCols = _pet.Animations.Values.Max(a => a.FrameCount);
        if (_sheet.PixelWidth < neededCols * _pet.FrameWidth ||
            _sheet.PixelHeight < neededRows * _pet.FrameHeight)
            throw new ManifestException(
                $"Sprite sheet is {_sheet.PixelWidth}x{_sheet.PixelHeight} but the manifest " +
                $"needs at least {neededCols * _pet.FrameWidth}x{neededRows * _pet.FrameHeight}.");
    }

    public BitmapSource GetFrame(SpriteFrame frame)
    {
        var anim = _pet.Resolve(frame.Animation);
        if (anim is null) return _sheet;

        var index = Math.Clamp(frame.FrameIndex, 0, anim.FrameCount - 1);
        if (_cache.TryGetValue((anim.Row, index), out var cached)) return cached;

        var crop = new CroppedBitmap(_sheet, new System.Windows.Int32Rect(
            index * _pet.FrameWidth, anim.Row * _pet.FrameHeight,
            _pet.FrameWidth, _pet.FrameHeight));
        crop.Freeze();
        _cache[(anim.Row, index)] = crop;
        return crop;
    }
}
