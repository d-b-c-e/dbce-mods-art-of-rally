# Releasing

Player instructions are in [SETUP.md](SETUP.md). Local build prerequisites are
in [BUILDING.md](BUILDING.md). This document is for maintainers.

## One mod, not four

All four features ship as a single mod with per-feature toggles, and that is
deliberate rather than lazy packaging:

- They hang off the same two Harmony hooks. `CarDynamics.FixedUpdate` serves both
  force feedback and telemetry; `CarController.Start` serves both the steering fix
  and the calibration fix. Split into separate mods, each would install its own
  patch on the same methods, and users would be running two or three copies of the
  same hook.
- The steering and deadzone fixes are prerequisites for the rest feeling right.
  Someone who installs "the force feedback mod" alone and still has a 27 degree
  dead band would reasonably conclude the force feedback is bad.
- Anything unwanted is one config line away from off.

The config is grouped by feature so this reads clearly to a user:

```
[Steering]  DirectSteering, ZeroAxisDeadzone, BindAnyDevice, GlyphTextFallback, DisableSteerAssist
[ForceFeedback]  Enabled, Strength, FyReference, Smoothing, Invert, PreferredDevice(+Index/Guid), DiagnosticLogging
[Shifter]  Enabled, IsHPattern, DeviceIndex/Name/Guid, SkipNeutral, gear and shift buttons
[WheelInput]  Enabled, Steer/Throttle/Brake/Clutch/Handbrake bindings ("device|index|axis:N|rest|far|guid:<instance-guid>"; legacy five fields accepted)
[Camera]  Bonnet* and Bumper*: Enabled, Height, Forward, Side, Pitch, FOV; BonnetLean
[CameraTuning]  remappable keyboard hotkeys (numpad defaults)
[Telemetry]  Enabled, Host, Port
[Experiment]  UseDirectInputBackend (Settings.xml only; see CONTROLS.md)
```

## Loader: Unity Mod Manager (done)

Shipping for **Unity Mod Manager**, which has official art of rally support in its
own game database:

```xml
<GameInfo Name="Art of Rally">
  <EntryPoint>[UnityEngine.UIModule.dll]UnityEngine.Canvas.cctor:Before</EntryPoint>
  <StartingPoint>[Assembly-CSharp.dll]GameEntryPoint.Start:After</StartingPoint>
</GameInfo>
```

Reasons, in order of weight:

1. **It is what the community already runs.** The Nexus camera mod requires it, so
   most people who would install this already have it.
2. **In-game settings.** UMM draws a settings panel at Ctrl+F10. That matters more
   than usual here: force feedback strength and the camera mount can only be judged
   while driving, and the alternative is quitting to edit a text file for every
   adjustment.

3. **It would have collided with BepInEx.** Verified after installing: UMM's
   installer wrote Doorstop to the game root, exactly as BepInEx does.

   ```ini
   ; <game>/doorstop_config.ini, alongside <game>/winhttp.dll
   target_assembly = artofrally_Data\Managed\UnityModManager\UnityModManager.dll
   ```

   Worth recording how this got muddled, because the trap is easy to fall into
   twice. UMM's game database defines an `EntryPoint` for art of rally that looks
   like assembly injection, and reading that alone led to retracting a correct
   claim. UMM supports both mechanisms and its installer chose Doorstop here.
   **A config file describes a capability; only the install shows which one was
   actually used.**

Development originally used BepInEx because it installs unattended; UMM's installer
is a GUI. That is a fine reason to prototype with it and a poor reason to ship it.

### How the port stayed contained

`Main.cs` is the only loader-aware file. Everything else talks to `ModLog` and a
plain-field `Settings` class, so supporting a second loader means adding a sibling
of `Main.cs` rather than touching a single patch. The pieces a loader entry point
provides:

1. `Info.json` — `Id`, `DisplayName`, `Version`, `AssemblyName`, `EntryMethod`.
2. `Main.Load(UnityModManager.ModEntry)` — creates the Harmony instance, patches,
   installs the watchdog.
3. `Settings : UnityModManager.ModSettings` with plain serialized fields;
   `SettingsPanel` draws the custom Ctrl+F10 interface.
4. References to `UnityModManager.dll` and its Harmony, extracted into `lib/umm`
   and **never committed**.

Two build notes worth keeping:

- The project targets **net48**, not net472, because UMM's own assemblies are
  built against .NET Framework 4.8 and will not resolve from a lower target.
  Unity 2019.4's Mono runs both.
- The custom panel owns labels/help and section layout; UMM owns the surrounding UI.

