# Pet pack format

A pet pack is a directory (or a zip of one) holding exactly two kinds of file:

```
my-pet/
  pet.json     ← the manifest
  my-pet.png   ← the sprite sheet
```

That's the entire format. No code, no config beyond this, nothing executable.

## The sprite sheet

- One PNG containing a grid of equally sized frames.
- **Each row is one animation**; frames play left to right.
- **Sprites must face right** — the engine mirrors them to move left.
- Frames are typically 128×128, but any size up to 1024×1024 per frame works.
- Transparent background (straight from any pixel-art editor).

Special convention for `fall` (shared with DPET): **frame 0 is the airborne
pose**, and the remaining frames in the row play once as the landing.

## The manifest — native format

```jsonc
{
  "format": "catpetstation/1",
  "name": "Station Cat",
  "spriteSheet": "cat.png",      // bare file name only — paths are rejected
  "frameWidth": 128,
  "frameHeight": 128,
  "author": "you!",
  "assetLicense": "CC-BY-4.0",   // shown in the UI; please credit artists
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
```

### Animation names the engine knows

| Name | Played when |
| --- | --- |
| `stand` | idling on the ground (also the fallback for anything missing) |
| `walk` | strolling left/right |
| `climb` | going up the left or right screen edge |
| `crawl` | hanging from the top of the screen, moving sideways |
| `jump` | hopping (rising part of the arc) |
| `fall` | airborne (frame 0) and landing (frames 1+) |
| `drag` | held by the mouse |
| `sleep` | napping (via the pet's right-click menu) |

Only `stand` is required. Behaviors whose animation is missing simply don't
happen (a pet without `climb` never climbs), so simple two-row pets are fine.

**Any other name** (`dance`, `wave`, `loaf`, …) becomes a *custom action* the
pet performs at random while idling — the same convention DPET used.

## The manifest — DPET dialect

Manifests from DPET : Desktop Pet Engine packs are read natively, so existing
community assets import without editing:

```json
{
  "name": "My Pet",
  "img": "mypet.png",
  "width": 128,
  "height": 128,
  "animePos": {
    "stand": { "line": 0, "count": 4 },
    "walk":  { "line": 1, "count": 6 }
  }
}
```

`line`/`count` map to `row`/`frames`; timing defaults to 8 fps since the DPET
format carries none.

## Installing a pack

- **Zip import** (recommended): tray icon → *Import pet pack (.zip)*. Only
  validated `.png` + `.json` content is extracted, and you get a report of
  everything that was skipped.
- **Manual**: drop the pack directory into `%APPDATA%\CatPetStation\packs`.

## Distribute your pack like a good citizen

- Ship **only** the PNG and JSON. If your zip needs a "run me" anything, it's
  not a pet pack.
- State the art license in `assetLicense` and credit the artist in `author`.
- Don't redistribute other people's characters or extracted game assets unless
  the rights holder allows it. Fan-made art of copyrighted characters is for
  personal use — keep it out of public packs and out of this repository.
