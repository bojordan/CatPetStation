namespace SpriteGen;

/// <summary>
/// The Station Cat, drawn as ASCII pixel grids. Legend:
/// '.' transparent · 'O' outline · 'b' orange body · 's' stripe · 'c' cream
/// 'p' pink · 'e' eye green · 'k' pupil · 'w' white.
/// All poses face RIGHT (the engine flips them to walk left).
/// </summary>
public static class CatArt
{
    public const int Cell = 32;    // logical pixels per frame side
    public const int Scale = 4;    // output pixels per logical pixel
    public const int Frame = Cell * Scale;

    private static readonly Dictionary<char, uint> Palette = new()
    {
        ['O'] = 0xFF4A3123, // dark brown outline
        ['b'] = 0xFFF49E4C, // orange coat
        ['s'] = 0xFFD9782A, // darker stripe
        ['c'] = 0xFFFFEBC9, // cream muzzle/chest
        ['p'] = 0xFFF2A5AE, // pink nose & inner ear
        ['e'] = 0xFF57A63F, // green iris
        ['k'] = 0xFF26211E, // pupil
        ['w'] = 0xFFFFFFFF, // highlight
    };

    // ---- Poses ------------------------------------------------------------

    private static readonly string[] SitTailDown =
    [
        "..........O.......O.............",
        ".........ObO.....ObO............",
        ".........ObbO...ObbO............",
        "........ObpbbOOObbpbO...........",
        "........ObbbbbbbbbbbO...........",
        ".......ObbsbbbbbbsbbO...........",
        ".......ObbbbbbbbbbbbbO..........",
        ".......ObekbbbbbbekbbO..........",
        ".......ObbbbbcbbbbbbbO..........",
        "........ObbccpccbbbO............",
        "........ObbccccccbbO............",
        ".........ObbbbbbbbO.............",
        "........ObbbbbbbbbbO............",
        ".......ObbbbbbbcbbbbO...........",
        "......ObsbbbbbccbbbbbO..........",
        "......ObbbbbbbccbbbbbO..........",
        ".....ObsbbbbbbccbbbbbbO.........",
        ".....ObbbbbbbbccbbbbbbO.........",
        "....ObsbbbbbbbccbbbbbbO.........",
        "....ObbbbbbbbbbbbbbbbbOOOO......",
        "....ObbbbbbbObbbbObbbObssbO.....",
        "....ObbbbbbObbbbbbObbObbbsO.....",
        ".....OOOOOOObbbbbbObbOOOOO......",
        "...........OOOOOOO.OOO..........",
    ];

    private static readonly string[] SitTailUp =
    [
        "..........O.......O.............",
        ".........ObO.....ObO............",
        ".........ObbO...ObbO............",
        "........ObpbbOOObbpbO...........",
        "........ObbbbbbbbbbbO...........",
        ".......ObbsbbbbbbsbbO...........",
        ".......ObbbbbbbbbbbbbO..........",
        ".......ObekbbbbbbekbbO..........",
        ".......ObbbbbcbbbbbbbO..........",
        "........ObbccpccbbbO............",
        "........ObbccccccbbO............",
        ".........ObbbbbbbbO.............",
        "........ObbbbbbbbbbO............",
        ".......ObbbbbbbcbbbbO...........",
        "......ObsbbbbbccbbbbbO..........",
        "......ObbbbbbbccbbbbbO......O...",
        ".....ObsbbbbbbccbbbbbbO....ObO..",
        ".....ObbbbbbbbccbbbbbbO....OsO..",
        "....ObsbbbbbbbccbbbbbbO....ObO..",
        "....ObbbbbbbbbbbbbbbbbOOOOObsO..",
        "....ObbbbbbbObbbbObbbObssbbbO...",
        "....ObbbbbbObbbbbbObbObbbsOO....",
        ".....OOOOOOObbbbbbObbOOOOO......",
        "...........OOOOOOO.OOO..........",
    ];

    private static readonly string[] WalkA =
    [
        "......................O....O....",
        ".O...................ObO..ObbO..",
        "ObO..................ObbbbbbbbO.",
        "ObO..................ObekbbbbbO.",
        ".ObO.................ObbbbbcpbO.",
        "..ObO......OOOOOO....ObbccccbO..",
        "...ObO...OObbbbbbOOOObbbbbbbO...",
        "....ObOOObsbbbsbbbbbbbbbbbbO....",
        ".....ObbbbbbbbbbbbsbbbbbbbO.....",
        "......ObbbbbbbbbbbbbbbbbbO......",
        "......ObbbbbbbbbbbbbbbbbO.......",
        "......ObbbObbbbbbbbObbbbO.......",
        ".....ObbO..ObbO....ObbO.O.......",
        ".....ObO....ObbO....ObbO........",
        ".....OO......OOO.....OOO........",
    ];