Shipping both loaders remains possible — two thin entry assemblies over the shared
core — but keep one as the documented default so support questions stay simple.

## Package contents and native placement

The zip contains `ArtOfSimRally/` at its root for UMM, plus Install.bat,
Uninstall.bat, install.ps1, verify.ps1, README.txt, LICENSE and manifest.json.
The standalone README.txt is generated from `tools/installer/README.txt`, with
`@RELEASE@` replaced at packaging time. Keep it aligned with README.md and SETUP.md;
use web links for details that are not included in the ZIP. Published archives
are immutable; updated installer/docs payloads go into the next package.
The baseline mod folder contains Info.json, build.json, ArtOfSimRally.Mod.dll,
Dbce.Wheel.Ffb.dll, Dbce.Wheel.Telemetry.dll, UnityForceFeedback.dll,
Dbce.TripleScreen.Core.dll and Dbce.TripleScreen.Protocol.dll. The shared-input
flavor adds dbce_shim.dll: nine owned mod payloads in the installed rc28 package. No game
or UMM assemblies or user Settings.xml are packaged. The development recorder
is a separate optional installation; packaging rejects its types/dependencies
in the production assembly as well as unexpected payload files.

The native artifact is our toolkit's x64 WheelFfb.dll, shipped under the name
UnityForceFeedback.dll. The batch installer deliberately copies it **both** beside
the mod and into `artofrally_Data/Plugins/x86_64`. UMM's archive route installs the
mod-folder copy. Neither path proves staleness: compare observed module hashes
with the tested package. If multiple copies differ, close the game and reinstall
the complete candidate; do not delete a correct plugin copy merely for its path.

## Cutting a release

The owner accepted the currently installed rc28 on October18. Its exact source,
ZIP, installation and retained qualification are in [Local deployment](LOCAL-DEPLOYMENT.md);
the [0.4.2 release draft](releases/0.4.2.md) separates the remaining publication
decision from untested hardware extras. This documentation update changes no
runtime files or accepted artifact.

**Current stable producer restrictions remain in force:** `package.ps1` rejects
a stable version with `-SharedInputCandidate`, and also rejects a final package
using the current `local+c8ec2ee` toolkit pin. Public distribution of the pinned
shim binary/source and the optional helper remains an explicit owner decision.
Do not remove either guard, fake an official pin, replace the accepted native,
or build a different baseline flavor just to obtain a stable label. Resolve
the public packaging/provenance scope in an assigned implementation first.

The owner has standing authorization to keep the local test installation current.
As a checklist item when finishing a feature or bug fix, deploy its artifact after
the automated gate passes, with the game closed and settings/backups preserved;
no new permission request is needed. If the game is open, record a pending deploy
for the next active work session. Do not schedule periodic deployment checks. See
[LOCAL-DEPLOYMENT.md](LOCAL-DEPLOYMENT.md). Public publication and attended
sign-off still follow the steps below.

Build, test and package releases **locally**, then upload the exact validated
archive and its SHA-256 file directly with the GitHub CLI. This is the owner's
release-build preference from 2026-09-08; do not add or dispatch GitHub Actions
builds unless the owner changes it. The repository had no Actions workflows when
checked on that date. GitHub hosts the source and release assets; it does not
need to rebuild the archive.

1. Vendor the intended official toolkit release using the transactional
   Sync-Toolkit.ps1. For 0.2.6, the five v0.15.0 payloads match the tested
   local RC16 pin byte-for-byte.
   Confirm VERSION and hashes; do not label an unreleased toolkit build as a
   published toolkit version. Building this consumer locally is the normal path.
2. Keep `Version.props` and source Info.json on the same numeric UMM version.
   Run `tools/testing/Test-Rc.ps1 -Version X.Y.Z-rc.N` with an unused RC number.
   It builds with warnings as errors, runs consumer checks, validates vendor
   hashes/exports and packages the identified artifact. Existing RCs are immutable.
3. Close the game, install that zip and complete the generated attended checklist.
   See [PRE-RELEASE-TESTING.md](PRE-RELEASE-TESTING.md) for captures and evidence.
4. Run `python tools/testing/rc_gate.py check <manual.json>`. Pending cases,
   missing evidence and changed archives return nonzero. A build alone is not a gate.
5. Review the concise Unreleased notes; keep unconfirmed hardware reports open.
   Commit the source, prepare the final-labelled artifact from a clean tree, and
   validate that exact artifact before tagging or publishing. Rebuilding changes
   its identity and invalidates the earlier artifact's sign-off.
