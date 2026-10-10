# Profile controls candidate

The owner requires Apply Rig Profile to include controls across modded and
native games. Routine testing must run without the owner: first complete the
configuration adapters, then provide functional recording/playback and a usable
reference for every active game. Pose playback alone does not verify bindings.

## Candidate behavior

Existing axis/shifter XML stays compatible. New fields are CameraSwitchBinding,
ConfirmBinding, BackBinding, StartBinding, NavUp/Down/Left/RightBinding and
TransmissionMode. They are exposed in F6 and written by Wheelkit's Art adapter;
there is no second profile file overwriting F6 on startup. Follow game is the
default transmission mode. An explicit mode overrides the game's option using
the same Drivetrain.automatic field, with that behavior stated in F6.

The optional `pov:index|angle|1|guid:instance` binding reads the existing native
0.9.1 module/slot through ReadDeviceStateWithPov. No new DLL load, device owner,
native pin or force-model change. The MIT source PovSnapshotReader is vendored
from toolkit **1f914b3**, `dotnet/Dbce.Wheel.Input/PovSnapshotReader.cs`, SHA-256
`8CE677D2D4AEFF2F5A33FAB9D2D6E0F4E1CD980C5FE67EE62B848A8A03871DCE`.
Absent export leaves hats unavailable; a failed coherent read never retries
through the old reader. All mapped values depend on successful reads.

New button edges are sampled once per watchdog frame, available to multiple
consumers without being consumed. Startup, failure, focus loss, pause and
editing require neutral before rearming. Unity focus/pause callbacks invalidate
even when Update stops. Missing/stale frames fail closed, with one warning.
New navigation binding capture rejects diagonals; held diagonal directions
activate their two adjacent cardinal bindings. The existing native controller
and keyboard bindings remain available.

Fixed game IDs (camera 61, pause 19, panel horizontal 14/back 17) and the
transmission override are gated on the inspected Assembly-CSharp SHA-256:
`7807A1D674C8214FE1FBD15B8EDC6E3E5E47700AB6E6C2943CF5BF234551C5E0`
(1.5.8b, Steam 17584229). An unknown assembly disables these new routes and
logs why. UI module action IDs are observed from the module itself. No Rewired
backend swap, OS input injection or focus manipulation.

Packages declare `controlsProfileSchema: 1` in build.json. Wheelkit uses that
installed capability for new fields, not just the newest catalog version.
Known older installs can receive their existing fields; unsupported actions
remain explicit and leave Apply history needing review.

## Evidence and remaining work

- WheelInput production source with fake raw device transport: **318 assertions**,
  including serialized hat/button bindings, all four directions, retained edges,
  unknown-game refusal, focus/failure/recovery and F6 binding persistence.
- Actual game's Unity Mono/Harmony: new Rewired seams attach/unpatch successfully
  without starting Unity or devices. This is method compatibility, not UI proof.
- Toolkit source reader: 25 fake-delegate checks; no physical input/force.
- Wheelkit's independent `art-controls` fixture executes real Apply and restore
  and compares complete expected XML for axes, camera, menu, shifter and
  transmission. It preserves unknown values, camera arrays and force enable.
- The first all-20-gate pass plus both original force-corpus cases is private at
  `results/rc-0.4.2-rc.5-9602f72831844e61ae682b5024398b0c`. It predates the review
  fixes and package capability marker; its dirty artifact is **not installed**.

Claude's source review (hcom 3215) led to diagonal-capture refusal, the exact game
assembly guard, transmission wording and stale-frame diagnostics. Final clean
candidate gates and live qualification must be recorded separately.

Clean candidate **0.4.2-rc.6** from **905d04bceea6b3b684ca91efd57abae3a3332d33**
passed all 20 gates and both original force-corpus cases at 2026-10-10 02:28 UTC.
Receipt: `results/rc-0.4.2-rc.6-23f795a6a09c46af9dc6d08f4035b4a8/automated.json`.
ZIP: `dist/ArtOfSimRally-0.4.2-rc.6.zip`, SHA-256
`647CCD0B2A8E5F8A2009FB88E3EFD5F68C5839358A710208C29D929DBEE2ABCD`.
This includes the review fixes and capability marker. It is **not installed or
published**; the owner's installed rc4/settings remain untouched.

Still open: real post-Apply cold launch/input observation, F6 render acceptance,
unfocused automated action qualification, new controlled USB/focus transitions,
and adapters for select/look-back/reset/horn/neutral/gears beyond Art's existing
six-gear shifter. Those profile actions are not silently reported as applied.
Software signal/trajectory replay remains distinct from physical feel.

## Installed October 10, 02:10 CT

The exact rc.6 artifact above is installed under the shared lease after an
owner-idle check. Every packaged payload and the second native DLL match;
eleven protected files, including the owner's settings/tune and development
probe, are byte-identical. Backup and verification receipt:
`results/profile-controls-rc6-install/`. No game launch, profile Apply or physical
output occurred. Lease released. Public release and the live checks above
remain separate; the older statement that rc.6 is not installed is superseded.

## Production Apply and original playback, October 10, 02:39-02:44 CT

Wheelkit `tools/Test-ProfileGame.ps1 -Replay` used its production configuration
service to write the owner's selected profile, independently verified the
written values, and carried those applied settings into a private session copy.
Launch hashes verified that the replay did not substitute the old tape's XML.
Camera button 32, confirm 31, back 18, start 35, profile POV navigation and
sequential transmission were among the applied settings.

The installed rc.6 played the original `2026-10-04-owner-session-5`: all 13,455
driving poses, zero pose-application error and zero gear mismatches. Skipped
rows were outside driving. The game closed normally; settings and automatic
backups restored exactly at `2026-10-10T07:44:39.8427525Z`. Shared lease and
owner-idle gate were used, wheel and motion outputs muted; no display override.

Private evidence is Wheelkit `artifacts/art-rc6-profile-live-20261010/`
(`preview.json`, `apply.json`, `launch-settings.json`, `qualification.json`,
`restored.json`, `runtime.log`, copied game log), plus this repo's
`results/sessions/2026-10-04-owner-session-5/replay-20261010-023939/`.
Launch Settings.xml SHA-256:
`60EFA75AB5363AFA980F91B6B9B1B7F073880ADEAA5E6A097C840A538746EE6D`;
TripleScreen.xml:
`13F0FD93B02D3457801D7720F33B94A9ABB3C967FF2FBCDF50214C6CCF4D17FC`.

This verifies the configuration write and the playback regression, **not**
camera/menu/pedal input delivery. The run's summary says `bindings preserved`;
read the preview/apply reports for unsupported actions. Wheelkit `0e82177`
subsequently preserves that detailed qualification report instead of replacing
it with the short runtime summary. Raw-input observation after Apply is the
next gate; a shared native test seam is under review. No physical acceptance
or public release is added by this run.
