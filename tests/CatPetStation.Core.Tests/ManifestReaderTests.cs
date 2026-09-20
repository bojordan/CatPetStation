using CatPetStation.Core;

namespace CatPetStation.Core.Tests;

public class ManifestReaderTests
{
    private const string NativeJson = """
        {
          "format": "catpetstation/1",
          "name": "Station Cat",
          "spriteSheet": "cat.png",
          "frameWidth": 128,
          "frameHeight": 128,
          "author": "CatPetStation",
          "assetLicense": "MIT",
          "animations": {
            "stand": { "row": 0, "frames": 4, "fps": 5 },
            "walk":  { "row": 1, "frames": 4, "fps": 8 },
            "fall":  { "row": 2, "frames": 3 },
            "sleep": { "row": 3, "frames": 2, "fps": 2 }
          }
        }
        """;

    private const string DpetJson = """
        {
          "name": "The Second Coming",
          "img": "tsc.png",
          "width": 128,
          "height": 128,
          "animePos": {
            "stand": { "line": 0, "count": 4 },
            "walk":  { "line": 1, "count": 6 },
            "climb": { "line": 2, "count": 4 },
            "crawl": { "line": 3, "count": 4 },
            "jump":  { "line": 4, "count": 3 },
            "fall":  { "line": 5, "count": 4 },
            "drag":  { "line": 6, "count": 2 },
            "dance": { "line": 7, "count": 8 }
          }
        }
        """;

    [Fact]
    public void ParsesNativeFormat()
    {
        var pet = ManifestReader.Parse(NativeJson);

        Assert.Equal("Station Cat", pet.Name);
        Assert.Equal("cat.png", pet.SpriteSheet);
        Assert.Equal(128, pet.FrameWidth);
        Assert.Equal(4, pet.Animations["stand"].FrameCount);
        Assert.Equal(5, pet.Animations["stand"].Fps);
        Assert.Equal(ManifestReader.DefaultDpetFps, pet.Animations["fall"].Fps);
        Assert.Equal("MIT", pet.AssetLicense);
    }

    [Fact]
    public void ParsesDpetFormat()
    {
        var pet = ManifestReader.Parse(DpetJson);

        Assert.Equal("The Second Coming", pet.Name);
        Assert.Equal("tsc.png", pet.SpriteSheet);
        Assert.Equal(1, pet.Animations["walk"].Row);
        Assert.Equal(6, pet.Animations["walk"].FrameCount);
        Assert.Contains("dance", pet.CustomActionNames);
        Assert.DoesNotContain("walk", pet.CustomActionNames);
    }

    [Fact]
    public void AnimationLookupIsCaseInsensitive()
    {
        var pet = ManifestReader.Parse(DpetJson);
        Assert.True(pet.Has("WALK"));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("{}")]
    [InlineData("""{ "name": "x", "spriteSheet": "a.png", "frameWidth": 128, "frameHeight": 128, "animations": {} }""")]
    public void RejectsInvalidManifests(string json)
    {
        Assert.Throws<ManifestException>(() => ManifestReader.Parse(json));
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("..\\escape.png")]
    [InlineData("C:\\Windows\\evil.png")]
    [InlineData("/etc/passwd.png")]
    [InlineData("sub/dir.png")]
    [InlineData("sheet.exe")]
    public void RejectsUnsafeSpriteSheetReferences(string sheet)
    {
        var json = $$"""
            { "name": "x", "spriteSheet": {{System.Text.Json.JsonSerializer.Serialize(sheet)}},
              "frameWidth": 64, "frameHeight": 64,
              "animations": { "stand": { "row": 0, "frames": 1 } } }
            """;
        Assert.Throws<ManifestException>(() => ManifestReader.Parse(json));
    }

    [Fact]
    public void RejectsAbsurdDimensions()
    {
        var json = """
            { "name": "x", "spriteSheet": "a.png", "frameWidth": 999999, "frameHeight": 64,
              "animations": { "stand": { "row": 0, "frames": 1 } } }
            """;
        Assert.Throws<ManifestException>(() => ManifestReader.Parse(json));
    }

    [Fact]
    public void ResolveFallsBackToStand()
    {
        var pet = ManifestReader.Parse(NativeJson);
        var resolved = pet.Resolve("climb"); // not defined in the native sample
        Assert.Equal(pet.Animations["stand"], resolved);
    }
}
