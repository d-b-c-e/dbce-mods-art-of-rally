# DBC-259: unidentified-controller F6 explanation

Local source candidate, based on release HEAD e2b08f3. Not installed or runtime-qualified.

## Reconciliation

Main's dirty shared-input/settings/package/version files have no content differences with git diff --ignore-space-at-eol. CustomVehicles/SESSION.md has 21 unrelated added lines. No dirty files were copied, reset or stashed. Isolated branch egon/art-f6-refusal-dbc259 lives at E:/Source/games/.worktrees/egon-art-f6-dbc259. Retained handoff 12b69aa is documentation only; its KI-52 capture prerequisite stays on DBC-28.

## Behavior

NativeInputIsolation refuses an empty Rewired instance identity before changing joystick flags. Its existing reason flows through SharedInputStartup.Refuse and Lines to SharedInputStatus. Only Phase.Refused with the exact production prefix `native isolation unqualified: Rewired joystick identity unavailable:` receives three lines, in compact and detailed F6:

> Shared profile was not used for this session because a controller could not be identified. This prevents duplicate wheel input. Art bindings are used.
>
> Restart the game with that controller disconnected. If you use Steam Input, disable it for art of rally before restarting; Steam Input may be the cause.
>
> Details: [original qualification reason]

This follows the documented workaround in docs/LOCAL-DEPLOYMENT.md. Other refusals keep their existing advice. CleanupFailed never claims Art bindings are restored and keeps its existing restore warning. No identity admission, isolation, binding, acquisition, SharedInputStartup state transition, shared toolkit or Wheelkit behavior changed. Exact prefix matching deliberately avoids inferring a cause from arbitrary device names or other failures; changing the producer wording later requires updating this presentation mapping.

## Focused checks

Existing tests/ShimInput production-source fixture: Release net8.0 and net48 both passed 12,354 assertions. Presentation and production-isolation checks cover session fallback, conditional workaround/restart, original diagnostic, compact parity, unrelated refusal and cleanup-failure warning. Existing identity/isolation cases remain in this suite. git diff --check passed.

Initial restore failed because this run environment omitted Windows ProgramFiles(x86)/ProgramData. Supplying those standard paths in the child shell resolved restore; no persistent machine configuration changed.

Release mod build completed on 2026-10-26 with zero warnings/errors and warnings treated as errors. No package build, install, launch, rig/input, rendered F6 or physical device checks performed. The fixture verifies pure status presentation and existing offline identity logic, not Unity/Mono runtime or layout. No public push. Settings, Art bindings and working install preserved.

## Review and QA

Darlene reviewed the exact quoted copy in DBC-260; it was applied verbatim. Sandy granted bounded OFFLINE ADMISSION in DBC-261 for source 5579db95c341ac294df6996e561634e968ac0ae5. This does not authorize runtime or establish user readiness. Future runtime evidence must show this reason on actual F6, readable wrapping at normal/enlarged scale, unchanged fallback bindings, and no Steam Input advice for unrelated failures. Current issue restrictions prohibit performing that runtime here.

Read-only standards comparison: STD-011/012/013/014/016/018/019 remain unchecked; existing partial rows remain open. STD-037/038/039/040 have no adoption rows in this release baseline. This narrow explanation follows STD-040 plain-language intent without claiming full parity/adoption or editing shared standards.

## Admitted source and build provenance

Source: 5579db95c341ac294df6996e561634e968ac0ae5; clean worktree at build start. Documentation reconciliation after this build changes no compiled source.

The existing IsolationTests fixture obtains the wrapped reason from production NativeInputIsolation.Check, reproduces the startup period suffix, and checks compact/detailed presentation, preserved Details, unrelated production refusal and CleanupFailed exclusion. Unity SharedInputStartup is not executed. Prior Release net8.0/net48 results of 12,354 assertions each are retained, not rerun in this documentation/build continuation.

Build: dotnet build src/ArtOfSimRally.Mod/ArtOfSimRally.Mod.csproj -c Release -warnaserror -p:SourceRevisionId=5579db95c341ac294df6996e561634e968ac0ae5 -p:BuildSourceState=clean -p:ArtShimProbe=false

Normal inert probe flavour; version 0.4.3, informational source pinned above. Local game/UMM references are build-only and not deliverables. ProgramData and ProgramFiles(x86) were supplied only to the child shell. No source or machine configuration changed.

