# Raw controls probe candidate

The earlier Art-only managed sample override was withdrawn without installation.
This developer probe uses the shared native test-injection ABI instead. It is
not included in the player mod. Installed rc.6 and its native libraries remain
unchanged; this source is not a passed input qualification.

Both `%LOCALAPPDATA%/dbce/art-of-rally/inject.on` and a cold-start
`%LOCALAPPDATA%/ArtOfSimRally/controls-request.txt` are required. The latter is
single-use and names a future expiry (at most five minutes), a GUID nonce in N
format, exact mod/settings/native SHA256 values, and a new absolute output
directory. Keys: expiresUtc, nonce, modSha256, settingsSha256, nativeSha256, out.
No session-tape recording or playback can run alongside it.

The probe keeps force/telemetry delivery and score output muted, blocks managed
force initialization, and refuses assignment and calibration capture for the
rest of the process. Native arming additionally requires the irreversible
no-force latch. It binds only the already resident reader module at FfbNative's
verified requested path: a missing, hash-mismatched or second loaded WheelFfb /
UnityForceFeedback module refuses the test. Module identity is rechecked during
the run. No native library is loaded by the probe.

Commands use the existing current-user named pipe:

```text
CONTROLS STATUS <nonce>
CONTROLS RAW <nonce> inject raw axis 5 dev={synthetic-example-guid} value=1743 ms=500
CONTROLS STOP <nonce>
```

The example needs a real test instance GUID. Native code owns command parsing,
raw substitution, expiry and delivery accounting. No applied binding is inverted
to manufacture the input. The harness must choose samples from the original
Wheelkit profile independently of the written file. Maximum test duration is
five minutes, 256 commands and 200,000 observation rows. Stop closes the normal
WheelInput readers, clearing pending native commands; the force fence and
assignment block remain until process exit. The first terminal cause is retained.

Evidence records command acceptance, actual last-read injection flag with raw
axes/buttons/hats, normalized mod values, actual Rewired getter results and
AxisCarController input, UI selection and camera rotation index. Camera selection
does not by itself prove a correctly rendered view. The trace does not declare
bindings passed automatically. It retains the game's normal focus/input guards.

Validation: probe builds with zero warnings; actual CLR/Harmony capture checks
pass (18); Unity's own Mono attaches the new observer/guards and executes the
assignment/calibration/force-open refusals without entering device or game APIs
(24 checks including existing collision/UI seams). These guards include methods
with Unity ECalls, so CLR alone cannot qualify their patch installation.
The vendored managed ABI client passes 79 fake-delegate checks each on net8/net48;
its precise candidate/source hash is in tools/testing/Recorder/TEST-INPUT-PIN.txt.

Still required before a live run: peer review of both ABI halves, native fake-COM
and legacy regression checks, exact temporary candidate installation of both
native copies under the lease, production Wheelkit Apply, independent raw
workload, observation classification, normal exit and byte-exact restoration.
No physical force may be enabled for that unattended qualification.

`Send-ControlsCommand.ps1` sends Status, Raw or Stop through the current-user
pipe. Supply the nonce, PID and exact UTC process start time captured at launch;
it also checks the executable path before sending and after the reply. Raw
commands are at most 512 printable ASCII bytes. Transport has one total deadline
and a 4 KiB reply bound. A timeout has an unknown outcome and is never retried
automatically. Command acceptance is separate from delivery and game response.
