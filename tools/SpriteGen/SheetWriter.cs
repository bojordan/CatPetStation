using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpriteGen;

/// <summary>Encodes a raw BGRA pixel grid as a PNG without any third-party dependencies.</summary>
public static class SheetWriter
{
    public static void WritePng(PixelGrid grid, string path)
    {
        var source = BitmapSource.Create(
            grid.Width, grid.Height, 96, 96, PixelFormats.Bgra32, null,
            grid.Pixels, grid.Width * 4);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}

/// <summary>A width x height buffer of 0xAARRGGBB pixels.</summary>
public sealed class PixelGrid(int width, int height)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public uint[] Pixels { get; } = new uint[width * height];

    public void Set(int x, int y, uint argb)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
            Pixels[y * Width + x] = argb;
    }
}
