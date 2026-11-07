# Owner session playback: returning from a custom rally

The October 13 owner tape `20261013-004100-65e93dc2` reached the finish on the
installed rc.18 mod and shared-HUD recorder. The first cold replay played all
10,484 driving rows with zero detected pose application error and no gear
mismatches. It returned `partial:post-stage-divergence`, not a full-session pass:
the expected `MainMenu|ResultsScreen|-` never appeared. Game exit was normal,
the owner environment was restored, and recording/tool seals were unchanged.

Evidence is private at the hub's `artifacts/art-owner-replay-20261013/run-01`
and `run-01-wrapper`. The original tape is retained unchanged in the Wheelkit
Art recording library. This is trajectory playback, not deterministic physics
or a physical force measurement.

## Cause and correction

The sealed `load-0001.json` targets MainMenu (scene 3, CUSTOM) and contains a
non-null default Season with `Rallies: []`. `ScenePlayback` intentionally does
not capture a season for menu loads, but Unity's inline serialization emits a
default object for that null field. Playback tested only for non-null and
overwrote the completed live event with the empty placeholder. The first
`RallyManager.RallyData` index exception follows that return-to-menu load.

The installed Assembly-CSharp has SHA-256
`7807a1d674c8214fe1fbd15b8edc6e3e5e47700ab6e6c2943cf5bf234551c5e0`, MVID
`08d2bf0d-3873-4a77-9007-4b3bccb7fb01`. Static REA identity evidence
`ev_acfb7145927c122362d4c1e8ffd6042d157390be23284de7c7cca58d0bccccea`
and focused ILSpy output confirm its CUSTOM `RallyData` getter indexes the
season's Rallies list. The runtime stack and sealed menu JSON establish the
actual failing path; static inspection alone is not runtime evidence.

Restore now uses the same stage eligibility as capture: scene 0 and scene 3,
and NULL game mode, never restore a season. The recorded scene and mode checks
remain in place, and the menu target still applies. Stage loads still restore
the recorded selection. Existing sealed tapes need no modification or recapture.

The SessionTape fixture compiles the production adapter against engine fakes
and exercises the empty menu object, valid stage restoration, NULL mode,
mode/build refusal before mutation, and ignored fading loads. All 28 assertions
pass; the net48 recorder builds with zero warnings/errors. Nene's independent
source review found no blocker.

## Corrected cold replay passed

At 04:37-04:41 CT, optional SessionTools `0.4.0-rc.19` built from `2237b54`
replayed the unchanged owner tape through ResultsScreen, RallyCompleteScreen,
and ExitGame. The result is **passed**, with all 10,484 driving rows played,
10,511 applied poses, zero detected pose error and zero gear mismatches. The
53 unused rows are non-driving tail rows; this is not a claim of frame-exact
menu timing. The game closed normally, the owner environment was restored,
and both source tape and tool seals stayed identical.

Private evidence: `artifacts/art-owner-replay-20261013/run-02` and its wrapper
receipt. The center HWND was selected by its observed rectangle for WGC;
`center-01.png` shows the route, normal camera and shared playback banner.
Unity ScreenCapture images instead show a side view in this configuration and
must not be used to infer that the center HUD is missing.

Installed recorder SHA-256:
`CD0F82BD0146BFC4795658CA7531733F91A61DA9BDE1D79D9054EFF0482FDD73`.
SessionTools ZIP SHA-256:
`023E7D70D6B2CD67690281A278856BC7BEFE92DE489D04AF2AF2F4A27D9F1C41`.
The ordinary mod, native/bridge/shim binaries and force tune were unchanged.

## Recorder indicator check

At 05:00 CT a bounded synthetic recording with physical inputs isolated and
all outputs muted showed the shared **REC 00:02 | F11 to stop and save | Outputs
muted** banner at the top of the observed center window. The image was inspected
at `artifacts/art-hud-20261013/visible-02/recording.png`. The companion Stop
command sealed the tape, the game closed normally, and the owner environment
was restored. This verifies visibility and the stop/save path; physical F11
was not pressed. The synthetic tape is not an owner force-normalization drive.

The brief Saved image was not captured: the older compiled WGC helper supported
single images but rejected the newer source's burst arguments. It remains a
visual follow-up on the next purposeful Art run, not a reason to repeat the
unchanged route now. A preceding observer attempt also hit the recorder's live
event-log lock; its game closed and restored normally. Both harness failures
are retained in the private artifacts and are not product playback failures.
