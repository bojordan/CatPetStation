# CatPetStation for macOS — port plan

A native Swift sibling of the Windows app. Not started yet; this document is
the design so a contributor (or a future session) can build it without
guessing.

## Goals

Identical pet packs, identical behavior. A pack authored for the Windows app —
including imported DPET-dialect packs — must work unchanged on macOS.

## Stack

- **Swift 6 + AppKit** (not SwiftUI for the pet window itself: we need
  borderless, non-activating, click-through-precise `NSWindow` control).
- No third-party dependencies, mirroring the Windows app.
- Distribution: signed + notarized `.app` in a `.dmg`; Homebrew cask later.

## Mapping the architecture

| Windows (WPF) | macOS (AppKit) |
| --- | --- |
| `PetWindow` (transparent, topmost, no taskbar) | Borderless `NSWindow`, `backgroundColor = .clear`, `isOpaque = false`, `level = .floating`, `collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle]` |
| `Image` + `NearestNeighbor` + `ScaleTransform` flip | `CALayer` with sprite frame contents, `magnificationFilter = .nearest`, `transform` for mirroring |
| `DispatcherTimer` 30 Hz | `CVDisplayLink` or `Timer` on the main run loop (30 Hz is plenty) |
| `TrayIcon` (`NotifyIcon`) | `NSStatusItem` menu-bar item; app is `LSUIElement` (no Dock icon) |
| `SystemParameters.WorkArea` | `NSScreen.main.visibleFrame` (note: origin is *bottom-left*; the engine's Y grows downward, so flip when applying) |
| `%APPDATA%\CatPetStation` | `~/Library/Application Support/CatPetStation` |
| WPF PNG decode + validation | `CGImageSource` with explicit type identifier + pixel-size checks |
| `System.IO.Compression` importer | Apple `Compression`/`libarchive` is awkward for zip — prefer a minimal built-in zip reader (central directory only) to keep the same allowlist-in-memory design |

## Port the Core by contract, not by transliteration

Reimplement these files as pure Swift with the same names, constants, and
tests (the C# tests are the spec — port them first):

- `ManifestReader` — both JSON dialects, same rejection rules
  (`Codable` won't cut it for the validation detail; parse via
  `JSONSerialization` and validate explicitly).
- `PackImporter` — same allowlist pipeline, same limits, same
  `ImportEntryResult` reporting. macOS additions: strip quarantine handling is
  *not* needed (we never execute), but keep the reserved-name and ADS checks
  for packs that may travel back to Windows machines.
- `PetEngine` — same activities, same tuning constants (`WalkSpeed = 65` etc.),
  same fall-frame conventions. Inject the RNG (`RandomNumberGenerator`
  protocol) for deterministic tests.

## Window-ledge sitting

The Windows app lets pets sit on other windows' top edges via read-only
geometry (`WindowLedgeProvider` → `PetWorld.Ledges`; the engine logic and its
tests port as-is). On macOS the equivalent is `CGWindowListCopyWindowInfo`
(`kCGWindowListOptionOnScreenOnly`, reading only `kCGWindowBounds` + layer for
z-order/occlusion) — but on modern macOS reading other apps' window bounds
requires the **Screen Recording permission**, which is a scary prompt for a pet
app. Ship the feature **off by default** here, behind an explanatory opt-in
("macOS asks for Screen Recording permission because window *positions* count
as screen information; CatPetStation reads geometry only — never pixels or
titles"). Everything else works without the permission.

## Safety parity checklist

- [ ] Sandboxed app (App Sandbox on, user-selected-file read for zip import)
- [ ] No network entitlements at all
- [ ] `asInvoker` equivalent: no privileged helpers, no launch agents
- [ ] Import report UI identical in spirit to Windows

## Suggested layout

```
ports/macos/CatPetStation/
  Package.swift                 (SwiftPM, executable target + test target)
  Sources/CatPetStationCore/    (engine, manifests, importer)
  Sources/CatPetStationApp/     (AppKit app)
  Tests/CatPetStationCoreTests/ (ported spec tests)
```