Output payload SHA-256 (bin/Release; build outputs only, not a validated release package):

- ArtOfSimRally.Mod.dll: CEB6607C4B9E11F98F96531A3540F2332D9CCC8A3C9C27784A89478196222AFF
- Dbce.TripleScreen.Core.dll: C878FCC903D17E0A27375DA601976240A89BD57A6F90BDA08E9BA52B172A33E5
- Dbce.TripleScreen.Protocol.dll: AF60296AA41105D1B94169330986B28BDA916D15E20CFB17F638FC2E1D4961E5
- Dbce.Wheel.Ffb.dll: 6FC2BE9197D846A477C84E81D39DD420B324BD4B79B30CD54BB78567E270A72B
- Dbce.Wheel.Telemetry.dll: 293D09062B7B1FA9FCB7D48B70A6D8F8BFF2F22577EE90487786800F8D76373F

Runtime remains separately authorized: compact/detailed refusal, normal/enlarged wrapping, preserved Details, fallback bindings after cleanup, unrelated refusal and CleanupFailed warning. Requalify the game-owned capture route against current pins; serialize the existing lease, mute physical outputs, record focus/window/devices/resources and owner-state changes, restore exact owner state and verify owned-process closure. No delivered-input, driving or feel claim; rc.30 acceptance is unchanged; KI-52 stays DBC-28.

## Private offline package — 2026-10-26

Package source 57d504ecf5d8a9c63a7fd4198c9fbce70510874d (clean): admitted behavior 5579db95 + dd2391e handoff + byte-preservation correction only. No compiled C# changed after admission.

Concrete packaging defect: Git LF normalization changed two vendored XML files and shim/build-receipt.json. Restoring CRLF reproduced all three existing manifest SHA-256 pins exactly. Narrow -text attributes retain upstream bytes; no pins, binaries or shared toolkit source changed.

Commands: tools/package/package.ps1 -Version 0.4.3-rc.260 -SharedInputCandidate; Expand-Archive to a unique temporary directory; Assert-Payload on that extraction; tools/testing/Test-Installer.ps1 -PackageDirectory <temporary extraction>.

PASS: Release build zero warnings/errors (-warnaserror), ArtShimProbe=false; existing release assembly/dependency check 364 assertions; complete 27-entry extracted manifest verified; isolated installer 70 assertions. Installer evidence: results/installer-ef593f929a7f4b4aacaf33e43c44ba4b. No game or DLL execution by installer fixtures.

ZIP ArtOfSimRally-0.4.3-rc.260-shared-input.zip (2,108,787 bytes): SHA256 507A899F241932C29BE5B80DC60C9EF7496C126B52106FD0D44AC1A32D1B9163. Packaged mod B59F9B96D28DB653E667121F795FF1776A6D63181C937F4AAA36D0F0F19C4930 differs from prior build solely through package build identity parameters. Identity: 0.4.3-rc.260+57d504ecf5d8a9c63a7fd4198c9fbce70510874d.clean.

Toolkit v0.16.0: managed v0.15.0 bytes retained; native bbbf6e3/44E46799945438BC07DF738472602DB1E3ECC04C3F77CAD5B12FCACF49C69272; shim 421bd4b/BC9B030A25834D8CCE562F22588EC5E0D3F5C1FA026AD7AD984776D60CD6254B. All output hashes follow in the extracted manifest below.

Earlier local rc.259 is retained as superseded dirty-source packaging evidence, not the handoff candidate. Initial incorrect 0.4.2 version was refused before output. First commit attempt failed because injected Git identity was empty; rc.260 was built only after the correction committed cleanly.

Private package uploaded to DBC-259 attachment 939deca5-7a86-435f-99eb-c3d1213ebfd9. Darlene text review and Sandy exact behavior-source offline admission remain retained; package is ready for bounded package review, not user-ready. Runtime authorization card remains pending. No real install, launch, rig/input, KI-52 change, shared Wheelkit edit or public push. Owner rc.30 acceptance unchanged.

### Package manifest

