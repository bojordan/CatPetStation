# Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  CatPetStation.App (WPF, net10.0-windows)                    │
│                                                              │
│   TrayIcon ──► PetHost ──► PetController ──► PetWindow       │
│                  │   │          │                            │
│                  │   └── 30 Hz DispatcherTimer               │
│                  │              │                            │
│                  │        SpriteSheetImage (frame cache)     │
└──────────────────┼──────────────┼────────────────────────────┘
                   │              │ Tick(dt) / CurrentFrame
┌──────────────────▼──────────────▼────────────────────────────┐
│  CatPetStation.Core (net10.0, no UI dependencies)            │
│                                                              │
│   ManifestReader ─► PetDefinition ─► PetEngine               │
│   PackImporter  ─► packs on disk ─► PackLibrary              │
└──────────────────────────────────────────────────────────────┘
```

## The split: Core vs. App

Everything that *decides* lives in `CatPetStation.Core`, which has **zero UI
dependencies** and targets plain `net10.0`:

- [`ManifestReader`](../src/CatPetStation.Core/ManifestReader.cs) parses the two
  manifest dialects (native + DPET) into an immutable `PetDefinition`.
- [`PackImporter`](../src/CatPetStation.Core/PackImporter.cs) is the hardened
  zip importer (see [SECURITY.md](../SECURITY.md) for the threat model).
- [`PetEngine`](../src/CatPetStation.Core/PetEngine.cs) is the pet's brain: a
  state machine (idle → walk → climb → crawl → fall → …) plus gravity/fling
  physics, advanced by `Tick(dt, screenBounds)` and read back through
  `CurrentFrame`. Randomness is injected, so tests simulate minutes of pet life
  deterministically.

This is also the porting seam: the macOS app reimplements the App column in
Swift/AppKit and keeps the Core semantics identical (same manifests, same
behavior table, same physics constants — see [ports/macos](../ports/macos/README.md)).

## The App column

- **`PetHost`** owns settings, the pack library, and one shared 30 Hz
  `DispatcherTimer` that drives every pet (dropping to 2 Hz while everyone
  naps). One timer for N pets keeps CPU flat.
- **`PetController`** binds one engine instance to one window: forwards ticks,
  pushes `X/Y` into `Window.Left/Top`, converts mouse drags to engine
  coordinates (DPI-aware), and computes an exponentially smoothed fling
  velocity so releasing mid-gesture throws the pet.
- **`PetWindow`** is a borderless, transparent, topmost, non-activating,
  taskbar-invisible WPF window containing a single `Image` with
  nearest-neighbor scaling (crisp pixel art) and a `ScaleTransform` for
  horizontal mirroring.
- **`SpriteSheetImage`** decodes the sheet once (`OnLoad`, size-validated
  against the manifest) and lazily crops frozen per-frame `CroppedBitmap`s.
- **`TrayIcon`** is the only WinForms touchpoint (`NotifyIcon` has no WPF
  equivalent); even its icon is drawn in code so the repo carries no binary
  icon.

## Behavior model

The engine maps *activities* to *animation names*; a pack simply not defining an
animation disables the matching behavior. Frame selection follows the DPET
conventions the community's art was drawn for: `fall` frame 0 is the airborne
pose with the rest of the row as landing, sprites face right and are mirrored to
move left, unknown animation names become random idle actions.

Screen geometry is the primary monitor's work area (`SystemParameters.WorkArea`,
in DIPs — the same space as `Window.Left/Top`, so no unit conversions leak into
the engine). The ground is the top of the taskbar.

## Persistence

`%APPDATA%\CatPetStation\settings.json` (active pets, scale, nap state) and
`%APPDATA%\CatPetStation\packs\` (imported packs). Both plain, hand-editable
JSON/files; deleting the folder factory-resets the app.

## Deliberate non-features

No network stack, no auto-updater, no telemetry, no plugin loading, no
elevation. These aren't roadmap gaps — they're the product. Anything that would
add remote code or data flow to the app needs an extraordinary justification.
