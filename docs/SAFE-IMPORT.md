# Importing community asset dumps, safely

Desktop pet assets circulate as zip archives on Reddit, Discord, forums, and
file lockers — often after the original app disappeared (this is exactly what
happened to DPET : Desktop Pet Engine, whose community re-shared its pet assets
after the app was delisted from Steam). Those archives are untrusted input. Some
genuinely contain only sprite sheets; some bundle "helper" executables; some are
malware with a cute file name.

This guide is the workflow we recommend — it's the same one used while building
this project.

## What CatPetStation guarantees

When you use *Import pet pack (.zip)*, the app:

- extracts **only** files that are verifiably PNG images or valid pet manifests;
- **never executes anything** from an archive, and never even writes any other
  file type to disk;
- neutralizes hostile archive tricks (path escapes, decompression bombs,
  disguised executables, alternate data streams);
- shows you a report listing every skipped file and the reason.

So the worst a malicious zip can do *through CatPetStation* is waste your time.
The remaining risk is what **you** do with the archive outside the app.

## Rules for handling a downloaded asset dump

1. **Never run anything from the archive.** No `setup.exe`, no "extractor", no
   `.bat`/`.ps1`/`.scr`, no matter what the upload post says. Real pet assets
   need no installer — they are pictures and text.
2. **Don't unzip it at all if you can avoid it.** Feed the zip directly to
   CatPetStation's importer and let the allowlist do the choosing. Unzipping
   first is how a stray double-click happens.
3. **Let your antivirus see it.** Windows Defender scans downloads by default;
   you can also right-click → *Scan with Microsoft Defender*, or upload the file
   to VirusTotal for a second opinion. A clean scan is *not* proof of safety —
   but a flagged scan is proof enough to delete it.
4. **Mind the copyright.** Ripped or re-shared assets of commercial characters
   are for your own desktop only. Don't re-upload them, don't bundle them into
   packs you distribute, and don't commit them to public repositories — that's
   how communities get DMCA'd.
5. **When sharing your own packs**, zip exactly two file types (`.png`,
   `.json`), name the pet in the manifest, and state the license. A pack that
   imports with zero skipped files is the mark of a well-made pack.

## For maintainers of other pet apps

The importer in [`PackImporter.cs`](../src/CatPetStation.Core/PackImporter.cs)
is deliberately small and dependency-free (~200 lines of documented C#) and MIT
licensed — port it, copy it, or use it as a checklist:

- [ ] allowlist extensions, don't blocklist
- [ ] discard entry paths entirely; generate your own file names
- [ ] verify magic bytes, not just extensions
- [ ] cap entry count, per-entry size, and total uncompressed size
- [ ] verify streams don't exceed their declared length
- [ ] parse and validate manifests before keeping anything
- [ ] refuse `:` (ADS) and reserved device names on Windows
- [ ] report every skipped entry to the user
