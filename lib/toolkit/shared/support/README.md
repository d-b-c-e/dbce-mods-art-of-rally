# Shared problem reports

## PowerShell hosts

`SupportReport.ps1` is a lazy adapter over the exact adjacent `SupportReport.cs`.
Vendor both files and the toolkit MIT license, with commit and SHA-256 provenance.
It uses Windows PowerShell 5.1's existing Add-Type compiler; no runtime installation
or Wheelkit assembly is required. Compilation occurs only on a report action.
The compiled assembly carries the source fingerprint; repeat import reuses the
same source, while a conflicting loaded component refuses with a reopen message.

The wrapper adds no redaction, log-reader, export or browser-budget implementation.
Core owns those rules and returns its immutable PreparedReport directly. Metadata
and explicitly selected local log paths are host inputs. PowerShell functions
return browser submission data only; the host handles deliberate browser opening.
The host must show the redacted Preview and best-effort warning, tie review to
that snapshot, and explicitly save the one report attachment for long drafts.
No clipboard, automatic upload, broad support ZIP or automatic log selection is
added. UNC paths are refused before report IO. UI links/labels remain in each game.

The affected Gravel `Test-SetupUx.ps1 -HelpOnly` fixture qualifies the wrapper and
inert host actions against Windows PowerShell; existing C# fixtures remain the
behavioral reference and their unchanged passing results are retained. Source
preparation alone does not establish compilation or rendered Help acceptance.

Windows C++17 hosts use the source counterpart in
[`native/support`](../../native/support/README.md), with the same host review,
selection and browser handoff contract below. It has no managed-runtime or UI
dependency; native IO/report qualification is recorded with that component.

`SupportReport.cs` is the single portable C# 7.3 implementation for standalone
mod Help tabs and Wheelkit's report adapter. Vendor the exact file and toolkit
MIT license with source commit/SHA-256 provenance. No Unity, loader, WPF,
Wheelkit installation, HTTP client, token, clipboard or process API is required.
The initial reader/redaction/composition behavior comes from Wheelkit Core's
`Services/ProblemReport.cs`; authenticated submission stays in Wheelkit.

Host contract:

1. Package `ReportProduct(game, version, owner/repository, optionalClient)` with
   the product. Never run git or discover issue destinations on player PCs.
   Every mod Help tab offers **Submit GitHub Issue**, **Mod GitHub** and
   **Wheelkit GitHub**. The descriptor supplies validated `ModPage` and
   `WheelkitPage` HTTPS links (Wheelkit defaults to `d-b-c-e/dbce-wheelkit`);
   hosts open them only on the corresponding deliberate link action.
2. Let the player describe the problem and explicitly choose optional relevant
   logs. Standalone hosts offer known mod/BepInEx/Unity text logs only. Do not
   feed existing broad support ZIPs, settings, bindings, saves, recordings or
   whole directories into this helper. The extension allowlist is a text-format
   check, not proof that a file is an appropriate log. Wheelkit retains its
   explicit text-file picker. No files are selected automatically by this API.
3. Call `Prepare(product, title, description, selectedPaths)` on an explicit
   Review action. Show `PreparedReport.Preview`, including its redacted title.
   Maximum five logs share a 24,000-byte tail budget (plus tiny BOM/header reads).
   UTF-8 and BOM-marked UTF-16 are supported. Partial first lines are discarded;
   invalid text/binary controls and linked paths are refused. A read failure
   fails preparation; do not silently omit a selected log or show an old preview.
4. Any input/selection change clears both the prepared snapshot and review
   checkbox. Show best-effort redaction wording; unusual secrets can remain.
   The prepared snapshot never rereads a file. Review is tied to that object.
5. On the deliberate reviewed report action, use
   `prepared.CreateBrowserSubmission(reviewed)`. Open its URL through the host's
   browser action. Opening transmits the prefilled issue text to GitHub; it does
   not create an issue. Show `Instructions`. When `NeedsAttachment`, explicitly
   save this snapshot with `WriteReviewFile(newTxtPath)`, show its location and
   have the player attach that one file in the issue box. Attaching uploads
   immediately; the player then checks and submits the issue on GitHub.

The short draft includes the complete body. The long draft keeps a brief
description and setup, names the one report attachment, and preserves the full
report locally. The 4,096-character encoded URL budget is a conservative local
policy, not a promised GitHub/browser maximum. An unusually expensive encoded
title can require shortening and another review. Errors should be shown without
logging raw report content. Existing files are never overwritten; output
directories must already exist. No directory is created while preparing.

Redaction covers the current home path, Windows user paths, GitHub token forms,
labeled credentials (including quoted JSON keys) and email addresses. This is
not a security sandbox against concurrent filesystem replacement or a guarantee
that arbitrary personal data is removed. The player must review the text.

## Integration survey, October 15

