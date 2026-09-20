# Security policy

## Threat model

CatPetStation's defining risk is **malware distribution through pet packs**.
Desktop pet communities share assets as zip files on forums, Discord, and Reddit;
those archives regularly bundle executables, and users are routinely told to run
them. CatPetStation is designed so that this class of attack cannot pass through
the app:

1. **Packs are pure data.** A pet is a PNG sprite sheet plus a JSON manifest.
   There is no plugin system, no scripting, no pack-supplied code path — by
   design, not by policy.
2. **The importer is an allowlist.** Only `.json` and `.png` entries are ever
   extracted from an archive ([`PackImporter`](src/CatPetStation.Core/PackImporter.cs)).
   Executables, scripts, shortcuts, and everything else are skipped and shown to
   the user in an import report.
3. **Archive attacks are handled.**
   - *Zip slip*: entry paths are discarded entirely; content is written to
     flat, sanitized file names chosen by the app.
   - *Decompression bombs*: per-entry and per-archive size caps, entry-count
     caps, and a check that streams do not exceed their declared length.
   - *Masquerading files*: `.png` entries must begin with the PNG signature;
     `.json` entries must parse as a valid pet manifest.
   - *Windows quirks*: alternate data stream names (`file.png:evil.exe`) and
     reserved device names (`CON`, `NUL`, …) are rejected.
4. **Manifests cannot reach out of their pack.** A sprite-sheet reference must
   be a bare file name — paths, drive letters, and `..` are rejected at parse
   time, with dimension and animation-count sanity limits on everything else.
5. **The app itself is a minimal target.** No network access, no elevation
   (`asInvoker`), no shell execution of user data (the only `Process.Start` is
   opening the packs folder in Explorer), settings are plain JSON in
   `%APPDATA%\CatPetStation`.

What the importer does **not** do: it does not scan images for steganography or
guarantee an archive is otherwise clean — it guarantees that *only inert image
and manifest data enters your system through this app*. Never run executables
from asset archives; see [docs/SAFE-IMPORT.md](docs/SAFE-IMPORT.md).

## Supported versions

The latest release on the default branch.

## Reporting a vulnerability

Please open a private security advisory on GitHub (Security → Advisories →
Report a vulnerability) rather than a public issue. Reports that include a
proof-of-concept archive are especially welcome — add it as a *renamed* `.bin`
attachment, never as a live `.zip` of executables.
