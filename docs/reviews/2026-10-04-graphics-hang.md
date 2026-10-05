# Owner-reported graphics/input failure — 2026-10-04

At approximately 19:38 local (America/Chicago), the owner hard-restarted the PC
after finding a garbled screen and no keyboard/mouse control. The owner could
not identify whether a game or the desktop was visible. Do not attribute the
failure to a particular game or mod without additional evidence.

## Evidence collected after restart

- Windows reports boot time 19:38:41. Kernel-Power 41 records an unclean restart
  with BugcheckCode 0; this is not a diagnosed blue-screen stop.
- NVIDIA `nvlddmkm` event 153 occurred at 17:51:05 and 18:06:03, both containing
  `Error occurred on GPUID: 100`.
- Windows Error Reporting records two current-day LiveKernelEvent 141 reports,
  referencing `WATCHDOG-20261004-1751.dmp` and `WATCHDOG-20261004-1806.dmp`.
  Their post-reboot report times are not new failure times. Dumps have not been
  analyzed. Microsoft defines 0x141 as a display engine timeout:
  https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/bug-check-0x141---video-engine-timeout-detected
- The retained Woden log ends at 18:26:11. It records a developer request for
  7680x1440 FullScreenWindow, no local car ticks, and FFB Off/zero writes. This
  is context, not proof that the request caused the GPU errors.
- Art's 18:33 attempted launch and Woden's 18:35 attempted launch were awaiting
  Steam interstitial responses. The Windows control helper had already failed.
- At 19:43–19:45 all four games were closed. Art's saved Settings.xml and
  installed shipping DLL matched the pre-test protected hashes. No Art request,
  temporary Art request, or shared Woden/DRIVE/iRacing stage request remained.

Private evidence is under
`results/session-playback-development/incident-20261004-1938/`: event exports,
current-day WER reports, Woden log, Steam console and file hashes. It is not a
public release asset. No driver, display setting or owner tune was changed.

## Consequences for recording/playback work

Keep live game/display tests stopped while correlating this incident. Offline
builds and data checks can continue. Art RC10 and its optional SessionTools ZIP
are prepared, not released or live-qualified as the final artifact. Woden,
DRIVE and iRacing adapters remain uninstalled, offline candidates. The iRacing
repack.104 installer fixture was interrupted (child exit 1073807364), so it has
no complete passing result.

The earlier Art capture hangs must not be conclusively blamed on the added
native-effect observers: those hooks were withdrawn, but the overlapping GPU
timeouts provide another unresolved explanation. Preserve both failed captures
and the successful session-5/format-3 evidence separately.

## Post-reboot artifact recovery

At the next offline build, the newly generated shared core 0.2 DLL on disk was
35,328 zero bytes. Its SHA-256 was
`0E180F0DFE2D5F69DA5BB563E71BD387982C02A2D5A30D7BD40B18FFEA594021`.
It had been copied into the three uninstalled candidates after reboot. Their
compilers rejected it; no package containing it was installed or published.
The zeroed file is retained with the private incident evidence.

A full rebuild from clean shared source
`50b6b679e3c6e01025b6a50f8dd0f3eb85d9d4bb` restored the pre-restart hash
`2F987D570616742D259422324734F7BD3DC15D68B1359CF0154290A235E718A4` and passed
54 core assertions. Consumer pins were corrected and now check managed assembly
metadata as well as hashes. Art uses the separately pinned core 0.1 and was not
affected; both RC10 ZIP hashes and extracted optional analysis were rechecked.

## Later controlled resumption

The owner explicitly authorized resuming tests and attended the rig. RC10's
original session-5 replay was visually accepted; the exact final 0.4.0 package
then passed run 210753 with 13,455 poses and normal close/restoration. The final
launch used native 2560x1440 borderless presentation without an exclusive-mode
fallback. See [release evidence](2026-10-05-release-0.4.0.md). These observations
supersede the temporary live-test hold above, not the unresolved GPU diagnosis.
Do not claim the cause of the garbled desktop has been identified or repaired.