All four Help tabs currently have a local support-file action, with different
collection scopes. Keep those actions separate from selected-log reporting:

| Host | Packaged issue metadata candidate | Known log source candidates |
| --- | --- | --- |
| Art | `d-b-c-e/dbce-mods-art-of-rally` | Existing `SupportLogs`/mod/Unity text log paths supplied by host |
| DRIVE | `d-b-c-e/dbce-mods-drive-rally` | `BepInEx/LogOutput.log` |
| iRacing | `d-b-c-e/dbce-mods-iracing-arcade` | `BepInEx/LogOutput.log`, local `IRacingArcadeWheel/ffb.log` |
| Woden | `d-b-c-e/dbce-mods-woden-rally-edge` | Host-provided BepInEx log; existing `CreateSupport` is a status export, not a log |

These are source observations for adapter authors, not runtime discovery rules
or proof those files exist on a player's installation. Coordinator confirmed
through GitHub API on October 15 that DRIVE's old `drive-rally-wheel` origin
redirects to `dbce-mods-drive-rally`, with issues enabled (currently private).
Per-game Help wiring is coordinated
separately; this component alone does not add buttons to the four mods.

## Validation and references

Focused fixture source: `tests/SupportReport`. It targets C# 7.3 and covers
redaction, issue routing/escaping, snapshot review gating, no-overwrite export,
UTF-8/UTF-16 tails, binary refusal, selected-log limits and useful long-draft
fallback. **Not compiled or executed during the owner's October 15 UAT.**
Syntax parsing is not a build, test pass or rendered UI acceptance. Schedule
the focused fixture and affected Wheelkit tests/build in a fresh rig/build slot.
The initial component and fixture source were parsed with the PowerShell host's
Roslyn 5.0 parser using `CSharp7_3`: zero syntax errors. Project XML parsed.
No compilation, semantic binding, fixture execution or game/UI run occurred.

GitHub documents [issue URL title/body parameters](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-an-issue)
and [supported attachments and immediate upload timing](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files).
No attachment query parameter or automatic upload behavior is assumed.

## Legacy CLR 2 / .NET 3.5 hosts, October 16

Compile the same source with `DBCE_SUPPORT_NET35` for HCT's legacy host.
That opt-in uses `IOException` for invalid report data (Unity Mono2 lacks
`InvalidDataException`), `IList<string>` for selected log paths, `USERPROFILE` for
home redaction, and a character whitespace check. It uses the legacy regex
overload on fixed helper patterns with a 50,000-character input bound because
CLR 2 lacks timeout overloads. Modern builds retain their existing
`IReadOnlyList<string>` API, user-profile discovery and one-second regex timeout.
No file discovery, automatic selection, sending or overwrite behavior changes.

Product `20b269fe164cf14184bd5cc7f1313a07bc7bc1ba` passed the existing
C# 7.3 report fixture on .NET 8 (47 checks), and compiled against the installed
CLR 2 mscorlib/System plus .NET 3.5 System.Core and ran on CLR 2 (50 checks).
The legacy additions cover collection compatibility, default home redaction,
whitespace refusal and the bounded legacy regex input. Both compiles passed
with warnings treated as errors. Source SHA-256:
`BC89C07A873F9C42B3E840550415447ED7EE55FA2E02CAD344E345FBE7715B56`.

Original evidence: hub `artifacts/support-net35-lola-20261016/run-03`.
The earlier runner setup failure has an unknown build outcome; its separate
recovery closure and a contention-only refusal are preserved in `run-01/02`.
The passing batch closed all six owned process identities, restored its process
environment, and released its exact lease at `2026-10-17T00:33:16.9685425Z`.
These checks do not qualify a rendered HCT Help panel or game behavior.

### Stripped Unity Mono2 follow-up

HCT's actual game System.dll does not define `InvalidDataException`, despite the
full desktop CLR2/3.5 target above passing. Source `969a1092bfd0930350893ae77e0a74edb568f632`
uses `IOException` only under `DBCE_SUPPORT_NET35`; modern exception/API behavior
is unchanged. Exact shared source SHA-256 is
`16C9F7D0EBAF82C388B958CDC7AA4A453F2922453FA62218AE0DDD3B704FD9C3`.

The same fixture and shared source compiled with warnings as errors against the
three shipped HCT framework assemblies (mscorlib/System/System.Core). The game's
existing `Check-Net35Api.ps1` passed its assembly-qualified type/member-name
checks, then the fixture ran 50 checks under Windows CLR2. This establishes
shipped-reference compilation and API inspection plus that desktop execution;
it does not establish Unity game loading or rendered Help behavior.
Original evidence: hub `artifacts/support-unity35-lola-20261016/run-01`.
Four owned identities closed and the exact lease released with safe queries,
quiescent children and environment restoration at `2026-10-17T00:54:07.7451202Z`.
The earlier desktop passes and game-API failure are retained as distinct results.
