# Recorder final capture extension

Source-only candidate for coordinator request 184414, based on Art `75e3a6d76b230c682cd9b2bb1973fc28ce4878bb`.
The retained Recorder DLL `002336926D48AAD037CD2EB9ABACF2BEEFA57A10B6D2BCC6E11782414A278962`
was built from `56c10043b3227b1964093e44749669dda9e887ca`; that Recorder subtree is identical at the selected base.
The product remains RC27/75e. This candidate changes the developer Recorder and its fixtures only.

The final render plan now schedules 18 render shots: Controls; two action positions;
Cameras top; all 11 keyboard/USB adjustment pairs; Advanced Cameras top; Help top and lower.
The Look forward shot focuses the following disclosure. Each camera pair focuses
the next keyboard row, with the reset button serving as the last pair's anchor.
Help only selects page 4 and scrolls; it does not activate report actions or links.
The existing `Add` settlement, screenshot-file wait, validated binding save, close,
and saved-state restoration remain in use. No foreground or input framework is added.

There are 24 total steps and 23 shots, including the existing unshot `bindings-ready` step.
The exact ordered successor `ExpectedShots` array is
[`final-ui-expected-shots.json`](../../tools/testing/Recorder/fixtures/final-ui-expected-shots.json).
The last pair retains `final-cameras-lower`, so the parent's camera-disclosure assertion remains applicable.
Kiki owns parent04 and must compose this array with the newly built Recorder's exact
path, hash and source commit. Original03, its inputs and results remain immutable.
No successor inputs are frozen by this source change.

## Checks and limits

`python tools/testing/Recorder/Check-FinalUiSource.py` checks product camera count,
row relationships and ID anchors, render-only navigation, production plan consumption,
and the 23-name manifest. This is static source evidence, not C# execution.
The authored focused C# fixture executes the production plan and checks its queue,
full camera-table coverage, next-row anchors, Help navigation, and manifest order:

```powershell
dotnet run --project tests/Recorder/Recorder.csproj -c Release -- --ui-final-plan .
```

Do not run it or compile the Recorder until the coordinator names a build slot.
No compiled or screenshot acceptance is claimed here. Focus requests can only
establish the intended scroll target; actual pixels must confirm that each complete
pair and the Look forward value/device lines fit at the runtime viewport and scale.
Advanced numeric pose rows, native glyphs, and actual action delivery retain their
separate acceptance checks. Screenshots cannot replace those checks.

## Build-only handoff

Use this isolated worktree and its committed source, one worker, one coordinator slot.
The only distributable to build is the developer Recorder; do not rebuild the product,
run InstallRecorder.ps1, install into the owner copy, or launch a game.
The new worktree lacks the ignored `lib/umm` references. Before compiling, copy only
the following two references from
`E:/art-ui-research/rc26-targeted-observer-build-20261016-01/source/lib/umm/`
into this worktree's ignored `lib/umm/`, verifying SHA-256 before and after:

| Reference | SHA-256 |
| --- | --- |
| UnityModManager.dll | `63E5BAF7B1738E4091B5FD17CCB738ECDB4D1DBF246061DFD55DC52835D52691` |
| 0Harmony.dll | `DF043F05C6E43602FA8E8F38F52A3A2CF962AAB7A65A9D85573EA25F01A63E80` |

The default GameDir is `D:/Program Files (x86)/Steam/steamapps/common/artofrally`;
read its managed references only. Retain the tracked Playback DLL
`BC297CC206090761EA09E4E9119F59959F90CE9F03C0C52F3FFBBFB425213BDD`.
Record the source commit and dependency hashes in the slot receipt.
The bounded batch compiles/runs only the focused queue fixture, then builds the
Recorder once; stop on failure and retain logs. Suggested commands from the worktree:

```powershell
dotnet build tests/Recorder/Recorder.csproj -c Release --nologo -warnaserror -m:1 -nr:false -p:UseSharedCompilation=false
dotnet tests/Recorder/bin/Release/net8.0/Recorder.dll --ui-final-plan .
dotnet build tools/testing/Recorder/Recorder.csproj -c Release --nologo -warnaserror -m:1 -nr:false -p:UseSharedCompilation=false
```

Capture the resulting `tools/testing/Recorder/bin/Release/net48/ArtOfSimRally.DevRecorder.dll`
hash and fixture output; hand those and the exact manifest to Kiki for parent04 composition.
Release the build lease after processes exit. A runtime attempt requires its own named handoff.

The existing adoption ledger remains unchanged. A read-only standards comparison
found STD-026/028/029/032 absent from its text, alongside its existing partial/unchecked
entries; this Recorder-only change makes no new toolkit-adoption claim.