    private static readonly string[] WalkB =
    [
        "......................O....O....",
        ".O...................ObO..ObbO..",
        "ObO..................ObbbbbbbbO.",
        "ObO..................ObekbbbbbO.",
        ".ObO.................ObbbbbcpbO.",
        "..ObO......OOOOOO....ObbccccbO..",
        "...ObO...OObbbbbbOOOObbbbbbbO...",
        "....ObOOObsbbbsbbbbbbbbbbbbO....",
        ".....ObbbbbbbbbbbbsbbbbbbbO.....",
        "......ObbbbbbbbbbbbbbbbbbO......",
        "......ObbbbbbbbbbbbbbbbbO.......",
        "......ObbbbObbbbbbObbbbbO.......",
        ".......ObbbObbb...ObbbO.........",
        ".......ObbO.OObO...ObbO.........",
        ".......OOO...OO.....OOO.........",
    ];

    private static readonly string[] WalkC =
    [
        "......................O....O....",
        ".O...................ObO..ObbO..",
        "ObO..................ObbbbbbbbO.",
        "ObO..................ObekbbbbbO.",
        ".ObO.................ObbbbbcpbO.",
        "..ObO......OOOOOO....ObbccccbO..",
        "...ObO...OObbbbbbOOOObbbbbbbO...",
        "....ObOOObsbbbsbbbbbbbbbbbbO....",
        ".....ObbbbbbbbbbbbsbbbbbbbO.....",
        "......ObbbbbbbbbbbbbbbbbbO......",
        "......ObbbbbbbbbbbbbbbbbO.......",
        "......ObbbbObbbbbbbObbbbO.......",
        "......O.ObbO..ObbO...ObbO.......",
        "........ObbO...ObO....ObbO......",
        "........OOO.....OO.....OOO......",
    ];

    private static readonly string[] ClimbA =
    [
        "...........O.....O..............",
        "..........ObO...ObO.............",
        ".........ObbbOOObbbO............",
        ".........ObbbbbbbbbO............",
        ".........ObsbbsbbsbO............",
        "......OO.ObbbbbbbbbO............",
        "......ObbObbbbbbbbbO............",
        ".......OObbbbbbbbbbO............",
        ".........ObbbbbbbbbO.OO.........",
        ".........ObbbbbbbbbObbO.........",
        ".........ObbbbbbbbbbOO..........",
        "......OOObbbbbbbbbbO............",
        "......ObbObsbbbsbbbO............",
        ".......OO.ObbbbbbbO.............",
        "..........ObbbbbbO.OO...........",
        "..........ObbbbbbObbO...........",
        "......O...ObbbbbbbOO............",
        ".....ObO..ObbbbbbO..............",
        "......ObO..OOOOOO...............",
        ".......O........................",
    ];

    private static readonly string[] Leap =
    [
        "......................O....O....",
        ".....................ObO..ObbO..",
        "....................ObbbbbbbbO..",
        "....................ObekbbbbbO..",
        ".O..................ObbbbbcpbO..",
        "ObO.................ObbccccbO...",
        "ObO........OOOOOO...ObbbbbbbO...",
        ".ObO....OObbbbbbbOOObbbbbbbO....",
        "..ObOOObsbbbsbbbbbbbbbbbbbO.....",
        "....ObbbbbbbbbbbbsbbbbbbbO......",
        ".....ObbbbbbbbbbbbbbbbbbO.......",
        "....ObbObbbbbbbbbbbObbbO........",
        "...ObbO..ObbbbbbbbObbO..........",
        "..ObbO....ObbO.OOOObbO..........",
        "..OOO......OOO.....OOO..........",
    ];

    private static readonly string[] LandSquash =
    [
        ".....................O.....O....",
        ".O..................ObO...ObO...",
        "ObO.................ObbbbbbbbO..",
        "ObbO................ObOObbbbbO..",
        ".ObbO......OOOOOO...ObbbbbcpbO..",
        "..ObbOOOObbbbbbbbOOObbccccbbO...",
        "....ObbsbbbsbbbbsbbbbbbbbbbO....",
        ".....ObbbbbbbbbbbbbbbbbbbbO.....",
        "....ObbbObbbbbbObbbbbObbbbO.....",
        "....ObbbObbbbbbObbbbbObbbO......",
        ".....OOOOOOOOOOOOOOOOOOOO.......",
    ];

    private static readonly string[] DragA =
    [
        "..........O.......O.............",
        ".........ObO.....ObO............",
        ".........ObbO...ObbO............",
        "........ObpbbOOObbpbO...........",
        "........ObbbbbbbbbbbO...........",
        ".......ObbsbbbbbbsbbO...........",
        ".......ObbbbbbbbbbbbbO..........",
        ".......ObekbbbbbbekbbO..........",
        ".......ObbbbbcbbbbbbbO..........",
        "........ObbccpccbbbO............",
        "........ObbccccccbbO............",
        ".........ObbbbbbbbO.............",
        "........ObbbbbbbbbO.............",
        "........ObbbcbbbbbO.............",
        "........ObbbccbbbbO.............",
        "........ObbbccbbbbO.............",
        "........ObbbccbbbbO.............",
        "........ObsbbbbbsbO.............",
        "........ObbbbbbbbbO.............",
        "........ObObbbbObbO..O..........",
        "........ObO.bb..ObO.ObO.........",
        "........OO..OO...OO.ObO.........",
        "....................O...........",
    ];

