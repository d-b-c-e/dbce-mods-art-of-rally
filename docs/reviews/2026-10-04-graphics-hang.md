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