6. When publication is authorized, create and push a tag at the **source commit
   recorded in the artifact's identity**, even if later documentation commits
   exist. Use `gh release create` with that tag, the exact ZIP and `.zip.sha256`,
   `--verify-tag` and `--notes-file` pointing to the reviewed release notes.
   For a candidate, include `--prerelease --latest=false` to preserve the stable
   release. No workflow dispatch or remote rebuild is involved.
7. Download the published ZIP and checksum with `gh release download` into a
   separate local folder and compare their SHA-256 against the automated report.
   Keep the report and attended checklist tied to the same archive. Never
   overwrite a published candidate to insert fixes; use a new version instead.

The existing 0.2.4-rc.4 ZIP was already built and tested locally; its
[exact identity and reports](reviews/2026-09-08-feedback-review.md) remain valid.
Changing the upload procedure does not require rebuilding it or imply that its
pending attended checks have passed.

`tools/package/package.ps1 -Version X.Y.Z` remains the packaging entry point for
a final clean-tree build; it does not itself authorize publication or claim game
tests passed. Numeric UMM/assembly versions and the full informational build
identity are validated before archiving. Archive and payload hashes are retained.

Use `tools/testing/Test-Rc.ps1 -Version X.Y.Z -Final` to run the complete offline
suite against a final-labelled artifact, including packaging and installation
tests. This creates a new attended checklist; `-Final` does not mark it passed.

`dotnet test ArtOfSimRally.sln` runs no tests; the explicit executable runners in
the RC script are the automated evidence. Native/telemetry source suites live in
the toolkit. The optional SessionTools adapter now recreates recorded trajectories
in Unity; its live evidence is separate from this offline gate and physical acceptance.
Package it with `tools/package/session-tools.ps1 -Version X.Y.Z` from the same
clean source. Publish its ZIP/checksum as a separate optional asset; never add
probe dependencies to the player mod. Preserve original captures privately.

The optional controls setup helper is private and separate (sana 51994). Adding
`-PrivateStandaloneSetup -StandaloneSetupZip <pinned Wheelkit bundle ZIP>` to a
`-SharedInputCandidate` build also writes
`ArtOfSimRally-X.Y.Z-rc.N-shared-input-controls-setup.zip` and its checksum. It holds:
- the Wheelkit standalone bundle pinned in `tools/package/standalone-setup.json`,
  unmodified, under `ModSetup`;
- `Set Up Controls.bat` and its script, with `verify.ps1`;
- a `companion-manifest.json` that hashes every file.

It is a separate ZIP because UMM extracts every entry of a mod ZIP into `Mods`, so the
helper never reaches the game folder. The launcher finds the game with the installer's
own `Find-ArtOfRally` and needs the shared-input package installed; the helper itself
checks the installed candidate. The currently pinned companion is a private test
artifact, not a public release asset. Test it
offline with `tools/testing/Test-ControlsSetupCompanion.ps1 -PackageDirectory
<extracted mod ZIP> -CompanionDirectory <extracted companion>`. Moving `Find-ArtOfRally`
into `verify.ps1` changed the installer and verifier script bytes of later packages; the
installed mod files work the same, and published packages are unchanged.

Standalone installation and configuration are supported without Wheelkit: the
ordinary mod already supplies its installer and F6 setup, and the reusable helper
adds Controls, Screens and Settings for shared-profile packages. For an eventual
public package, retain this existing companion route and select its frozen helper
bundle in the normal release scope; no new configuration framework is needed.
Keep the companion separate from the UMM mod archive and retain the existing
exact-package tests. The first-launch requirement for the optional tabs and the
shared-axis calibration limit are in its packaged README.

The exact rc28 companion instead pins Wheelkit helper
`a8583c59feef9f5db318d316756f64043b40fa91`, helper archive SHA-256
`F3CF1345402CD31562B8A6112F440419DC79C6D28DDD276B4DBEDFEA084FC331`,
bundle manifest `E9D54FFF4597DE943A2D2449071DC5DE8600416B7C3DEB7D573A66801D94A702`.
The separate rc28 companion ZIP is `78C27DB0`; its complete hash and retained
PS5.1 PlanOnly continuation are in Local deployment. This is an immutable private
artifact; the following older pin and tests are historical, not rc28 qualification.