```json
{
  "schema": 1,
  "release": "0.4.3-rc.260",
  "files": {
    "ArtOfSimRally/ArtOfSimRally.Mod.dll": "B59F9B96D28DB653E667121F795FF1776A6D63181C937F4AAA36D0F0F19C4930",
    "ArtOfSimRally/Dbce.Wheel.Telemetry.dll": "293D09062B7B1FA9FCB7D48B70A6D8F8BFF2F22577EE90487786800F8D76373F",
    "ArtOfSimRally/Dbce.Wheel.Ffb.dll": "6FC2BE9197D846A477C84E81D39DD420B324BD4B79B30CD54BB78567E270A72B",
    "ArtOfSimRally/UnityForceFeedback.dll": "44E46799945438BC07DF738472602DB1E3ECC04C3F77CAD5B12FCACF49C69272",
    "ArtOfSimRally/Dbce.TripleScreen.Core.dll": "4939D408AE053BBC866DC50224687D2C7FAD43D670737B0B488BF1FBB2C8D89B",
    "ArtOfSimRally/Dbce.TripleScreen.Protocol.dll": "0CE174559E92CA8E3FBEDFF3C6B29BE87D1548A0C7C49A72F5C533096D1AF2FB",
    "ArtOfSimRally/Info.json": "AB6F930CEC092300EBA53F7DD638E03E6440DE80300B1742F4C0E17FFBEE9107",
    "ArtOfSimRally/build.json": "7E81DAC54A2C2FC5199C9DCC3E473D89CBE9F673747EB02CC15B38D408515045",
    "LICENSE": "B2212C476244C444D6B7E804AC89C038A88B5C9288A087B30A2343D2DC3021DB",
    "README.txt": "C1AA7AA6CF9C1433A968C9D1ABED494CC8202F5157B8E6B43AB8EDD397F9ADE9",
    "Install.bat": "31903B840CB387188F4F943D9729935B61684BDEFF787B6FBB28740DAC7974D7",
    "Uninstall.bat": "46C042D835D8F5C56DC19C956308E3F1CDF8BBC004AC2EEA185A1C394D4050F8",
    "install.ps1": "5A4485AF33FE725E6CFDAD81954691335BC9687C47576F4E5F61CF664F84C580",
    "verify.ps1": "D30627BF46FC32F87A73F4608D707C7505724E5A5E4918D8C80A4AC876C1E987",
    "UnityModManager/winhttp.dll": "8C6CDBC38836DEE87E3368F5DE1994D7C0CCEBF29E4CE7ABA3C0981F9375412C",
    "UnityModManager/doorstop_config.ini": "4F0DE4444B8577E9FF173AAC7E70161ACBC9AACE87BE39181341B8754C102B68",
    "UnityModManager/UnityModManager.dll": "63E5BAF7B1738E4091B5FD17CCB738ECDB4D1DBF246061DFD55DC52835D52691",
    "UnityModManager/0Harmony.dll": "DF043F05C6E43602FA8E8F38F52A3A2CF962AAB7A65A9D85573EA25F01A63E80",
    "UnityModManager/dnlib.dll": "54C7968CFBDDA0E9FDDFA0F844D2DBEF3BB3A5A4D2D7462D5413EAC3FD0614CA",
    "UnityModManager/Config.xml": "8A959494C3821A34F72DEF2AF763E044637AF221E9FC94EACF70AFA89775A49E",
    "UnityModManager/Params.xml": "7B8491EFA228C4050226D0FA78372DA7529B2686096C3769FCA9B000A5CBB951",
    "UnityModManager/PROVENANCE.md": "725D035EA27B2696FBDC6DEF8C81DFF020A90F219AD8AB47E515540F17C76A95",
    "UnityModManager/licenses/UnityModManager-MIT.txt": "ED767DF421B56A786DB759CBDC7343B635E3F572BB18742F1EB4D0E11D83BEDD",
    "UnityModManager/licenses/UnityDoorstop-LGPL-2.1.txt": "20C17D8B8C48A600800DFD14F95D5CB9FF47066A9641DDEAB48DC54AEC96E331",
    "UnityModManager/licenses/Harmony-MIT.txt": "A5883D0C45EB7E176D3D1631220E7F96E958E37064773D40245AE6DB560BBD6D",
    "UnityModManager/licenses/dnlib-MIT.txt": "420D7871726A5010B8376D4DD6F0E8708CEF41ECE119FDE1CF2A889B57541412",
    "ArtOfSimRally/dbce_shim.dll": "BC9B030A25834D8CCE562F22588EC5E0D3F5C1FA026AD7AD984776D60CD6254B"
  }
}

```

