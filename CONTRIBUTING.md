# Contributing to CatPetStation

Thanks for helping desktop pets be safe *and* fun. All contributions are under
the MIT license.

## Ground rules

1. **Assets are data, never code.** PRs that add scripting, plugin loading,
   pack-supplied code paths, network access, or telemetry will be declined —
   these are the product's core promises, not defaults to be toggled.
2. **Only art you have the rights to.** Original work or compatibly-licensed
   art with attribution (`author` + `assetLicense` in the manifest). No fan art
   of copyrighted characters, no extracted game assets, even "temporarily".
3. **Keep the importer paranoid.** Changes to `PackImporter`/`ManifestReader`
   need tests for the attack they might open up. The checklist in
   [docs/SAFE-IMPORT.md](docs/SAFE-IMPORT.md) is the review standard.

## Getting set up

```bash
git clone https://github.com/YOUR-USER/CatPetStation.git
cd CatPetStation
dotnet test                                   # 35 tests, all green
dotnet run --project src/CatPetStation.App    # meet Station Cat
```

Regenerate the built-in sprite sheet after editing the ASCII art in
[`tools/SpriteGen/CatArt.cs`](tools/SpriteGen/CatArt.cs):

```bash
dotnet run --project tools/SpriteGen
```

## Good first contributions

- **Art**: more frames for Station Cat (run animation, stretch, loaf), or an
  entirely new original pet under `assets/pets/`.
- **Behaviors**: new activities in `PetEngine` (chase the cursor *visually*
  without capturing it; sit on the taskbar edge). Engine changes need a
  deterministic test.
- **Platform**: multi-monitor support, fullscreen-app auto-hide, the
  [macOS Swift port](ports/macos/README.md).

## PR expectations

- `dotnet test` passes; new logic in `CatPetStation.Core` comes with tests.
- Engine changes stay UI-free; UI changes stay logic-free.
- One feature per PR, with a sentence in the description about *why*.