Historical October15 private helper adoption pinned Wheelkit
`44f7adbe5b816f402de1433a1eb6a2db428edcac`, ZIP
`StandaloneModSetup-private-44f7adbe5b81-win-x64.zip`, SHA-256
`d188c949b6d96dafaf669d0350382ec9f73704757618e38f6f751a30e6fd982f`,
bundle manifest
`B3DD30066BC1C2DA89D51EA4B75ECF3FEA5D0384073A45686062586C427D4009`,
527 payload files / 528 ZIP entries. Its combined focused Core batch passed
124/124 tests and its exact-ZIP private GUI/CLI checks include Art's pinned
candidate Preview/Apply/refusal. Evidence is retained in the portfolio hub under
`artifacts/shared-helper-44f7adb-20261015/run-01`.

This pin-only change does not rebuild Art. Preserve the qualified Mod SHA-256
`F98D3BDFC87BF5ED4D92D2CE872FD628B6ED479B9D3621A90D453EC1CCD81D5D`,
built from `11a7b6b058f8994410dbba9523558c0ca6342e94` (the `2c504af`
merge has identical `src/` and `tests/`). That DLL's ProductVersion is
`0.4.2-dev+unpackaged`; it does not identify this helper adoption commit.
`package.ps1` normally rebuilds and stamps a new Mod identity, so running its
full producer would require separate qualification of those new bytes. The
consumer companion delivery checks remain pending against the pinned helper;
the common helper checks above are not an Art release or installed-game test.

Shared-input delivery and the optional research shim probe are separate choices:

```powershell
# Normal shared-input Mod; still a private RC with pinned shim/native and candidate receipt.
./tools/package/package.ps1 -Version 0.4.2-rc.N -SharedInputCandidate -SharedInputShim <pinned-shim>
# Research only; explicitly includes the diagnostic probe types.
./tools/package/package.ps1 -Version 0.4.2-rc.N -SharedInputCandidate -ResearchShimProbe -SharedInputShim <pinned-shim>
```

The first produces `-shared-input.zip`, the second `-shared-input-probe.zip`,
with distinct stage and optional companion names. Both use the same pinned
payload/receipt checks; the shared candidate's `build.json` records `artShimProbe`
and the existing assembly metadata checker verifies the selected flavor.
`-ResearchShimProbe` without `-SharedInputCandidate` refuses before building.
Historical research recipes must add the explicit switch; old artifacts remain
immutable. A default package still contains no shared payload or research probe.

Both routes build newly stamped Mod bytes. Qualify and retain their actual
source/hash identities; neither is the unpackaged F98 DLL merely because its
runtime source matches. Focused checks use `tools/testing/Test-ShimPackageFlavors.ps1`
against both extracted packages. The 44E native pin is unchanged and remains
unlatched; package checks do not authorize an unattended game run or substitute
managed FFB Off for the DD301 load-time mute used in earlier runtime evidence.

The October 13 source/package notice audit of pinned helper `19a5897` found the
declared .NET, WPF-UI and CommunityToolkit notice/license files, Art and toolkit
adapter MIT texts, and Vortice/SharpGen copyright and MIT text in the helper's
`THIRD-PARTY-NOTICES.md`. Its owner-authored `MozaTelemetry.Core` 0.5.0 matches
the frozen `56FE5A71` dependency pin; the dashboard executable is excluded. The
bundle includes neither WheelFfb nor dbce_shim. No additional missing notice file
was identified in that declared set. Choosing public distribution for the helper
is separate from the mod's existing shared-shim provenance and runtime release
checks; it does not make Wheelkit installation a prerequisite.

## Channels

| Where | Notes |
|---|---|
| **GitHub releases** | Current distribution and source of truth; exact validated ZIP/checksum. |
| **Nexus Mods** | Planned distribution; UMM packaging. No current listing is claimed here. |
| **OverTake.gg** | Possible future distribution for the sim racing audience. |
| **Official Discord** | Possible announcement/support channel; posting is a separate authorized action. |
| **In-game CurseForge browser** | `ModManager` with `GameID 78103`. Content pipeline — cars, liveries, stages. Almost certainly will not accept a code mod; unverified. |

## Honesty in the release notes

Two things to state plainly, because both will otherwise generate complaints:

1. **Tested on one wheel.** Everything was developed against a MOZA R12 Base. The
   deadzone and steering findings should apply to any wheel Rewired does not
   recognise, which is likely every modern direct-drive base, but that is
   reasoning rather than testing. `FyReference` will need tuning per wheel.
2. **Leaderboards.** `DirectSteering` and `ZeroAxisDeadzone` restore what a
   recognised wheel already gets and are fair-play neutral. `DisableSteerAssist`
   genuinely changes driving aids and is off by default. Say so, so nobody enables
   it by accident and posts a time.
