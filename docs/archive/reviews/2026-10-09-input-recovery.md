# Input recovery after a device-list interruption

## Evidence

Private snapshot: `results/input-failure-20261009-201122` contains UMM, Player,
native FFB and Wheelkit logs plus current Settings/TripleScreen XML, the
automatic TripleScreen original and installed build identity.

Installed source was 914ea3a / 0.4.2-rc.3, native c8ec2ee / 901. At 19:55:39
local Wheelkit 0.6.8 applied only three screen measurements. Art launched at
19:57. Both nonexclusive and shared-FFB initial R12 reads succeeded. At 20:03:42
native enumeration omitted the R12; at 20:03:48 it returned, but all reopened
reader slots still used the retained FFB handle and returned no state. Shifter
and stalk reads continued. The session exited at 20:08:53.

The owner did not power-cycle the base. They plugged/unplugged a separate
camera on another hub. This does not establish why DirectInput omitted the
wheel or establish that the same cause explains an earlier failed control.
The recovery defect is independently visible in the code: native Ready tracks
initialization, while reader-only refresh keeps choosing the retained handle.

## Change

`WheelInput.DeviceReadHealth` reads the latest in-memory observation, using the
exact GUID. It performs no device calls. `FfbReadRecovery` requests recovery
after two seconds of failure and latches that request until stable healthy
reads or a new selected identity. Unknown/closed readers are not successful
reads. Requests pass through the existing five-attempt reconnect policy; it
releases old readers and the FFB handle before acquiring the selected device.
There is no reconnect while driving, assigning controls or unfocused. Saved
bindings, force tune, native payload and screen settings are unchanged.

## Validation and limits

- Production mod builds with warnings as errors.
- Lifecycle: 77 assertions, including short failure suppression, sustained
  outage, reader-close persistence, intermittent success, later independent
  outage, identity switch, invalid clock, focused/idle delay and retry bounds.
- WheelInput: 258 assertions, including exact identity, missing/closed/invalid
  identity, failed/resumed reads and no hardware call from the health query.
- Full package gates and installation receipt are recorded in LOCAL-DEPLOYMENT
  after completion. These initial tests do not establish hardware recovery.

Next attended check: fresh launch with saved controls intact, ordinary driving,
and recovery when paused after a naturally occurring input loss. Do not induce
a USB fault or issue unattended physical force merely to produce a passing test.
If input still fails, capture a support file before refreshing/restarting.

Owner follow-up, October 9: fresh rc4 launch at 20:30 read the R12 successfully
before and after FFB initialization. Owner answered **"Yes, controls respond"**
to the steering/throttle/handbrake check. The applied screen measurements and
saved controls were retained. This accepts fresh-launch control operation on
this rig; it does not qualify automatic recovery after another USB/device-list
interruption, nor establish the original USB cause.

Wheelkit's new shared-settings requirement is in its `docs/SETTINGS-PARITY.md`.
Control-profile application is pending; it must not be presented as complete
until the per-game binding adapter has been implemented and qualified.
