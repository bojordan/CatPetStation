# Development log

This project was built in one AI-assisted session (Claude Code, September 2026)
and this log records the actual process — research, decisions, dead ends, and
the reasoning behind the safety design — so the repo teaches as well as runs.

## 1. Research: what should a desktop pet app be?

We surveyed the field before writing code:

- **DPET : Desktop Pet Engine** (Steam, free) — the direct inspiration. Pets
  idle, walk, climb, clone; right-click actions; Twitch integration; reminder
  gimmicks. Notably, **DPET was delisted from Steam**, which stranded its
  community: workshop pets became unreachable and users began re-sharing
  extracted assets as zip dumps on Reddit. Its pet format (a PNG sprite sheet +
  JSON with `name`/`img`/`width`/`height`/`animePos`, rows of right-facing
  frames) became a de-facto community standard, documented only in Steam
  guides.
- **Shimeji / Shimeji-ee / Shijima-Qt** — the anime-mascot lineage: climbing,
  multiplying, window-tossing. Powerful but famously invasive, and the original
  needs Java.
- **Desktop Goose** — chaos as a feature (steals cursor, drags memes). Beloved,
  but the opposite of "stays out of your way".
- **eSheep, Bongo Cat, VPet, OpenPets** — nostalgia, reactivity, and the modern
  open-source umbrella respectively.

**Feature conclusions:** take DPET's behavior set (it's what community art is
drawn for: stand/walk/climb/crawl/jump/fall/drag + custom idle actions), skip
the invasive tricks (no cursor theft, no window interference), skip the
gimmicks (no Twitch, no reminders), and make *asset-pack compatibility and
import safety* the differentiating feature. An app whose community shares zip
files needs to treat zip files as hostile by default.

**The asset dump question.** The immediate motivation was a Reddit dump of the
Alan Becker DPET pets. We deliberately did **not** download it during
development — the safety design must not depend on any particular archive being
clean, and the characters are Alan Becker's IP so the art cannot ship in this
repo anyway. Instead, the DPET manifest schema came from public Steam guides,
and the importer was built so that any such dump — clean or booby-trapped — can
be imported with only its PNG/JSON surviving the trip. Users import their own
downloads locally; the repo stays 100 % original, MIT-licensed content.

## 2. Design decisions

- **Two projects, one seam.** Everything that decides (manifests, importer,
  behavior state machine, physics) is a UI-free `net10.0` library; WPF only
  renders. This makes the logic testable (35 tests simulate minutes of pet
  life deterministically via injected `Random`) and gives the planned Swift
  port a precise contract to reimplement.
- **Support the DPET manifest dialect natively** rather than convert-on-import:
  keeping users' files byte-identical to what they downloaded means re-imports,
  diffs, and community tooling all keep working.
- **Allowlist importer.** The importer never extracts by entry name: it reads
  approved content into memory, validates (PNG magic, manifest parse), then
  writes to file names *it* generates. Zip-slip is impossible not because paths
  are sanitized, but because archive paths are never used at all.
- **The built-in pet is generated from source.** ASCII pixel grids in
  `tools/SpriteGen` compile to the sprite sheet. No binary of unknown
  provenance in the repo, an unambiguous MIT license on the art, and pixel art
  that is reviewable in a pull request diff.
- **One 30 Hz timer for all pets**, not one per window; ~2 Hz in nap mode.
- **WinForms for exactly one class.** WPF has no `NotifyIcon`; referencing
  WinForms for the tray beats taking a NuGet dependency for a zero-dependency
  app. The cost was a round of `Point`/`MenuItem`/`MessageBox` ambiguity
  errors, fixed with using-aliases.

## 3. Build notes & dead ends

- `dotnet new sln` on .NET 10 creates `.slnx` (XML solution) — a tooling
  surprise worth knowing.
- WPF's implicit usings don't cover `System.IO` in top-level programs the way
  console templates do; `SpriteGen` needed the explicit using.
- First sprite-sheet render was ~90 % right on the first try; the bugs were a
  lowercase `o` (not in the palette → transparent hole in the sleeping cat) and
  a stray space inside an outline. ASCII art bugs are typo bugs.
- `SanitizeFileName("....png")` originally returned `"png"` — trimming leading
  dots after splitting the extension, not before, fixed it. Caught by tests.
- A behavior test asserting `Activity == Idle` five seconds after landing was
  flaky by design (the cat may legitimately have wandered off); the fix was
  asserting "on the ground, not falling" instead. Behavior tests should assert
  invariants, not schedules.
- WPF's image decoder throws `FileFormatException` (not `NotSupportedException`)
  for corrupt PNGs — the pack-load error handling has to catch both.

## 4. Window-ledge sitting (added post-v0.1.0)

The Shimeji-signature feature, built the good-citizen way: the app *reads*
window rectangles (`EnumWindows` + DWM frame bounds — never titles, contents,
or input) and turns visible top edges into `Ledge` records the engine treats as
standable surfaces. Design notes:

- The engine grew a `PetWorld` (bounds + ledges) and a `Support` concept.
  Standing pets re-validate their support every tick: a ledge that shifted
  within 24 DIPs carries the pet (cats ride gently dragged windows!), a
  vanished one starts a fall. Landing requires 40 % footing.
- A test named `YankingTheWindowAwayDropsThePet` failed on first run because
  the pet fell and **landed back on the same window at its new position** —
  emergent behavior better than the spec, so the test was renamed and the
  assertion inverted. Behavior engines earn their keep this way.
- Occlusion is interval subtraction along each top edge using the z-order that
  `EnumWindows` already returns; shell windows (Progman, WorkerW, tray) and
  cloaked UWP windows are filtered out.
- Ledges refresh at 5 Hz on the shared clock, not per pet; steady-state CPU
  stayed at ~1 % of one core with one pet.
- It's a tray toggle ("Pets can sit on windows"), on by default on Windows.
  The macOS port must make it opt-in: reading other apps' window geometry
  there requires the Screen Recording permission prompt.

## 5. Coats: one set of poses, four cats

Dusty (grey tabby), Domino (tuxedo), and Patches (calico) reuse Station Cat's
poses. A `Coat` is a palette swap plus an optional positional patch rule
evaluated in each pose's bounding-box coordinates — Domino's white socks are
"coat pixels with ny > 0.86", Patches' orange/black markings are vertical
bands over the upper 45 %. Two details mattered:

- Colorizing happens **before** derivation transforms (the crawl is a vertical
  flip of the walk), so markings stay attached to the body when a pose flips.
- Bounding-box coordinates, not cell coordinates, keep markings from sliding
  around as poses change size between animations.

CI's reproducibility check now hashes every file under `assets/pets`, and the
refactor was verified to leave `station-cat/cat.png` byte-identical.

## 6. What's deliberately left for contributors

- Multi-monitor roaming (engine already takes arbitrary bounds; the host just
  passes one work area today).
- Fullscreen-app detection to auto-hide pets during games/presentations.
- More Station Cat frames and more original pets — art PRs are the most
  welcome PRs.
- The Swift/AppKit port ([ports/macos](../ports/macos/README.md)).