    private static readonly string[] Sleep =
    [
        "..........OOOOOOOO..............",
        ".......OOObbbbbbbbOO............",
        ".....OObbbbsbbbbsbbbOO..........",
        "....ObbsbbbbbbbbbbbbbbO.........",
        "....ObbbbbOOOOObbbsbbbO.........",
        "...ObbbbOObbbbbOObbbbbbO........",
        "...ObbsbObbObbbbObsbbbbO........",
        "...ObbbbObbbbpcbObbbbbbO........",
        "...ObbbbbOccccbbbbbbbsbO........",
        "....ObbsbbOOObbbbbbbbbO.........",
        ".....OObbbbbbbbbbbbbOO..........",
        ".......OOOOOOOOOOOOO............",
    ];

    // ---- Sheet layout -----------------------------------------------------

    /// <summary>Animation rows in sheet order. Must match <see cref="Manifest"/>.</summary>
    public static PixelGrid BuildSheet()
    {
        var blink = SitTailDown
            .Select(r => r.Replace('e', 'O').Replace('k', 'O'))
            .ToArray();

        string[][][] rows =
        [
            [SitTailDown, SitTailUp, SitTailDown, blink],          // stand
            [WalkA, WalkB, WalkC, WalkB],                          // walk
            [ClimbA, HFlip(ClimbA)],                               // climb
            [VFlip(WalkA), VFlip(WalkC)],                          // crawl (hanging)
            [Leap, WalkB],                                         // jump
            [Leap, LandSquash, SitTailDown],                       // fall: airborne, squash, recover
            [DragA, ShiftLowerHalf(DragA, 1)],                     // drag
            [Sleep, DropTopRow(Sleep)],                            // sleep (breathing)
        ];

        var maxFrames = rows.Max(r => r.Length);
        var sheet = new PixelGrid(maxFrames * Frame, rows.Length * Frame);

        for (var row = 0; row < rows.Length; row++)
        {
            // Crawl hangs from the top of its cell; everything else stands on the bottom.
            var anchorTop = row == 3;
            for (var col = 0; col < rows[row].Length; col++)
                Blit(sheet, rows[row][col], col * Frame, row * Frame, anchorTop);
        }

        return sheet;
    }

    public const string Manifest = """
        {
          "format": "catpetstation/1",
          "name": "Station Cat",
          "spriteSheet": "cat.png",
          "frameWidth": 128,
          "frameHeight": 128,
          "author": "CatPetStation contributors (generated by tools/SpriteGen)",
          "assetLicense": "MIT",
          "animations": {
            "stand": { "row": 0, "frames": 4, "fps": 2.5 },
            "walk":  { "row": 1, "frames": 4, "fps": 9 },
            "climb": { "row": 2, "frames": 2, "fps": 5 },
            "crawl": { "row": 3, "frames": 2, "fps": 5 },
            "jump":  { "row": 4, "frames": 2, "fps": 8 },
            "fall":  { "row": 5, "frames": 3, "fps": 9 },
            "drag":  { "row": 6, "frames": 2, "fps": 4 },
            "sleep": { "row": 7, "frames": 2, "fps": 1.2 }
          }
        }
        """;

    // ---- Grid helpers -----------------------------------------------------

    private static void Blit(PixelGrid sheet, string[] pose, int cellX, int cellY, bool anchorTop)
    {
        var yOffset = anchorTop ? 0 : Cell - pose.Length;
        for (var py = 0; py < pose.Length; py++)
        {
            var line = pose[py];
            for (var px = 0; px < Math.Min(line.Length, Cell); px++)
            {
                if (!Palette.TryGetValue(line[px], out var color)) continue;
                for (var sy = 0; sy < Scale; sy++)
                    for (var sx = 0; sx < Scale; sx++)
                        sheet.Set(
                            cellX + px * Scale + sx,
                            cellY + (yOffset + py) * Scale + sy,
                            color);
            }
        }
    }

    private static string[] HFlip(string[] pose) =>
        pose.Select(r => new string(r.PadRight(Cell, '.').Reverse().ToArray())).ToArray();

    private static string[] VFlip(string[] pose) => pose.Reverse().ToArray();

    private static string[] DropTopRow(string[] pose) => pose.Skip(1).ToArray();

    private static string[] ShiftLowerHalf(string[] pose, int dx)
    {
        var from = pose.Length / 2;
        return pose.Select((r, i) =>
            i < from ? r : new string('.', dx) + r).ToArray();
    }
}
