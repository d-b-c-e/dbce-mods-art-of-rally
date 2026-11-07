# Local deployment

## Current installation - Art 0.4.4-rc.43 (shared input), November 7

UI-4 build (source `8f53874`): adds the developer-only `[Dev] UiHost=hook` F6 (docs/UI-HOST-HOOK.md); the player
default is unchanged and the ZIP's file list equals rc.42's. Installed with the package `install.ps1` over rc.42 with
the game closed; settings and `dbce-controls.ini` stayed byte-identical and all payload hashes match.
ZIP SHA-256 `FDCF10F27A950E7E204212005C28BB1401FE9BD53ACE0C25A0C4AE3102A25905`, installed Mod `785B26D1F2926F3E372D77B0D46C41EBA902C0E70C8CF9A2A0D1A5027CA89461`.
Rollback copy of rc.42 (mod folder, UMM folder, saves, registry): `E:/Source/temp/art-0.4.4-rc.43/launch-20261107-110613`.
Launch-checked 2026-11-07 11:06 through Steam with force feedback muted: the default build (three-view, no dev file)
logged no UiHost line and its capture passed Test-GamePicture on all three screens. A second run with the dev switch in
single-screen mode drew the core F6 in the game's frame; see docs/UI-HOST-HOOK.md. Settings, saves and registry were
restored; ArtUserState reported only the expected `remotecache.vdf` difference.

## Previous installation - Art 0.4.4-rc.42 (shared input), November 7

Cleanup build (ART-1, source `73d5757`): the shim probe flavour, InputBackend, the companion ZIP and the
qualification runners are deleted; player behavior is unchanged. Installed with the package `install.ps1` over
rc40 with the game closed; settings and `dbce-controls.ini` stayed byte-identical and all payload hashes match.
ZIP SHA-256 `C5C819D74CED0DC413773CE371CE29482F2FD8BCF719054A55F88B47A80948E7`, installed Mod `BE6AEEE9E0C1B4EBBA5568497608AEDA0148E06A556122DE3864F13AE955E68F`.
The ZIP file list equals rc40/rc41 (plain and shared-input); `build.json` no longer has `artShimProbe`.
Rollback copy of rc40 (mod folder, Plugins native, saves, registry): `E:/Source/temp/art-0.4.4-rc.42/backup-installed-rc.40`.
Launch-checked 2026-11-07 05:07 with force feedback muted: the UMM log showed `Patches applied.` and `Active.`, both
wheel devices opened, and the game was still running at the menu stage before it was closed by PID. Steam was signed
out (owner signed in elsewhere), so the exe was started directly with a temporary `steam_appid.txt`, removed afterwards.
Saves, registry and mod settings were restored byte-identical. CV 0.6.7 is unchanged.

## Previous installation - Art0.4.4-rc.40 + CV0.6.7, November5

File-only installed from clean package source
`4daff2175b5af16891e2d11955ed9180babfd5ea`. Retains rc39 mounted/chase changes
and adds CV per-car/set pose offsets, shared FOV offsets, measured-seat/reset
scope, exact legacy backup/migration, pending-save retry and safe imported-key
consumer behavior. See [CV cockpit persistence](../experiments/CustomVehicles/COCKPIT-PERSISTENCE.md).

| Artifact | SHA-256 |
|---|---|
| Art rc40 shared-input ZIP | `DF004857E9A08FCE91E489B96A11247431E6F4AB97F34EB0A47811AF4E011F39` |
| Installed Art Mod | `9EB7184937B41BD0828A9AA6BC32B6664AC72562DDA563CD70888774EBD7FD84` |
| Installed Art build.json | `7066CB3CECDD487ACB9DCA19D0B069767F1BB1EFA5B7F79243B001C30EA3E73A` |
| CV0.6.7 ZIP | `32D1D5AFE17F150CC87A225AE077955BB06A80151F72BCF281E7B1A98C93542D` |
| Installed CV plugin | `48822A55CA82CA4642D34E426F95AF95A91730D4DA2AC5070FD10EB0E8CAC0F2` |
| Installed CV Info.json | `62E7C598591F8FA6730C016723211372764A68C1FF2D71A5F6CE33965F037D69` |
| Unchanged shim | `7E4A1251AC733E34A393E28EA3453F3ED409C8356158FF82ADD4242348A0A9A1` |
| Both unchanged native force copies | `44E46799945438BC07DF738472602DB1E3ECC04C3F77CAD5B12FCACF49C69272` |

CPU527449 PASS20.2847s: old6cf01f2 compiled then3 named behavior REDs; current
159 cockpit/148 Core/57 Harmony targets, actual upload/local callbacks and
locked physical-default guards passed. Plugin net48 compile0 warnings/errors.
CPU527546 PASS35.6633s: actual Art and CV producers;379 metadata/70 installer/
5 creator tests,27 Art and14 CV archive entries read back. Unchanged optional
Blender omitted. All636pins/certs exact;603-image census48 identities absent,
scoped0; exact95659bcad8134a27a2c2101338c89969 released07:44:05.6479464Z.

Standing install527612 admitted fresh1793864862794 once: rawidle9347.437s before
lease and9348.203s after, Art closed/Steamapp0. Six writes only: Art Mod/Core/
Protocol/build and CV plugin/Info. Eleven payload hashes match; separate-process
65 security descriptors/original backups,59 protected bytes/creation/write ticks/
raw attributes and6 rollback guards/reviewed body passed. Entire installed CV
Vehicles and State, docs/cache, Art settings/calibration,24 saves, shim and both
native force copies remain exact. No legacy State rewrite occurs on installation.
603-image independent census found operator42544 absent/scoped0; exact
5a5a1b4932214e3a9be1a4ef11f76e1a lease released2026-11-05T07:47:59.2624630Z,
named/token absence corroborated. No custody. Standing approval stopped after
this first admitted run.

Outside backup and guarded rollback:
`E:/Source/_archive/2026-11-05/owner-before-art-cv-cockpit-rc40/Rollback.ps1`,
actualSHA `8B2231AE35E49D7E51BB8962D90DADD2ED040D8382E04AC349FBE4EA7CC354BE`.
Preview46DB312A has the same executable body with one less trailing newline;
rollback preserves later changed files and restores original bytes/timestamps/
attributes/security. Reviewed/parser checked, not exercised against owner files.

Evidence in `E:/Source/_work/napo-art-cv-cockpit-20261105`:
`obj/cv-cockpit-install-20261105/{install-1793864862794,independent-owner-verify,result,independent-audit,closure}.json`.
Result `0AD1B5969631946F7EE1453637001AA89B7F1B6125C138E8EC1D472AACE9020E`;
audit `3FB2847D5823DA2CDF097F9AC2BB117A25EA96A04C3F58FC68B245308B53DB1E`;
ownerproof `27BFB5BE4465A3F6106E487B6B4FDF84E4FA6DE1B2C6295981E623EB135D0C63`.
Adjacent cv-cockpit-20261105 and cv-cockpit-package-20261105 preserve all source,
package, baseline regression and raw logs; archives stay at their recorded paths.

No game, real keyboard, UI, device, rendered, physical or new owner acceptance.
Native Unity callback/geometry/view-index/stage/restart/seating gates remain;
RuntimeQualified=false. Perspective/curve stay ready_for_test for owner re-check
on rc40/CV0.6.7. Preserve rc30 historical driving/force/performance acceptance.
No full STD-046 adoption, public push or release.

## Historical installation — 0.4.4-rc.39, November 5

File-only installed from clean local-main package source
`8f67b511bcf068571a64b4d9bd75b98c275417d9`. Adds stock chase pose offsets per
car/view/set, calculated individual F6 Defaults and real mounted-store coverage.
The mounted store preserves the last edit across target/set/settings/save changes,
late body/seat recalculation and same-donor custom-package ID changes. Chase FOV
remains shared per view/set across cars. See
[camera persistence](2026-11-05-camera-persistence.md) for exact scope and limits.

| Artifact | SHA-256 |
|---|---|
| `ArtOfSimRally-0.4.4-rc.39-shared-input.zip` | `E8351B57BF1C780BD2E62F073CDE440312E7949099EF17F082EBF0143CA9F8E5` |
| Installed Mod DLL | `3D4221FFEEE8C3DE6F3F17998AA443460AD2C475EC6479BAD1F6189842B21A37` |
| Installed `build.json` | `31773C6EDFD6EF142522DF72FA25729FC9243240FB23EBAE62E9DD5AFF761857` |
| Unchanged shared shim | `7E4A1251AC733E34A393E28EA3453F3ED409C8356158FF82ADD4242348A0A9A1` |
| Both unchanged native force copies | `44E46799945438BC07DF738472602DB1E3ECC04C3F77CAD5B12FCACF49C69272` |

CPU526948 retained four actual-old runtime regression failures, then passed
CameraTuning1,056, SettingsUi3,565, Lifecycle95 and full mod compile0 warnings/
errors. The production mounted store replaces the old Reset double; synthetic
scene/input/geometry remains explicit. CPU526985 passed the actual producer,
379 metadata checks,27-entry archive readback and70 real installer checks, without
repeating the source suites. Source/public push remains local only.

Standing526985 admitted fresh install1793861915431 once, rawidle6400.859s,
Art closed/Steam app0. Four writes only (Mod/Core/Protocol/build.json); all nine
installed package hashes match. Separate-process verification passed51 original
backup hashes/security descriptors,47 protected bytes/creation/write ticks/attrs
and four rollback guards. This covers24 save files and four CV State files;
settings, calibration, native and shim were unchanged. Independent586-image
census found the installer absent/scoped0; exact68eb3694c46a48148165ae6e9e5b219c
lease released2026-11-05T06:58:50.2950667Z, absence corroborated. No custody.
Standing approval stopped after this first admitted run.

Backup/one-step guarded rollback:
`E:/Source/_archive/2026-11-05/owner-before-art-camera-persistence-rc39/Rollback.ps1`,
actual SHA `8B2231AE35E49D7E51BB8962D90DADD2ED040D8382E04AC349FBE4EA7CC354BE`.
Reviewed preview46DB312A has the same executable body with one less trailing
newline byte. Guarded rollback restores original bytes/timestamps/attributes/
security and refuses later changed targets; not exercised against owner files.

Evidence in `E:/Source/_work/napo-art-camera-persistence-20261105`:
`obj/camera-persistence-install-20261105/{install-1793861915431,independent-owner-verify,result,independent-audit,closure}.json`.
Result `80D5737C12F9882B22A6A9B8B9D8F9219A1D2110EE8DEF82C73D979910135370`;
audit `9CBA1E79F87A7553CD9087B6F7C401F5E98D1E5DF03F11A10AC0D7A7EA07CBAB`.
Adjacent camera-persistence-20261105 and camera-persistence-package-20261105
folders retain source/package receipts and raw logs.

No game/UI/input/device/rendered/physical/owner acceptance run. Perspective and
curve remain ready_for_test with owner re-check required on rc.39. Historical
rc.30 driving/force/performance acceptance remains preserved. Custom Vehicles'
separate absolute/bounded cockpit store/imported-F7 dispatch remains open;
native geometry/indices/UI/callbacks/stage/restart behavior still needs runtime.
No full STD-046 adoption or public release is claimed.

## Historical installation — 0.4.4-rc.38, November 5

File-only installed from clean local-main package source
`53399f616bb5fd32a629ec424d060b2a6a0c03b9`. Fixed Art F7 reservation covers
capture and imported keyboard dispatch/reset while preserving saved bindings
and independent wheel channels. Mounted/chase tweaks retain their stored values;
only the final lens is fenced1..179degrees. Camera +/- editing can exceed the
initial slider span. Chase consumes one wheel-reset edge with capture/focus
suppression. Cockpit Custom compares the actual Game/Realistic FOV baseline.

| Artifact | SHA-256 |
|---|---|
| `ArtOfSimRally-0.4.4-rc.38-shared-input.zip` | `E08D682BDA91B76446DBD80A53F5A4E83778741FA08354160B96E66677834000` |
| Installed Mod DLL | `45CB3C5DF223EB78203D8238D2B55E9FA72A0DA829F89D9863A687E47826CD3A` |
| Installed `build.json` | `065CFB79BD1ADD609EB2865C7EA9C0B60045AAC1D0947AFB221D6F15466E962E` |
| Unchanged shared shim | `7E4A1251AC733E34A393E28EA3453F3ED409C8356158FF82ADD4242348A0A9A1` |
| Both unchanged native force copies | `44E46799945438BC07DF738472602DB1E3ECC04C3F77CAD5B12FCACF49C69272` |

Four actual-old regression modes failed as expected; current CameraTuning481,
TripleScreen1,304, metadata inspector372 and real isolated installer70 checks
passed. The first package batch526705 produced the exact ZIP but failed its
wrapper's phase-counter scope check; raw FAIL preserved. Installer-only526743
qualified that retained archive without rebuilding or repeating passed checks.

Install526782 admitted on raw idle4080.734s, replaced only Mod/Core/Protocol/
build.json, and verified all nine installed package payloads. Separate-process
verification confirmed51 original backup hashes,47 protected-file bytes/creation/
write timestamps/attributes,51 security descriptors and four rollback guards.
This includes24 save files and four Custom Vehicles State files, plus all mod
settings and native force copies. Calibrated numbers and owner settings were
not rewritten. Independent587-image census found the installer absent and zero
scoped survivors; exact lease released2026-11-05T06:20:08.9373524Z. No custody.

Backup and one-step guarded rollback:
`E:/Source/_archive/2026-11-05/owner-before-art-std046-rc38/Rollback.ps1`, actual SHA
`8B2231AE35E49D7E51BB8962D90DADD2ED040D8382E04AC349FBE4EA7CC354BE`.
Its reviewed body is text-exact with the frozen preview46DB312A; the raw hashes
differ by one trailing newline byte from Set-Content. Actual parser0; rollback
was not exercised against owner files. It refuses changed targets/permissions,
then restores original bytes, timestamps, attributes and security descriptors.

Evidence in `E:/Source/_work/napo-art-std046-fixes-20261104`:
`obj/std046-install-20261105/{install-526782,independent-owner-verify,result,independent-audit,closure}.json`.
Owner proof `C56A96A05F5E523E0512667BA35F1C55E371BB7193DD9184902CA8EDBCCA0D0B`;
result `06B40052558B341ED3C381670D519E8884B90ADBF445A4F1F89BB47349FCFE0A`.
Package and source checks are in the adjacent std046-package-20261105 and
std046-qualification-20261104 folders. Standing install approval stopped after
this first admitted run. PUBLIC repository remains local only; no push/release.

No game/UI/input/device/rendered/physical/owner acceptance test accompanied this
installation. Perspective/curve remain ready_for_test and need owner re-check
on this new candidate; earlier rc.30 driving/force/performance acceptance remains
preserved. Real mounted persistence coverage, chase pose offsets, Custom Vehicles
offset storage/F7 dispatch and remaining STD-046 gaps stay explicit in
[the source increment](2026-11-04-std046-source-fixes.md).

## Historical installation — 0.4.4-rc.37, November 4

File-only installed from clean package source
`0d9c0808d521286b8002816317ba54a5cb1f52f0`. This candidate restores an owned
saved mounted camera after late rig/target/player readiness, retries legacy held
H-pattern gears after a refused/reset shift, and retains absolute H-pattern gates
through startup/reacquisition in the exact installed 31-action shared Core.
Stock/foreign camera ownership and sequential/menu/paddle release guards remain.

| Artifact | SHA-256 |
|---|---|
| `ArtOfSimRally-0.4.4-rc.37-shared-input.zip` | `859F6A097340E50175C50E9D2C30271C37ADCA3202178D5C0A378A83BDCABDE6` |
| Installed Mod DLL | `79FC4995EB99018087CCE46A53E7B999AC898CA420E439D39831E1E98B53F75B` |
| Installed shared shim | `7E4A1251AC733E34A393E28EA3453F3ED409C8356158FF82ADD4242348A0A9A1` |
| Installed `build.json` | `01BD50C889594197DDBFD43CA173CC1A81BDA8DADF91204E85C205415B834DA8` |
| Both unchanged native force copies | `44E46799945438BC07DF738472602DB1E3ECC04C3F77CAD5B12FCACF49C69272` |

Retained qualification: 507 Art consumer + 679 native + 140 exact-DLL managed +
70 isolated real-installer assertions = **1,396 checks**. The real package producer
regenerated the receipt, including the formerly stale nested controls identity.
All nine installed package payloads match the retained archive. Six files changed;
the whole 22-file mod folder was backed up. Seventeen protected files retained
bytes, creation/write timestamps and attributes, and all 24 captured security
descriptors match. Atomic Info.json replacement changed its owner SID; the
captured owner was restored and independently checked before release.

Backup and guarded one-step rollback:
`E:/Source/_archive/2026-11-04/owner-before-art-stage-gates-074254237/Rollback.ps1`
(SHA-256 `3DAD9298AD87FBD02239FD8DD9BB151CD69FBE4CB27F2D89E50003E293B11A59`).
The rollback also verifies and restores the captured security descriptors; it has
been parser-checked, not executed against the installed candidate.

Evidence: `bld/stage-start-qualification/owner-install/independent-readback.json`
(`1D1198B5969DF1D1B32364747D355179117735949CDA1564A764428AAC5557D8`),
the adjacent install receipt, and
[stage/held-gate qualification](2026-11-03-stage-camera-gates.md).
No game, UI or device run occurred. Stage/restart rendering, late Custom Vehicles
cockpit restoration, physical held shifter behavior and owner acceptance remain
open. STD-042/043 remain partial. Historical entries below describe earlier copies.

## Current file-only successor, November 3

The owner-directed sweep installed a905fe79 Mod DLL
`C2C358228C02D1EAE964B542C50DA4127DF5F3E48475D656369DF8BF93FDE727`
and build.json, backed up at
`E:/Source/_archive/2026-11-03/owner-before-artofrally-040825444` with manifest
and guarded Rollback.ps1. The installed DLL was independently confirmed again
November4. Settings and existing native/shim payloads were preserved; no game,
force or owner acceptance accompanied this file-only update. Earlier installed
rc.36 paragraphs below are historical checkpoints.

The current build.json nested controls assembly hash still names E5368292;
the final qualified stage-readiness/held-gate package must regenerate it through
the real producer. The new successor is not installed: its actual hook/legacy
consumer checks total507 GREEN assertions, while the exact31-action shared-Core
successor awaits native/DLL/ABI qualification and combined production build.
See [current work](2026-11-03-stage-camera-gates.md).

## Camera source successor, November 2 (not installed)

ISS-0024: after adding mounted views, refresh an available owned saved camera
once through the native method. Lifecycle95/CameraTuning408/SettingsUi3549 and
net48 build pass; no stage/runtime or owner acceptance is claimed. ISS-0023's
Wheelkit UI already exists in current source; installed UI/Apply and cross-game
gates remain open. [Camera issue handoff](2026-11-02-camera-issues.md).
Installed rc.36 below is unchanged.

## Source successor, November 2 (not installed)

Single-element saves now preserve original XML bytes outside the owned scalar;
SettingsUi 3549/Regression 478849 assertions and production net48 build pass.
Source 1502abe was pushed with owner approval; no owner-file change. Details and evidence:
[element-save handoff](2026-11-02-element-save-preservation.md).
The installed rc.36 identity below remains unchanged.

## Current installation — 0.4.4-rc.36 (November 1, owner test)

rc.35 plus the cab-over bonnet camera on the roof's front edge and the bumper camera at most 45 cm up (`4ea8b21`; owner:
in the test view the camera sat between the van's front corners). ZIP SHA-256
`0B42775FC7382DBC6E76DC3D5C7618D736BCA125C9DBA831323E4F4A24928C47`; Mod.dll
`E5368292772994DBBDEDF729F2A2337E44D629CD8A7943E8D42DE2CBCF7D8CE3`. Installer 70, CameraTuning 408 passed. Game closed,
lease vemi; settings unchanged. Rollback copy of rc.35: `E:\Source\temp\art-0.4.4-rc.36\backup-installed-rc.35`.

## Previous installation — 0.4.4-rc.35 (November 1, owner test)

rc.34 minus the separate-windows mode (`54c77eb`, -1,309 lines): one window across the monitors is the only triple
presentation; a saved EnableSeparateDisplayPrototype=true turns into the span on load; F6 is one on/off toggle. Curved
span path unchanged (one presenter over three thirds). Full `Test-Rc.ps1` gate passed (rc.35 label), Triple 1303,
SettingsUi 3540, Lifecycle 80, CameraTuning 407, Installer 70. ZIP SHA-256
`C4775008844A05C9711045EA0DD9778AE7BB68004507F022CBE697C71C569DC6`; Mod.dll
`F139D8AD37C5F2DDEB930D6A40522728D0C4C55438DFD79565D322901AED5417`. Game closed, lease vemi; payload 9/9; owner
settings byte-identical. Rollback copy of rc.34: `E:\Source\temp\art-0.4.4-rc.35\backup-installed-rc.34`. Custom
Vehicles 0.6.6 unchanged. Player docs (docs/triple/SETUP.md, README, TROUBLESHOOTING) still mention Separate monitors.

## Previous installation — 0.4.4-rc.34 + Custom Vehicles 0.6.6 (November 1, owner test)

Owner 11-01 on rc.33: spanning window "in the right direction". rc.34 (`43ffcb9`): plain-colour camera backgrounds go
black while menus are centred (grey splash side bars). Custom Vehicles 0.6.6: the car's particle effects are hidden
in the cockpit view only (spray/mist smearing inside the van's cab). ZIP SHA-256
`D86CFA9F4AA0CC7FD99FF2D563725052A6BD58852B9EAC26234B19DD6B9224B2`; Mod.dll
`B9B856BF06773AF1642569CBEA782CD232FFAEBA5071E4B7A0A95764110C8C8A`. Installer 70, Triple 1356, SettingsUi 3572, CV 148
passed. Game closed, lease vemi; payloads 9/9, 13/13; settings byte-identical. Rollback: rc.33 at
`E:\Source\temp\art-0.4.4-rc.34\backup-installed-rc.33`, CV 0.6.5 at `E:\Source\temp\customvehicles-0.6.6\backup-installed-0.6.5`.

## Previous installation — 0.4.4-rc.33 (November 1, owner test: spanning window)

rc.32 plus the span window (`0b6f821`): span mode on separate monitors makes one borderless 7680x1440 window at the
racing row's origin (AC/CSP style) instead of side windows. TripleScreen.xml switched for the test:
EnableThreeViewPrototype true, EnableSeparateDisplayPrototype false (before:
`E:\Source\temp\art-0.4.4-rc.33\TripleScreen.before-span.xml`; restore it to go back to separate windows). ZIP SHA-256
`E1730EAF71B2E7F6A1D829920554917D3F2BC4E3AB908994C98923AA49774A4D`; Mod.dll
`D456059076D4F1299A18795514ADCE490F7DB5525C3F34DC70BF2E9F188249FB`. Installer 70, Triple 1356, SettingsUi 3572 passed.
Game closed, lease vemi; payload 9/9; Settings.xml byte-identical. Rollback copy of rc.32:
`E:\Source\temp\art-0.4.4-rc.33\backup-installed-rc.32`. Log lines: "Span window: ...".

## Previous installation — 0.4.4-rc.32 (November 1, owner test)

rc.31 plus numpad + zooms in and - zooms out (`81efa1c`; owner: "+/- are swapped"); untouched key sets migrate on load.
ZIP SHA-256 `B3AFFD8AB8525BBBA98345C2DBF2E59719C00D6A5C02F130FF6CB945ABF2DBA1`; Mod.dll
`FD1F2150FB79475493358F5AB355793A5E6AE72BFB686B82D99A03647840A28C`. Installer 70, CameraTuning 407, Triple 1356,
SettingsUi 3572 passed. Game closed, lease vemi; payload 9/9; owner settings byte-identical (the key migration
happens in game). Rollback copy of rc.31: `E:\Source\temp\art-0.4.4-rc.32\backup-installed-rc.31`. Custom Vehicles
0.6.4 unchanged (it reads Art's keys).


Custom Vehicles 0.6.5 (`207874f`): logs the body size beside the donor's at attach and warns past 15%. dll
`E237B67FA74FF9452B1DC597A3EA6CFB6B16E70634DB11CA59F8814AE42EDC90`; 13/13; State files unchanged; 0.6.4 backup at
`E:\Source\temp\customvehicles-0.6.5\backup-installed-0.6.4`.
## Previous installation — 0.4.4-rc.31 + Custom Vehicles 0.6.4 (November 1, owner test)

The van's "Realistic" cockpit was saved at 47.6° (seat-true 33.6°), then reset by the old numpad 5 to the 65° Game
default; that, not the projection, bent the dashboard at the seams. rc.31 (`f891337`): the cockpit label shows
"(Custom)" when the Realistic set is not seat-true; Custom Vehicles 0.6.4: reset returns Realistic to the seat FOV.
The van's Realistic cockpit FOV was set back to 33.6 in `State/cockpit-views.json` (before:
`E:\Source\temp\customvehicles-0.6.4\cockpit-views.before-seat-reset.json`). ZIP SHA-256
`F03BF5CE38794AC74E60587DACC7CFE90D4AE9BB61CD644C2FE1CFE4EE8F5464`; Mod.dll
`094E589EA03678131C3A7DD1D7B7F0AB8977FC323E54DEFEB5DC1EC04E5F20C7`; CV dll
`7856EE8F4C6CE62A45AEF0371DAC1EA332F626039CE7789707DD0396EB287FCB`. Installer 70, Triple 1356, SettingsUi 3572, CV
148 passed. Game closed, lease vemi; payloads 9/9 and 13/13; owner settings byte-identical. Rollback: rc.30 at
`E:\Source\temp\art-0.4.4-rc.31\backup-installed-rc.30`, CV 0.6.3 at `E:\Source\temp\customvehicles-0.6.4\backup-installed-0.6.3`.

## Previous installation — 0.4.4-rc.30 (November 1, owner test)

Owner report 11-01 (Turtle Van cockpit, Realistic): dash and roof bar jump at the seams in every Shift+F7 mode, unlike
Assetto Corsa. AC's `triple_screen.ini` puts the center screen at 0.660 m; ours subtracted the 1500R sagitta (42 mm).
rc.30 (`05a3291`, toolkit `74c5eda`): eye distance is to the plane through the panel edges. ZIP SHA-256
`894C5BB42E3E7E94CF09A3F232009F3DF53DA470B766884CF029E86C3721F384`; Mod.dll
`F89ADCDF48DD5DC3534A0A503977126AB21B5F7B5423FEBCEFC73FF0019ACF29`. Installer 70, Triple 1356, toolkit core 25 passed.
Game closed, lease vemi; payload 9/9; owner settings byte-identical. Rollback copy of rc.29:
`E:\Source\temp\art-0.4.4-rc.30\backup-installed-rc.29`.

## Previous installation — 0.4.4-rc.29 (November 1, owner test)

Owner screenshot 11-01: the Turtle Van's bonnet camera inside the cab (measurement refused at 4.15 m tall; vertex
sampling read its flat roof as a hole). rc.29 (`6021eab`): custom bodies sliced by their triangles, windscreen found
walking back from the front, cab-over handling, height limit 4.5 m. Camera settings were cleared again before this
drive (backup `E:\Source\temp\art-camera-reset-20261101b`). ZIP SHA-256
`30EE083568C3E677ED33B279E0A1CB3199CC922697257B93F34FAC30BABE4DA6`; Mod.dll
`D0491D130998D6550A112A95F3CACA1088FA03A9F54FF1DA89A491ED4F94B92F`. Installer 70, CameraTuning 403 passed. Game closed,
lease vemi; payload 9/9; owner settings byte-identical. Rollback copy of rc.28:
`E:\Source\temp\art-0.4.4-rc.29\backup-installed-rc.28`.

## Previous installation — 0.4.4-rc.28 (November 1, owner test)

rc.27 plus the bumper view starting Realistic on triples (`a56c9e4`; owner decision after the Assetto Corsa comparison,
docs/CAMERA.md "Triple-screen camera defaults"). ZIP SHA-256
`37E25D5311BBE724DB9AEFA2C67914F44733251EA3E689A761941439C5F53FC7`; Mod.dll
`A0A9CAF42A165BC4A729CEF5C2CB1AF208E67DEF5598B19668FCE27EDEB240EC`. Installer 70, SettingsUi 3572 passed. Game closed,
lease vemi; payload 9/9; owner settings byte-identical. Rollback copy of rc.27:
`E:\Source\temp\art-0.4.4-rc.28\backup-installed-rc.27`. Custom Vehicles 0.6.3 unchanged.

Full `tools/testing/Test-Rc.ps1` gate passes again at `28c711d` (run labelled rc.29; product source identical to rc.28,
only tests/docs changed): Lifecycle stubs caught up, WheelInput shim tests default to the toolkit pin. Results
`results/rc-0.4.4-rc.29-4b758fcd1b364b00ae340718cdb85dba`; attended checks pending.

## Previous installation — 0.4.4-rc.27 + Custom Vehicles 0.6.3 (November 1, owner test)

rc.27 (source `c39acee`): camera tweaks have no position limits (`d5d6e5c`); the bumper view hides the car, shadow kept;
bonnet/bumper rotation smoothed over 0.06 s (`900edec`); F7 remembered per camera: OverrideFieldOfView = bonnet, new
BumperRealistic/ChaseRealistic (`c39acee`; lili told for ISS-0023). ZIP SHA-256
`58C4FA33F94D32315509E33EEF154B709287AED6E3A507B54DDA78E1C5FD5AEC`; Mod.dll
`99D59DE6C80BA7D1251F0081FE58F0572067652E8D6BC8D127CC7C8DEB24A408`. Installer 70, CameraTuning 400, SettingsUi 3572,
Triple 1355 passed. Custom Vehicles 0.6.3 (cockpit rotation smoothing; offline build 148 assertions, 57 Harmony targets),
dll `00EF64D4C3F0A2BFFA4355ED495CD4F04096A43BE3F13E21E21857C2F6CA06CE`, 13/13, State files unchanged. Game closed,
lease vemi; payload 9/9; owner settings byte-identical. Rollback: rc.26 at
`E:\Source\temp\art-0.4.4-rc.27\backup-installed-rc.26`; CV 0.6.2 at `E:\Source\temp\customvehicles-0.6.3\backup-installed-0.6.2`.

## Previous installation — 0.4.4-rc.26 (November 1, owner test)

Owner screenshots 11-01 (Eibenhammer, camera settings cleared): the calculated bonnet camera sat inside the car (the
front-quarter median took the low sloping nose, -0.10 m, for the bonnet), and after adjusting, the light pods were cut
away. rc.26: windscreen found by its steepness from the roof forward, profile logged (`2d25a79`); bonnet/bumper views
draw from 5 cm near clip, stock value restored on release (`b6388fd`). ZIP SHA-256
`5B12BA2656B58FC3B1E87B29451A39A721485006F74DEEF11E5A12D1FDF4E83C`; Mod.dll
`14E40615172C0873BC3B93E9CE7E1926323AA81E9BC197BDF047BA7DD8C77773`. Installer 70, CameraTuning 400, Triple 1355 passed.
Game closed, lease vemi; payload 9/9; owner settings byte-identical. Rollback copy of rc.25:
`E:\Source\temp\art-0.4.4-rc.26\backup-installed-rc.25`. Owner camera settings before the 11-01 clear:
`E:\Source\temp\art-camera-reset-20261101`.

## Previous installation — 0.4.4-rc.25 (November 1, owner test)

Owner design 11-01 (4792659): bonnet/bumper start from a placement calculated from each car's shape (bonnet 10 cm ahead
of the windscreen base, 8 cm above the bonnet; bumper centre line, 5 cm ahead of the front, halfway up the front face),
the same for both sets; tweaks are offsets (position per car and set, FOV per camera and set for all cars); cameras kept
outside the body; "(Custom)" label; numpad 5 resets. Reference-car fit removed; adjusted sets migrate (position kept,
FOV from stock). ZIP SHA-256 `23304D18A849077267C6039FD5D675E8690A72D9957D81CD2A9CA1C263041C26`; Mod.dll
`F4704FEECF23D7E08F1A90C2F5F11D2BC44F6DB47D93F593D2A99C75FDDB59F2`. Installer 70, CameraTuning 398, SettingsUi 3540,
Triple 1355, shared-input startup 14 and drive trial 608 passed (the last two with `ART_SHIM_DLL`/`ART_LATCHED_SHIM_DLL`
set to the shipped `dbce_shim.dll` BC9B030A: their default evidence paths under `E:\Source\games\.worktrees` and
`E:\Source\research\.worktrees` were cleaned). The full `Test-Rc.ps1` gate does not build: the Lifecycle test's stubs
lag rc.12+ (ForceFeedbackEnabled, FeedbackOffNotice, Triple, ChaseFov, CameraMountMemory). Game closed, lease vemi;
payload 9/9; owner settings byte-identical. Rollback copy of rc.24: `E:\Source\temp\art-0.4.4-rc.25\backup-installed-rc.24`.

## Previous installation — 0.4.4-rc.24 (November 1, owner test)

rc.23 plus: camera fit measures each mesh's own (local) box (3a7ebd4; world-aligned bounds had made yawed cars 5.5-7 m
wide); F7 on every camera with cockpit starting Realistic (`CockpitRealistic`, default true) and bonnet/bumper/chase
starting Game, chase views gaining a Realistic set (07bc9b2). The owner's tuned Eibenhammer sets (Game bonnet 0.31 m
high / 1.02 m forward; Realistic 0.29 / 0.48) gain their measured shape on the next drive and become the fit reference.
ZIP SHA-256 `80C799F41CDE772597E0A6C80075487D9A3A1459F2AE6B95D9F0934981765CC1`; Mod.dll
`857F39E1CC4AA0E44CCAC12BBFC95921D64E52E80D2CBBBB3A8D1226BF3FA381`. Installer 70, Triple 1355, SettingsUi 3476,
CameraTuning 399 passed. Game closed, lease vemi; payload 9/9; owner settings byte-identical. Rollback copy of rc.23:
`E:\Source\temp\art-0.4.4-rc.24\backup-installed-rc.23`. Custom Vehicles 0.6.2 (d81a424, cockpit follows the cockpit
choice) installed, 13/13; 0.6.1 backup at `E:\Source\temp\customvehicles-0.6.2\backup-installed-0.6.1`.

## Previous installation — 0.4.4-rc.23 (November 1, owner test)

Owner report 11-01 (Eibenhammer, Custom Rally): bonnet camera near the ground, F7 no change, 1st held at the start not
engaged. rc.22 (`2ba82f2`, ZIP `B09FB3DA…C823`): camera fit uses body mesh colliders only, refuses non-car measurements
(the car had measured 5.71 m wide, bonnet below it), refits untuned sets, starts the Game set from the defaults when no
reference (the Game set had inherited the Turtle Van's Realistic values, so both sets were identical). rc.23 (`0ea8a71`):
the log showed the game already in 1st and the shared input reading the held 1st as neutral; the mod no longer shifts
out of gear on a first neutral reading. ZIP SHA-256 `FA9D4826607D61E457C29A77CE1AA55830F6704FEBD37BFF5F7DA1ADFC366A34`;
Mod.dll `BC4E2FCF54B862ECC79128F8A7671F9076C940C104A64DFFB58667D5E84395B7`. Installer 70, ShimInput 12360,
CameraTuning 399, SettingsUi 3444, WheelInput 620 passed. Game closed, lease vemi; payload 9/9; owner settings
byte-identical. Rollback copies: rc.21 at `E:\Source\temp\art-0.4.4-rc.22\backup-installed-rc.21`, rc.22 at
`E:\Source\temp\art-0.4.4-rc.23\backup-installed-rc.22`.

## Previous installation — 0.4.4-rc.21 (November 1, owner test)

guna GO 488089, file-only, no launch. rc.20 plus the `artFovSets` receipt marker in Info.json (e94836e) that Wheelkit
admission needs (dbce-wheelkit docs/ART-STANDARDS-FOR-WHEELKIT.md). ZIP SHA-256
`7C3B2D989F1C47207DFF438473C6DCFCE9E05D5F557A10652C5A409579996B0D`; Mod.dll
`7C653F3EADA3D7BD509050D20202E3854A1BFB04B1025EA87F05DB0BFBB7B8B7`. Installer 70. Game closed, lease vemi held and
released; payload 9/9; Settings.xml, TripleScreen.xml, dbce-controls.ini, Params.xml byte-identical. Rollback copy of
rc.20: `E:\Source\temp\art-0.4.4-rc.21\backup-installed-rc.20`. Custom Vehicles 0.6.1 (b85ba66: the cockpit uses Art's
camera keys) installed alongside, 13/13; 0.6.0 backup at `E:\Source\temp\customvehicles-0.6.1\backup-installed-0.6.0`.

## Previous installation — 0.4.4-rc.20 (October 31, owner test)

rc.19 plus (82adf86): the camera fit measures the body only (the Turtle Van had measured 7.09 m wide); start-of-stage
log lines for the H-pattern lever and gearbox ("H-pattern at stage start: ...") and for each camera change
("Camera: ... game's saved view N"), to diagnose the held 1st gear and the remembered camera. ZIP SHA-256
`57D6CEB51B46F45A7DBF2FA657306BC7FCD3A40DCF3D0968C902E1E33824BBE2`; Mod.dll
`364827CD1090E07FBEEC48C14012219D0CBDB8559DDE0D48FE9B227AA32205FC`. Installer 70, ShimInput 12356, CameraTuning 396,
Triple 1355, SettingsUi 3444, WheelInput 620 passed. Game closed; payload 9/9; owner settings byte-identical.
Rollback copy of rc.19: `E:\Source\temp\art-0.4.4-rc.20\backup-installed-rc.19`.

## Previous installation — 0.4.4-rc.19 (October 31, owner test)

rc.18 plus the bonnet camera fitted from the car's shape (645e77c): centre-line profile from readable meshes or
the car's colliders; bonnet height and windscreen base anchor the fit; box fallback; measurements logged per car
("Camera mounts: measured ..."). ZIP SHA-256 `7D4CD990CDAFAC8ADD10976275212B5E1F766BA7F6C77C685134B5B99DE78957`;
Mod.dll `060AF510C0926B530CE83B7CD050C3C86A9B50E19025B9E89FB4D67EE337D95B`. Installer 70, CameraTuning 396, SettingsUi
3444 passed. Game closed; payload 9/9; owner settings byte-identical. Rollback copy of rc.18:
`E:\Source\temp\art-0.4.4-rc.19\backup-installed-rc.18`.

## Previous installation — 0.4.4-rc.18 (October 31, owner test)

rc.15 plus (rc.16 was packaged but never installed; its installer test only failed because the game was running):
- per-car bonnet/bumper placement, a new car fitted from the last tuned one (06c283b);
- every camera keeps its own adjustments per Game/Realistic set; Realistic starts at the seat's true field of view
  and stays adjustable; chase views get a saved field-of-view offset (6b53ea4);
- an H-pattern gear held at the start engages (378da86).
rc.17 (`6b53ea4`, ZIP `16F60987…5753`) was installed first; rc.18 replaces it. Source `378da86`, ZIP SHA-256
`59B2959DD7A00BE8DB9DA5C005473D651F9F838B00EB68257D7A9B70B3A18B5B`; installed `ArtOfSimRally.Mod.dll`
`5084948945648AE8473C9AA310C3A283E0F3A994493B0FCA2BDA7168ED34F39B`. Installer 70, ShimInput 12356, CameraTuning 391,
Triple 1355, SettingsUi 3444, WheelInput 620, Regression passed. Game closed; payload 9/9; owner settings
byte-identical. Rollback copies: rc.15 at `E:\Source\temp\art-0.4.4-rc.17\backup-installed-rc.15`, rc.17 at
`E:\Source\temp\art-0.4.4-rc.18\backup-installed-rc.17`. Custom Vehicles 0.6.0 (43d4ee4: wider cockpit eye range, Game/
Realistic cockpit views; 0.5.1 restores a saved cockpit view) installed, 13/13; 0.5.0 backup at
`E:\Source\temp\customvehicles-0.6.0\backup-installed-0.5.0`.

## Previous installation — 0.4.4-rc.15 (October 31, owner test)

rc.14 plus: the 1500R curve warp is computed from the real eye at the game's field of view (owner saw a fishbowl on
straight dash edges; the warp used the 313 mm virtual eye; toolkit 7af26cd WithSourceOn, vendored); the camera name
near the bottom ("Bonnet Camera - Game FOV", "Cockpit - Realistic", "Chase 2") with a key reminder; a Custom Vehicles
cockpit counts as a mounted view (Realistic and F7 apply). Source `98678d0`, ZIP SHA-256
`D1FC6FC6B4DC7C73C7F1E17F0F675FE4B4AC75FBD0267D5454B4FBFB4C9A70C3`; installed `ArtOfSimRally.Mod.dll`
`92B99690C82C594108F41133A3736E061F1F73C5418BB51F21DBAF86C3DA218F`. Installer 70, Triple 1355, SettingsUi 3412,
CameraTuning 386, toolkit 25 passed. Game closed; payload 9/9; owner settings byte-identical. Rollback copy of rc.14:
`E:\Source\temp\art-0.4.4-rc.15\backup-installed-rc.14`. Custom Vehicles 0.5.0 (7ea3aba: numpad cockpit view saved
per car) installed alongside, 13/13 match; 0.4.3 backup at `E:\Source\temp\customvehicles-0.5.0\backup-installed-0.4.3`.

## Previous installation — 0.4.4-rc.14 (October 31, owner test)

rc.13 plus: numpad camera tuning no longer stays blocked while an H-pattern shifter is in gear (the release wait
counted joystick buttons; owner report) and a camera overlay on every camera change (camera, field of view, keys).
Source `7db0a4e`, ZIP `ArtOfSimRally-0.4.4-rc.14-shared-input.zip` SHA-256
`F46227E24F937BE60C12956DDC634E35571DB16600E2F36EF72DB3A1E1BCD57B`; installed `ArtOfSimRally.Mod.dll`
`49F83F2FB9AE633F9FD14CBADBC06F228E1CB9CF32971448A14B16CF7C28B925`. Installer 70, Triple 1357, SettingsUi 3412,
CameraTuning 386 passed. Game closed; payload 9/9; owner settings byte-identical. Rollback copy of rc.13:
`E:\Source\temp\art-0.4.4-rc.14\backup-installed-rc.13`. Custom Vehicles 0.4.3 (cfef600, Custom Rally arrival
preview) installed alongside, 13/13 match; 0.4.2 backup at `E:\Source\temp\customvehicles-0.4.3\backup-installed-0.4.2`.

## Previous installation — 0.4.4-rc.13 (October 31, owner test)

Owner 10-31: simplify triple-screen framing (Art first, then the portfolio if it works). rc.13: always angle (and
curve) corrected; chase views always the game's field of view; bonnet/bumper use the camera's own field of view
(numpad +/-) or Realistic (measured seat); F7 toggles Game/Realistic (saves only OverrideFieldOfView); Shift+F7 is
the developer comparison (session only). Retired: ViewWidthScale slider, ChaseUsesGameFov toggle, saved F7View.
Source `0da187b`, ZIP `ArtOfSimRally-0.4.4-rc.13-shared-input.zip` SHA-256
`453D72EC1373B4E6586E384E013948B025463CED6D4099BC279384158280106F`; installed `ArtOfSimRally.Mod.dll`
`B334E931E406082734787D37109F8AA465C03B5F9DBB8790C3EED5557407FF38`. Installer 70, SettingsUi 3412, Triple 1353,
WheelInput 620, Regression, CameraTuning 385, ForceLifecycle 23 passed. Game closed; payload 9/9; Settings.xml,
TripleScreen.xml (OverrideFieldOfView=true, so Realistic starts on; the saved F7View=1 is now ignored),
dbce-controls.ini, Params.xml byte-identical. Rollback copy of rc.12: `E:\Source\temp\art-0.4.4-rc.13\backup-installed-rc.12`.

## Previous installation — 0.4.4-rc.12 (October 31, owner test)

Owner report 10-31: no FFB after a Wheelkit Apply (the saved Off from the earlier session was kept) and switching
FFB on in F6 mid-race did nothing (the reconnect refused while the stage was underway). rc.12: an open F6 counts as
not driving for the reconnect, so FFB starts there; each stage that starts with FFB off shows the off notice once.
Source `58839ea`, ZIP `ArtOfSimRally-0.4.4-rc.12-shared-input.zip` SHA-256
`095701954B441C8C20E74F51CC3D109024266E140DDEA4FE0152CECFFA699D92`; installed `ArtOfSimRally.Mod.dll`
`066535BB5E8CEF919EB71963469785A7B5F6C76B10C1E005A5FB39DB5A06808E`. Installer 70, SettingsUi 3428, WheelInput 620,
Regression, ForceLifecycle 23, CameraTuning 385, Triple 1357 passed. Game closed, lease held and released; payload
9/9; Settings.xml (FFB now on, saved by the owner 17:01), TripleScreen.xml, dbce-controls.ini, Params.xml
byte-identical. Rollback copy of rc.11: `E:\Source\temp\art-0.4.4-rc.12\backup-installed-rc.11`.
Owner request: the standalone Turtle Van mod (`Mods\ArtOfSimRally.TurtleVan`, 0.1.4) was removed; Custom Vehicles
carries the van (`dbce.turtle-van`) and refused to load beside it. Hash-verified copy:
`E:\Source\temp\art-0.4.4-rc.12\removed-TurtleVan-mod`.

## Previous installation — 0.4.4-rc.11 (October 30, owner test)

guna offline request 483604, file-only, no launch. rc.10 plus ISS-0014 (separate displays: F7 Original renders
all three as one flat plane at the game's framing instead of releasing, which froze the side displays) and
ISS-0015 (every force feedback switch-off, F8 / wheel Stop / F6 toggle, is logged and shown on screen; the owner's
FFB is off in Settings.xml from the 20:37 session and stays off until turned on in F6 > FFB). Source `653995d`,
ZIP `ArtOfSimRally-0.4.4-rc.11-shared-input.zip` SHA-256
`3631B2D887A6761A1715FBA0F6F7C81295A533327FB63F849FF416AE11A88D60`; installed `ArtOfSimRally.Mod.dll`
`3D9CBCEAF46EFFDD881F80E5EA84CE85A496A0A5180B315D61EC71F07B296724`. Installer 70, SettingsUi 3428, Triple 1357,
WheelInput 620, Regression, CameraTuning 385, ForceLifecycle 23 passed. Game closed, lease held and released;
payload 9/9; Settings.xml, TripleScreen.xml, dbce-controls.ini, Params.xml byte-identical. Rollback copy of rc.10:
`E:\Source\temp\art-0.4.4-rc.11\backup-installed-rc.10`.

## Previous installation — 0.4.4-rc.10 (October 30, owner test)

guna "finish rc10 package/install" (482367), file-only. rc.9 plus saved F7 (camera policy rev 3, toolkit 398a85d):
the F7 pick (Original / Angle / Angle + curve) is written as `<F7View>` only (narrow XML update, atomic replace,
unknown settings kept) and restored at launch; no "Not saved"; only a failed save shows "Could not save"; menus
and the curve fallback never write. F6 shows one static F7 line. lulu reviewed 16bb310/e4767ec. Source `1dabcb4`,
ZIP `ArtOfSimRally-0.4.4-rc.10-shared-input.zip` SHA-256
`4566960935E03FDD519D2D5368A7EDEFE862473E55A4A2F076486EDAECA5EB9F`; installed `ArtOfSimRally.Mod.dll`
`B8D0F93F7FDDE442FA8BB07F512B8BF1E3E9760D55B7DA355C102DBF272B6B8E`. Installer test 70 passed; SettingsUi 3425,
Triple 1356, Regression, CameraTuning 385, WheelInput 620 passed (the full Test-Rc gate needs stale shim paths
and a Lifecycle project fix; not run, same as rc.9). Game closed, lease held and released; payload 9/9 match;
Settings.xml, TripleScreen.xml (no F7View yet, so startup is unchanged until the first F7), dbce-controls.ini and
UMM Params.xml byte-identical. Verified rollback copy of rc.9: `E:\Source\temp\art-0.4.4-rc.10\backup-installed-rc.9`.
No game launch. Owner check: drive, press F7 to a view, restart, drive: the same view returns.

## Previous installation — 0.4.4-rc.9 (October 30, owner test)

guna GO 479018, file-only. rc.8 plus the camera policy rev 2 F7 comparison (toolkit 7319e71): Original game
view → Angle correction → Angle + curve correction, framing follows the saved choice, centered 2.5 s label with
"Not saved", menus answer "Comparison available while driving". Source `9cfd798`, ZIP
`ArtOfSimRally-0.4.4-rc.9-shared-input.zip` SHA-256
`85C2006B9A9EC4FE268A90407F649A52D2EB5876685B228A560AF8992CF53F57`; installed `ArtOfSimRally.Mod.dll`
`6DFC735A453B3F9E17B12E725FF9A0C5B6B927A2E6C4BB2A0A23FCAADFA0C52C`. Installer test 70 passed; game closed,
lease held and released; payload 9/9 match; Settings.xml, TripleScreen.xml (owner's measured view + 1500R),
dbce-controls.ini and UMM Params.xml byte-identical. Verified rollback copy of rc.8:
`E:\Source\temp\art-0.4.4-rc.9\backup-installed-rc.8`. No game launch.

## Previous installation — 0.4.4-rc.8 (October 29, owner test)

guna GO 478560, file-only. rc.7 plus the owner's ISS-0005 policy: the game's field of view is the default
framing (`OverrideFieldOfView` default false; saved files keep their value), F6 names the view with
"Use measured perspective" / "Use the game's field of view". Source `d054820`, identity
`0.4.4-rc.8+d0548200f4d648330297397006370c6898f540f3.clean`, ZIP `ArtOfSimRally-0.4.4-rc.8-shared-input.zip`
SHA-256 `8193BD0193178A372B47041D8105E4AC673CC88750950E858434FB3B082FB451`; installed `ArtOfSimRally.Mod.dll`
`2721521ECF1703F6BF0DBEA8ED3B3FC125D7218FA015D01CA0CDD947B8E37D48`. Installer test 70 passed; game closed,
lease held and released; payload 9/9 match; Settings.xml, TripleScreen.xml, dbce-controls.ini and UMM
Params.xml byte-identical. Verified rollback copy of rc.7: `E:\Source\temp\art-0.4.4-rc.8\backup-installed-rc.7`.
No game launch.

**First in-game curved evidence (owner session on rc.7, 2026-10-29 ~22:30-22:43 local).** The owner set
`CurveRadiusMm` 1500 and the measured view in F6 (TripleScreen.xml written 22:43:45). The last status.json
(03:43:44Z) reads "Three cameras target independent Unity displays with the measured perspective (F7, this
session). Curved screens are corrected." and render-diagnostics (03:43:39Z) has
`separateDisplaysRenderedRecently` true, which for a curved rig requires every presenter to have drawn the
current center frame. Camera completion, not pixels: support logging was off (no luminance) and no capture
or owner verdict yet.

## Previous installation — 0.4.4-rc.7 (October 29, owner test)

guna GO 476449, file-only. rc.6 plus: Surround keeps every full-span UI canvas on the middle screen
(KI-52, "Keep menus and HUD on the middle screen"), the presenter grid from the shared
`CurvedPanelWarp.SourceUvGrid` (toolkit 29c1f4d), and an opt-in curved-view luminance sample in
render diagnostics (support logging only). Source `e41c62a`, identity
`0.4.4-rc.7+e41c62aea328b8a2eaf3885b9cac9f23e34047c8.clean`, ZIP
`ArtOfSimRally-0.4.4-rc.7-shared-input.zip` SHA-256
`CF4A7AA000EFBFDB6E14699D9BB21A77B48085ECA0A1E5968865982C22EC166E`; installed
`ArtOfSimRally.Mod.dll` `13B24EFBDEE6CF3670FD114250AAC103E5E7F135D3838C90D7150BC089FD1C67`.
Installer test 70 passed; game closed, rig lease held and released; payload 9/9 hashes match;
Settings.xml, TripleScreen.xml, dbce-controls.ini and UMM Params.xml byte-identical; no CurveRadiusMm
key. Verified rollback copy of rc.6: `E:\Source\temp\art-0.4.4-rc.7\backup-installed-rc.6`.
No game launch.

## Previous installation — 0.4.4-rc.6 (October 29, owner test)

guna GO 471949 after lulu's final source review of b131565. rc.5 plus: stock chase views keep the
game's framing (`ChaseUsesGameFov`, default on; bonnet/bumper measured) and F7/status use the same
effective policy. Source `b131565`, identity
`0.4.4-rc.6+b131565f952f4dc1aaf8eacf5fbcd72f108ceaac.clean`, ZIP
`ArtOfSimRally-0.4.4-rc.6-shared-input.zip` SHA-256
`96BF9867510AE4AC152998F062D39D53895E0F4F571F9E26AD9AC0E144BE3233`; installed
`ArtOfSimRally.Mod.dll` `2FDBAB2C845CDF56EC759AD4BCA2C68D6472E5F864BB8F6A7D526802C619FABE`.
Installer test 70 passed; game closed, rig lease held and released; payload hashes match;
Settings.xml, TripleScreen.xml, dbce-controls.ini and UMM Params.xml byte-identical. The saved
TripleScreen.xml has no CurveRadiusMm/ChaseUsesGameFov yet, so curvature stays 0 and chase uses the
default. Verified rollback copy of rc.5: `E:\Source\temp\art-0.4.4-rc.6\backup-installed-rc.5`.
No game launch.

## Previous installation — 0.4.4-rc.5 (October 28, owner test)

Owner-requested deployment (guna GO 470430, after Wheelkit 0.6.58+0c670b7). rc.3 plus: opt-in
curved-panel correction (CurveRadiusMm, 0 = flat), eye distance to the glass middle, local bezel
gap (BezelGapMm), presenter-gated status, F7 session-only A/B (measured perspective / game FOV),
and the `artCurvedTriples` Info.json marker for Wheelkit. Source `a2a99b4`, identity
`0.4.4-rc.5+a2a99b4f5bd1e0cf7e9d8439c8d0fe9b56c35c81.clean`, ZIP
`ArtOfSimRally-0.4.4-rc.5-shared-input.zip` SHA-256
`EAB5D08DE1534219EC183A62EF5659B7B60EAC1CC113E7A2FF9C530A77EB481B`; installed
`ArtOfSimRally.Mod.dll` `F7F0CDEC1BE3B1BA36667ED57D023A63086733B894D77B23AD8C5BF0A51668F7`.
Installer test 70 passed; installed with the game closed under the rig lease; payload hashes
match; Settings.xml, TripleScreen.xml and dbce-controls.ini byte-identical before/after. UMM
Params unchanged by the install (CustomVehicles and TurtleVan were already enabled since the
22:10 game exit). Backup of rc.3: `E:\Source\temp\art-0.4.4-rc.5\backup-installed-rc.3`.
No game launch.

## Previous installation — 0.4.4-rc.3 (October 28, owner test)

0.4.4-rc.1 plus the KI-46 "next weather" prompt fix and the seat-matched view line/"Match my
seat" on the Cameras page (game FOV moved to Advanced; saved settings not reset). Source
`f27de37`, ZIP `ArtOfSimRally-0.4.4-rc.3-shared-input.zip` (SHA-256 starts `261EA55E8A984617`).
PromptGlyphs 41 and SettingsUi 3351 assertions, installer test 70 passed; installed with the
game closed under the rig lease; payload hashes match. TripleScreen.xml kept as the owner left it
(separate monitors, game FOV). rc.2 was packaged but never installed. Backup of the replaced
rc.1: `E:\Source\temp\art-0.4.4-rc.3\backup-installed-rc.1`. No game launch.

## Previous installation — 0.4.4-rc.1 (October 28, owner test)

Same source as the earlier 0.4.3-rc.1 install (Egon F6 controller copy and window landing,
untested in game), relabelled so Wheelkit no longer reports published 0.4.3 as an update, and
packaged with `-SharedInputCandidate` so the shared adapter and `dbce_shim.dll` are present
(0.4.3-rc.1 lacked both, so Wheelkit's Apply Rig Profile refused). Source `efc89b4`, ZIP
`ArtOfSimRally-0.4.4-rc.1-shared-input.zip` (SHA-256 starts `545835435A194F39`). Installer
test 70 assertions passed; installed with the game closed under the rig lease; all payload
hashes match the package; settings kept (`SharedInput` true). Backup of the replaced rc.1:
`E:\Source\temp\art-0.4.4-rc.1\backup-installed-rc.1`; the pre-rc.1 backup stays at
`E:\Source\temp\art-0.4.3-rc.1-backup\before`. No game launch.

## Public stable release — 0.4.2 (October 24)

[Public stable 0.4.2](https://github.com/d-b-c-e/dbce-mods-art-of-rally/releases/tag/v0.4.2)
is the latest release, tag `v0.4.2` at clean `77ddd30276cfefe59f99fe211d3c561b2969cb70`,
built against official toolkit v0.16.0 (the rc30 native and shim bytes, promoted
unchanged). Mod ZIP SHA-256
`2E7ABBE4EF5B0EA202D1778116FF60E7027F3783DF5C0C995E684FB375F221B9`; Controls Setup
ZIP `AEB9278602EBB828618105D61C1DD7A9DA3189255E0F2F6D08FBAE746965CC37`. Both were
downloaded after publication and matched. Package verifier 364, installer 50 and
rc32→0.4.2 upgrade 64, and companion clean-install 67 checks passed (PS 5.1).

The same package is installed in the owner's Steam game (identity
`0.4.2+77ddd30276cfefe59f99fe211d3c561b2969cb70.clean`; settings, profiles and other
mods kept; rc30 files backed up under hub `.local/art-rc32-owner-install-claude-20261024`).
A plain Steam launch on the 7680x1440 Surround desktop logged the switch from
2560x1440 to 7680x1440 and three-view `active`, no exceptions, normal close.

Owner sign-off: driving, force, performance (rc30, Oct 23); telemetry, H-pattern,
separate-monitor triples and, on 0.4.2, Surround span triples (Oct 24). Art is fully
signed off. Future improvement: on Surround some UI and backgrounds still use all three
screens ([KI-52](KNOWN-ISSUES.md#ki-52--surround-span-some-ui-and-backgrounds-use-all-three-screens)).

Known behavior: if a controller Rewired cannot identify appears while the game starts
(for example a Steam Controller presented by Steam Input as "Controller (XBOX 360 For
Windows)"), shared input is not used for that session and Art's own bindings stay
active, because the mod cannot rule out a Steam-wrapped duplicate of the wheel.
Start without that controller, or disable Steam Input for Art, then restart.

## Public release candidate — 0.4.2-rc.31

[Public prerelease](https://github.com/d-b-c-e/dbce-mods-art-of-rally/releases/tag/v0.4.2-rc.31)
was published October 23 from clean `57d1e14605913093df9e764f577f02ba31e93d20`.
It retains the owner-accepted RC30 gameplay source and changes packaged player
instructions and the build identity. The mod ZIP is SHA-256
`CC43BF77D3D1A6B7D1A261F11A25DCB57EB9070B60C2BD6FFF1E5DD278684ADE`;
the separate optional Controls Setup ZIP is SHA-256
`A6803A2C47CB578131F700FF45260E0A884AE985D8BB98F7763D4FEDB207F794`.
Both public downloads and sidecars were retrieved and hash-checked. Package
verification passed 363 assertions, isolated installer 64 assertions, and
companion clean install 67 checks. RC31 has not replaced the owner's RC30 install
or received its own game/hardware run. Stable public remains 0.4.1.

## Current installation — 0.4.2-rc.30 (owner-accepted private candidate)

Installed October 23 from clean source `89ca2180af9199ca678095009e5c09e0ea79b27c`.
The owner tested it in Art of Rally and fully signed off on October 23. The earlier
keyboard concern was withdrawn as unconfirmed. Preserve this accepted driving,
force and performance behavior for the release.

- **Identity:** `0.4.2-rc.30+89ca2180af9199ca678095009e5c09e0ea79b27c.clean`.
- **Package:** `ArtOfSimRally-0.4.2-rc.30-shared-input.zip`, SHA-256
  `7F243455854449A3BE8A1EEF1A6FDC4CCD782FBBD7D9EEC6FC5E3A996C999045`.
  The nine installed mod payloads matched its manifest on October 23 after the
  owner's test. The six package-root files are installer and documentation files,
  not installed mod payloads.
- **Package checks:** [rc30 build, verifier, installer and companion PlanOnly](../../../dbce-project-mgmt/artifacts/doli-art-rc30-package-20261023/package-01/result.json);
  [67-check disposable clean install](../../../dbce-project-mgmt/artifacts/doli-art-rc30-clean-install-20261023/install-01/result.json).
  These do not add hardware acceptance beyond the owner's sign-off.
- **Owner install:** [receipt](../../../dbce-project-mgmt/.local/art-rc30-owner-install-miho-20261023/run-01/result.json)
  records four changed owned files, eleven protected mod-folder files retained,
  no game launch during installation and released custody.
- **Public release:** RC31 above is separate from this private install. RC30
  labels itself private; stable packaging remains blocked by the shared shim
  and local toolkit pin rules in `tools/package/package.ps1`.

## Prepared candidate — 0.4.2-rc.29 (private, not installed)

Packaged October 19 (kobe 291994) from clean source `3f31b6fb1d70cc81cd3206563d275666402f9fa9`: the accepted rc28
product source `eb2316d` plus only the default-off course route capture (`3f31b6f`, docs/COURSE-ROUTE-CAPTURE.md).
It used the existing rc28 route: `tools/package/package.ps1 -Version 0.4.2-rc.29 -SharedInputCandidate`, with the
same qualified UMM references, pinned normal shim `BC9B030A` and unchanged helper `a8583c59` (`F3CF1345`). It is not
installed, launched or accepted. The installed accepted rc28 below is unchanged.

- **Identity:** `0.4.2-rc.29+3f31b6fb1d70cc81cd3206563d275666402f9fa9.clean`; `artShimProbe=false`.
- **Mod ZIP:** `ArtOfSimRally-0.4.2-rc.29-shared-input.zip`, 797,933 bytes,
  SHA-256 `DC0056F46829A55508A7F5D582B7600B942E3ED6E7CBE21B77C68D6D218207A6`.
- **Manifest:** `E517BD95CDF26F3E10E9CE9D94B0A3916F0F3C637DC36F87DC8ADB3FEE47FEF4`.
- **Plugin:** `D201C44624FB…`, 515,072 bytes; **build.json** `D4FA2F7D5F9C6C92AD8C2A373F3E711D20770564B328422CB69D5968308A34C6`.
- **Companion:** `ArtOfSimRally-0.4.2-rc.29-shared-input-controls-setup.zip`, SHA-256
  `F5CB2F5BD4883EA29EBC5F4301D18A35E541390B527B012A904AC851704434AE`.
- **Difference from installed rc28:** only `ArtOfSimRally.Mod.dll` (the capture and its identity), the two
  `Dbce.TripleScreen` assemblies (same size, source-revision stamp only), `build.json` and `README.txt` (the 0.4.2 draft
  text). No file added or removed; native copies, shim and force payloads are unchanged, so the accepted tuning stays.
- **Checks (hub `artifacts/art-rc29-candidate-zara-20261019/`):**
  - packaging build: zero warnings and errors;
  - release verifier: 363 assertions;
  - exact-package installer and rollback to the installed rc28 package: 64 assertions;
  - companion PlanOnly under stock Windows PowerShell 5.1 (`ps51-continuation-02`), with the art-of-rally-only command.
- **Retained failures, not product defects:**
  - The first companion step failed on the inherited PowerShell 7 module path, as rc28 recorded.
  - The first continuation passed a zero timeout. Its two processes had exited, and the lease was released (`cleanup-release.json`).
- **Course capture disposition:** packaged here, default off. The capture's own route identity, geometry and progress
  check needs this package installed and one opt-in launch. That gate is the existing course runtime task, not
  something the package proves.

## Current installation — 0.4.2-rc.28 (owner-accepted private candidate)

Installed October17 07:54 UTC from clean package source
`eb2316dc5bc951557c9bf1f9c2abbe0c6b2118ab`. The owner's October18 acceptance
applies to this currently used installation: done and ready for release preparation.
Accepted gameplay, force and performance are retained; publication is separate.

- **Identity:** `0.4.2-rc.28+eb2316dc5bc951557c9bf1f9c2abbe0c6b2118ab.clean`.
- **Mod ZIP:** `ArtOfSimRally-0.4.2-rc.28-shared-input.zip`, 789,191 bytes,
  SHA-256 `B7818C8DF90310D3B4C9BE7E67E0459EB22176B31FE670C40F7B0FF2D5981A2F`.
- **Manifest:** `2FD01BB9CD870A27DE800105579A4574706D022CB886E785BD5A6373BE4DB2F0`.
- **Plugin:** `296C8EFA49A718AF47F22292B3346BFE5260BEB677969B9B24238556FDF6712A`;
  **build.json:** `8E1498FCB00EF363ECF0C3E790C66B2E59D438C3B999F3F38BC85482A60448F9`.
- **Native/shim:** both normal native copies `44E46799`, shim `BC9B030A`;
  `artShimProbe=false`. The developer Recorder remains separate from the package.
- **Source/package location:** private worktree
  `E:/Source/games/.worktrees/zori-art-readonly-discard-20261017/artifacts/art-ui-20261017/package-01/source`;
  archive and checksum in its `dist/`. October18 read-only verification matched
  all 15 manifest-listed entries, all nine installed mod payloads and the second
  native copy to that ZIP. No build, install or runtime check was repeated.
- **Retained qualification:** production build zero warnings/errors and release
  verifier's 353 assertions and the exact-package isolated installer/rollback's
  64 assertions passed. The original package parent remains FAIL at companion
  PlanOnly because its direct
  PowerShell invocation inherited an unsuitable module path. The unchanged
  companion then passed through the intended stock PS5.1 environment:
  sibling `ps51-continuation/result.json`, SHA-256
  `1C6C28EFBBC8A879BC32E9B2E84AD7FCADBE3559980BFA8D455E0433A3854B6D`.
  This is not a new hardware or helper GUI acceptance claim.
- **Owner install:** hub
  `artifacts/art-rc28-owner-upgrade-zori-20261017/owner-r1/result.json`, SHA-256
  `B53E4AC42E4FD521FCAAA748BADF29A9BD368FEC8D9E4797115EE7225ACC7209`.
  Exactly four stamped payloads changed from rc27; 89 other files, 18 protected
  directories and 58 raw preference values were preserved. The original receipt's
  pending lease closure is settled by sibling `cleanup-release.json`, SHA-256
  `11323D1E20D0D3B14862B25BEB560D45FD98C31F895D88308DD66629DB428ADD`:
  owned processes absent, foreign processes untouched, exact lease released and
  actually absent at 07:55:23Z October17.
- **Rollback:** retained private payload/state backup under that owner-r1's
  `art/before/files`, with `raw-preferences.json` and sibling tree-before/tree-after
  records. Preserve newer owner settings when reverting binaries. For a public
  older-release downgrade, follow [the current uninstaller-first instructions](SETUP.md#updates-removal-and-settings).
  The 64-case fixture used rc27 as its second package, not public 0.4.1.
- **Optional companion:** separate, not installed in the game;
  `ArtOfSimRally-0.4.2-rc.28-shared-input-controls-setup.zip`, SHA-256
  `78C27DB09A539FCBF0149CA71895F1E6238C8A68A54B41EFB6E929B41FF97070`,
  pinned helper a8583c59. Public helper distribution is not decided here.
- **Narrow later evidence:** hub
  `artifacts/art-rc28-binding-status-samo-20261017/RUN01-RESULT.md` retains visible
  Bind listening/Cancel, Save refusal, readable error persistence and Discard.
  Its failed outer monitor and successful cleanup-only recovery remain separate.
  Moved-control/new-device successful Bind/Save/restart, action response, glyphs
  and device loss on hardware remain untested extras, not newly claimed passes.

Public stable remains 0.4.1. The [0.4.2 release draft](releases/0.4.2.md) records
the exact pending distribution decision and stable producer restrictions.
No accepted archive, payload or installation was changed for this documentation.

Canonical consolidation (October 19): canonical `main` carries the rc28 source closure
(`68c4e22`) and this release draft (`ba684ac`) in one merge. The product source is still `eb2316d`, with no
non-Markdown change, and the owner's dirty `experiments/CustomVehicles/SESSION.md` was preserved. The rendered
and physical gaps of the binding work are recorded in `docs/archive/reviews/2026-10-17-readonly-binding-discard.md`.

## Previous installation — 0.4.2-rc.25 (private shared-input candidate)

Closed-game October15 22:26 UTC update from clean package source
`ac5f3c8490b07211fc35f13806bf23dbc801dc85`; mod C955FC1B/build81C8D4A6,
ZIP AF07AC99/manifest9BFD4149. Retained final managed compilation and metadata345
passed; the actual package passed disposable stock PS5.1 installation and actual
companion Preview/Apply/zero-change Preview. Original failed wrapper receipts
and their successful build/production stages remain retained.

All nine installed mod payloads and both normal native copies verified. Exactly
four paths changed: mod, TripleScreen Core/Protocol and build.json. Current
Settings/profile/triple XML, saves, dirty custom-vehicle work, raw preferences,
force/tune/native/shim and all protected nonowned files remained exact. Receipt
4E1D76C8, parent02AEE401 and independentB6BF82BC confirm successful installation,
all3 processes Absent and lease absence. Rollback is retained privately under
hub `artifacts/art-rc25-owner-install-20261015/owner-r1/art/before/`.

One subsequent private muted Controls-first screenshot passed actual image
review: PNG25939EC9 with exact shared/owner/copy restoration and independent
D1E9/E6DC closure. Only the changed first view is covered; the recorder's deliberate
early-close incomplete-tour outcome remains unchanged. No Save/binding workflow,
force/performance baseline or physical click/device acceptance was repeated.
Public release remains0.4.1; the optional bff4ce86/67DCD71C companion stays external.
[Detailed source, qualification and view evidence](archive/reviews/2026-10-15-controls-editor-first.md).

## Previous installation — 0.4.2-rc.24 (private shared-input candidate)

Closed-game October15 12:51:53 UTC update, source8d4fbd0, modC1CAC9EA/buildCA402E06. Exact ZIP02BCA721 passed release343, installer50 and actual companion helper checks. All nine mod payloads and both native copies verified; four stamped payloads changed. Settings/profiles/tune, nonowned files, saves and raw preferences remain exact. Backup and receipt603AE2D0 are retained privately. No game/force test/public push; public release remains0.4.1. [Full exact delivery evidence](2026-10-15-f6-ui-owner-delivery.md).

The owner gave standing authorization on 2026-09-09 UTC to keep the installed
copy current. Local deployment is a required checklist item when finishing a
feature or bug fix. After its candidate or final artifact passes the complete
local automated gate, deploy it for testing without asking again. Public release
publication and attended sign-off remain separate.

## Previous installation — 0.4.2-rc.23 (private shared-input candidate)

Installed October15 at09:58:55 UTC (04:58:55 CDT), exact clean package source
`1ec4c60357d7892e76fb451d33bfbafa837be130`. This is the normal private shared-input
payload; its optional standalone setup remains a separate companion outside the
game. The force algorithms, native libraries and saved owner tune are retained.

- **Normal package:** `ArtOfSimRally-0.4.2-rc.23-shared-input.zip`, SHA256
  `38F54CA8449EA75A2AC12BA212D1ECB264922BCB9380AB2057AD08AD1BBBEC8B`;
  manifest `8B2C349652B454E14C79FAFAB4A0327B71D26BA1FE8EBADB44917EB1B84BA2B5`.
- **Mod assembly:** `402AE0C509A4A28D6433C09F1D7376E4ADA2A7BB73D63FFAAECDC83A7F14A64C`.
  Installed identity is `0.4.2-rc.23+1ec4c60357d7892e76fb451d33bfbafa837be130.clean`.
- **Exact four-path delta from rc22:** ArtOfSimRally.Mod.dll, build.json,
  Dbce.TripleScreen.Core.dll and Dbce.TripleScreen.Protocol.dll. All nine mod
  payloads match the package, with the other five unchanged. Both native copies
  remain44E46799; shimBC9B030A and managed FFB6FC2BE91 remain exact.
- **Qualification retained:** normal package's50 exact installer assertions;
  helper-r3 actual Preview/Apply/idempotence on disposable current payloads.
  The initial helper-fixture failures were retained and corrected without a
  package rebuild. Hub review `docs/archive/reviews/2026-10-15-art-rc23-package-result-sana.md`
  gives source/runtime/physical limits. No gate, suite, build or rehearsal reran
  for this installation.
- **Reviewed installer recipe:** hub
  `artifacts/art-rc23-owner-test-20261015/owner-install-plan/Install.ps1`, SHA256
  `EA89FDF359B83141E373DCB36C1D231B70F37BAE2820F99FD1C123007C4384A8`.
  Sana136243 approved the exact recipe; execution followed Lola FINAL EXIT136258
  and actual lease absence. The actual shared lease covered09:58:43.495–09:58:55.147
  UTC, then released; direct FINAL EXIT went to the coordinator and next agent.
- **Owner state:**85 other protected file/absence entries and18 directory/root
  markers unchanged, including20 current LocalLow files, native bindings,
  Wheelkit profiles, dirty CustomVehicles SESSION.md and raw PlayerPrefs
  names/types/bytes. Current hashes:
  - Settings.xml `1ED90E601D646F5FCB963A017B6B986B5C09F7AC537B763769CCEE3FD69DD8B1`;
  - dbce-controls.ini `73E782304BB56DF5D722A0FADDA7E9C5FFD8347736C11003A6DFEFCDF94FADFA`;
  - TripleScreen.xml `13F0FD93B02D3457801D7720F33B94A9ABB3C967FF2FBCDF50214C6CCF4D17FC`.
  The historical rc22 settings snapshot was not substituted for current choices.
- **Receipt:** hub `owner-install-plan/owner-install-r1/install-receipt.json`,
  SHA256 `882E7A2D04FF1E97482E5032FF4C5DD55C0679BE8CD17690D2A5C26AC68E2C48`.
- **Backup:** hub
  `artifacts/art-rc23-owner-test-20261015/owner-install-plan/owner-install-r1/before/`.
  All protected file bytes have independent hash-verified copies under files/;
  raw-preferences.json retains exact registry data. Owner contents remain local.
  Preserve this backup and rc22's prior receipt/backups. Rollback is limited to
  the four known owned paths while closed and leased; preserve newer owner
  settings/profile choices and refuse unrecognized changed bytes.
- **Limits:** no helper installed, profile Apply, backend switch, retune, game
  launch, native call, force test, display change, source push, tag or publication.
  This update is installed software, not new runtime or physical acceptance.

## Previous installation — 0.4.2-rc.22 (private shared-input candidate)

Installed October 14 at 02:35:45 UTC (October 13, 21:35 CDT), exact package source
`2decf4d418815ca3835bbb51eb83a6e0ab886bc5.clean` (canonical main).

It is rc.21 plus automatic side displays for **Three separate displays**:
- The sides are the equal-size displays directly left and right of the primary. A dashboard or other extra display is
  never used.
- Unity's position-sorted display order predicts their indices. Each side is then proven by the window its activation
  opens before side views render.
- The saved `LeftDisplayIndex`/`RightDisplayIndex` are no longer used or shown; their stored values are untouched.

The shim, force native, explicit FFB selections, gains, effects and saved tuning are unchanged. This is a local test
candidate, not physical acceptance or a public release.

- **Exact package:** `ArtOfSimRally-0.4.2-rc.22-shared-input.zip`, 766,448 bytes, SHA-256
  `380BDA3E24A9B895E4CFDC461D99C7486E74A71619C558BE71C1765EE96B29AD`; manifest
  `0A6F857175199B4C200830361B45BB952C7D46FC3FCF5C6A71FD162699BD61F2`.
  - Installed mod `C256C8795BE11780F264CEF0D06301E58159144005BBE4DE399E61B6950B7755`.
  - Shim `BC9B030A` and both force native copies `44E46799`: unchanged from rc.21.
- **Gates** (receipt `results/rc22-final-gates/receipt.json`):
  - Throwaway rc.927 on the same clean source passed **20 of 20 gates** (510,530 assertions), with the original
    two-case corpus (`C964C158`) and landing 9,582. `ART_SHIM_DLL` and `ART_LATCHED_SHIM_DLL` were set to the
    `BC9B030A` copy for that runner only. Gate `automated.json` `AE22099A…`.
  - `Test-Installer` passed **50 assertions** on a fresh extraction of the exact ZIP. Report `37A16F95…`.
- **Runtime evidence reused:** the display change is the code tested in the rc.926 stage proof (`4f02a0f`); since then
  only documentation changed.
  - Unity 1 was proven on the left display and Unity 3 on the right.
  - The dashboard (Unity 2) was never activated.
  - The captured row showed one continuous scene left to right.
  - Hub `artifacts/art-separate-display-stage-20261013`. No new runtime run was made for this repack.
- **Rehearsal and install:** wrapper `results/rc22-install-plan/Install.ps1`, SHA-256
  `482FDE6E7DD35DCD5B747E3859E203222AA01E2EB7236A2E81694821E0335C24`.
  - It is the rc.21 wrapper (`CBA51376`) with rc.22 inputs. The rc.21 owner receipt is its prior state, and it expects
    the four-file delta.
  - The scratch rehearsal passed with the same bytes, then owner mode ran. Both ran under lease
    `e638f1a8ba234ed6a4af418f115f7fa7`, 21:31:37–21:35:52 CDT, which also covered the package and gates.
  - The game was closed. No launch, profile Apply, device call or force output.
- **Changed, exactly four files:** `ArtOfSimRally.Mod.dll`, `build.json`, `Dbce.TripleScreen.Core.dll` and
  `Dbce.TripleScreen.Protocol.dll`. All other **63 files** in the protected tree retained their hashes, including the
  shim and both native copies.
- **Current owner settings retained:**
  - Settings.xml `0A380211744928BF91BF3B8E30F791142286D97DBEF4CF65F05ECA95D2E4F68A`;
  - dbce-controls.ini `73E782304BB56DF5D722A0FADDA7E9C5FFD8347736C11003A6DFEFCDF94FADFA`;
  - TripleScreen.xml (separate displays on), unchanged;
  - Wheelkit mapping profiles `7AE6A254` and the canonical dirty CustomVehicles `SESSION.md` (`42EBDD69`), unchanged.
  - Saves (23 files) and PlayerPrefs (export `44235D65`) are byte-identical before and after
    (`results/rc22-install-plan/saves-prefs`).
- **Receipt and backup:** in
  `E:/Source/games/.worktrees/art-rc22-rita/results/rc22-install-plan/owner-install/`.
  - `install-receipt.json`, `tree-before.json`, `tree-after.json`, `exit.json` and `before-install/`. The verified
    backup holds the rc.21 Art mod folder and the second native copy.
  - The neighboring `rehearsal/` holds the scratch receipt.
- **Rollback:** with Art closed, restore the backed-up rc.21 payload files. Preserve newer owner settings and profile
  choices, and keep the prior receipts and backups.
- **Runtime scope:** the installed package was not launched.
  - The display proof covers this fixed four-display rig with a synthetic native-script route.
  - Physical controls, production FFB acquisition and coexistence, and force feel remain unqualified.
  - There is no public binary or source promotion, tag or release.

## Previous installation — 0.4.2-rc.21 (private shared-input candidate)

Installed October 14 at 01:12:28 UTC (October 13, 20:12 CDT), exact package source
`b1af29f9168d9f2c6ad8c0e0bff7c1c246508744.clean`. It moves the private candidate to
the canonical toolkit shim `c6594fa` (`BC9B030A`), which merges the copied-provider
bridge, the fenced latched session and the pure profile inspection. It also carries
the two main changes since rc.20: observed racing-row diagnostics (`281c6e2`,
diagnostics only) and American English player text (`dd133b6`). The force native,
explicit FFB selections, gains, effects and saved tuning are unchanged. This is a
local test candidate, not physical acceptance or a public release.

- **Exact package:** `ArtOfSimRally-0.4.2-rc.21-shared-input.zip`, 754,813 bytes, SHA-256
  `D2C754B64361705587FC79C8835BFA3FA917D10C80697C589D4C11A0BC3A25DB`; manifest
  `B0F36FF0C8FDEDBA43FA6041DC4A8CF5DA119BC8FCFF50660DBB8F1403112F4C`.
  - Installed mod `7CB24EE8025D59CB43D233FCBB56C59019C06A83E875F52A215057B35F567EC7`.
  - Shim `BC9B030A25834D8CCE562F22588EC5E0D3F5C1FA026AD7AD984776D60CD6254B`, toolkit
    `c6594fa` built at `421bd4b`. The private copy and its provenance are described in
    [lib/input-shim/PROVENANCE.md](../lib/input-shim/PROVENANCE.md).
  - Both force native copies remain `44E46799`.
- **Gates:**
  - The first complete gate, throwaway rc.923 on `b1af29f`, stopped at `game-bindings`.
    The test still asserted "cancelled" after `dd133b6` changed the player text to
    "canceled". `aa40524` fixes only that test.
  - The retry, rc.924 on `aa40524`, passed **20 of 20 gates** (510,450 assertions). Its
    runtime and package inputs (`src`, `lib`, `tools/package`, `tools/installer`,
    `Version.props`) are identical to the packaged `b1af29f`. It used the original
    two-case corpus (`C964C158`), and its recorded landing check passed 9,582 assertions.
  - `ART_SHIM_DLL` and `ART_LATCHED_SHIM_DLL` were set to the `BC9B030A` copy for that
    runner only.
  - `Test-Installer` passed **50 assertions** on a fresh extraction of the exact ZIP.
  - Receipts: `results/rc21-final-gates-retry/receipt.json`; gate `automated.json`
    `91FC7840…`; installer report `73C57AE0…`. The failed first attempt is kept in
    `results/rc21-final-gates`.
- **Rehearsal and install:** wrapper `results/rc21-install-plan/Install.ps1`, SHA-256
  `CBA513767F51E03971757F8958D215CE208EAF41DB699556ADBF86625C84051D`.
  - It is the rc.20 wrapper (`5AD5243A`) with rc.21 inputs. The rc.20 owner receipt is
    its prior state, and it borrows the announced lease instead of taking its own.
  - The scratch rehearsal passed with the same bytes, then owner mode ran. Both ran under
    lease `a3da531b50de401d95931ba90463bbbf`, 20:03:32–20:13:09 CDT, which also covered
    the gates.
  - The game was closed. No launch, profile Apply, device call or force output.
- **Changed, exactly five files:** `ArtOfSimRally.Mod.dll`, `build.json`,
  `dbce_shim.dll`, `Dbce.TripleScreen.Core.dll` and `Dbce.TripleScreen.Protocol.dll`.
  - The TripleScreen dependencies were rebuilt again, without a triple-screen source
    change.
  - All other **62 files** in the protected tree retained their hashes. That includes
    SessionTools, CustomVehicles, the game and loader files, both native copies, and all
    settings and layout files.
- **Current owner settings retained:**
  - Settings.xml `0A380211744928BF91BF3B8E30F791142286D97DBEF4CF65F05ECA95D2E4F68A`;
  - dbce-controls.ini `73E782304BB56DF5D722A0FADDA7E9C5FFD8347736C11003A6DFEFCDF94FADFA`;
  - Wheelkit mapping profiles `7AE6A254`, and the canonical dirty CustomVehicles
    `SESSION.md` (`42EBDD69`), both unchanged.
  - Saves (23 files under `LocalLow\Funselektor Labs`) and PlayerPrefs
    (`HKCU\Software\Funselektor Labs\art of rally`) were last written before the install.
- **Receipt and backup:** in
  `E:/Source/games/.worktrees/art-shim-canonical-rita/results/rc21-install-plan/owner-install/`.
  - `install-receipt.json`, `tree-before.json`, `tree-after.json`, `exit.json` and
    `before-install/`. The verified backup holds the rc.20 Art mod folder and the second
    native copy.
  - The neighboring `rehearsal/` holds the scratch receipt.
  - Copies of the wrappers, logs and receipts, without binaries, and the saves/prefs check
    are in `dbce-project-mgmt/artifacts/art-rc21-install-20261013`.
- **Rollback:** with Art closed, restore the backed-up rc.20 payload files and the second
  native. Preserve any newer owner settings or profile choices, and keep the prior
  receipts and backups.
- **Runtime scope:** the installed package was not launched. No runtime run used the
  `BC9B030A` shim, and that includes a fresh shared-only startup. The offline adoption
  checks are in the shim provenance.
  - Physical controls, production FFB acquisition and coexistence, and force feel remain
    unqualified.
  - The shim stays a private packaging input. There is no public binary or source
    promotion, tag, push or release.

## Previous installation — 0.4.2-rc.20 (private shared-input candidate)

Installed October 13 at 13:52:55 UTC (08:52 CDT), exact package source
`1d845d8bb26090f13893c2249092f371b4f0e861.clean`. It resolves a fresh shared-only
user's configured steering FFB target before reader qualification, using the
shared Core's pure profile inspection. Explicit FFB selections, gains, effects,
saved tuning and the accepted force native are unchanged. This is a local test
candidate, not physical acceptance or a public release.

- **Exact package:** `ArtOfSimRally-0.4.2-rc.20-shared-input.zip`, SHA-256
  `8BA67D4EDC3E99F549F3FC2818D8185CC3CC6FE0AB03A7A24EB475FC5B8743EA`.
  Installed mod `40C837E2`, shim `723E8A38` from private native source `5b96ba8`;
  both force native copies remain `44E46799` from the accepted `bbbf6e3` line.
- **Gates:** complete Test-Rc 20/20 with both original recorded cases passed on
  clean `76d4c10` (throwaway rc.922; runtime/package inputs identical to `1d845d8`).
  Corpus 215,928 assertions, recorded landing 9,582. Exact rc.20 Test-Installer
  passed 50 assertions. The required `ART_SHIM_DLL=723E8A38` environment and the
  preserved earlier stale-fixture refusal are documented in the review below.
- **Rehearsal and install:** the same reviewed wrapper `5AD5243A` first passed
  against a scratch copy of the current owner tree. Actual install held lease
  `3a0f6041a5504c9a9e8532e80af957b9`, 08:52:51–08:52:55 CDT, then released it.
  Game closed; no launch, profile Apply, device call or force output.
- **Changed, exactly five files:** `ArtOfSimRally.Mod.dll`, `build.json`,
  `dbce_shim.dll`, `Dbce.TripleScreen.Core.dll`, `Dbce.TripleScreen.Protocol.dll`.
  The TripleScreen dependencies were rebuilt without a triple-screen source
  change in this slice. All other **62 files** in the protected tree retained
  their hashes, including SessionTools, CustomVehicles, game/loader files, both
  native copies and all settings/layout files.
- **Current owner settings retained:** Settings.xml
  `0A380211744928BF91BF3B8E30F791142286D97DBEF4CF65F05ECA95D2E4F68A` and
  dbce-controls.ini
  `73E782304BB56DF5D722A0FADDA7E9C5FFD8347736C11003A6DFEFCDF94FADFA`.
  No backend, binding, FFB selection or tuning field was rewritten. External
  Wheelkit mapping profiles `7AE6A254` and the canonical dirty CustomVehicles
  `SESSION.md` (`42EBDD69`) also retained their hashes.
- **Receipt and backup:** in
  `E:/Source/games/.worktrees/art-shared-ffb-target/results/rc20-install-plan/owner-install/`,
  `install-receipt.json`, `tree-before.json`, `tree-after.json`, `exit.json` and
  `before-install/`. The verified backup contains the previous Art mod folder
  and second native copy. The neighboring `rehearsal/` holds the scratch receipt.
- **Rollback:** with Art closed, restore the backed-up rc.18 payload files and
  second native; preserve any newer owner settings/profile choices. Keep the
  prior receipts and backups. Do not use the diagnostic native substitution or
  the fresh-user fixture Settings for installation or rollback.
- **Runtime scope:** one disposable startup resolved the expected configured
  target before qualification and accepted a shared frame, with exact owner/copy
  restoration. It used the explicitly recorded test-only `DD301314` native and
  irreversible load-time mute, without injection. Physical controls, production
  native FFB acquisition/coexistence and force feel remain unqualified by that
  run. The installed package was not launched after deployment.

Details and exact hashes: [shared steering target review](archive/reviews/2026-10-13-shared-steering-ffb-target.md).
The shim remains a private packaging input; no public binary/source promotion,
tag, push or release accompanied this installation.

## Previous installation — 0.4.2-rc.18 (private shared-input candidate)

Installed October 13 at 03:46:04 UTC from clean `26f5cca`, package SHA-256
`C9506342C55040BAE584CC30D0E14409FB596110547D343D32B9D8AC9943B901`.
Receipt and verified rc.17 backup remain in the canonical worktree at
`results/shared-input-rc18-install/`. Full rc.919 Test-Rc at `24439a3` passed
20/20 with the original corpus; exact-package installer checks passed. Exactly
four payloads changed (mod, build receipt and both TripleScreen dependencies),
with 58 other files unchanged and both native copies `44E46799`.

That install's Settings.xml was `91090837` with shared input absent. The owner
later applied a shared profile; rc.20 preserved the current Settings/profile
listed above rather than restoring this historical snapshot. rc.18's receipt,
package and backup remain intact.

## Previous installation — 0.4.2-rc.17 (private shared-input candidate)

Installed October 12, 19:29:51 UTC (14:29 CT), clean source `096bf5e`, at the
coordinator's request (sana 35954), under the standing authorization above.
Installed for testing only: it is not physical acceptance or a public release.
- **Package:** ZIP SHA-256
  `3A5888144A6DD9977881A82BED0D174D389B3AF49DA118846702CE9CA5A546A8`. Test-Rc with the
  corpus passed 20 of 20 on that source (throwaway rc.915). Test-Installer and the
  exact-ZIP Wheelkit Apply/Verify/Restore both passed.
- **Receipt:** `results/shared-input-rc17-install/install-receipt.json` in the main
  worktree, beside `Install.ps1`, which refuses unless the target is exactly the
  recorded rc.13 install.
- **Rehearsal:** the same script first ran against a scratch copy of the game files and
  gave the same result.
- **Changed, exactly 7 files:**
  - mod `AB4C7856` and `build.json`;
  - a new `dbce_shim.dll` (`7DDC9621`), owned by the installed `build.json` pins;
  - both TripleScreen DLLs;
  - the native `44E46799` in `Mods` and in `Plugins\x86_64`.
- **Unchanged (55 files):** every other file in `Mods`, `Plugins\x86_64` and the game
  root, plus `Assembly-CSharp.dll` and the UMM `Params.xml`. That includes:
  - `Settings.xml` `91090837`: no SharedInput key, so the shared profile stays off;
  - `TripleScreen.xml` and `desired-layout.json`;
  - the DevRecorder, CustomVehicles and TurtleVan mods;
  - the owner's Wheelkit `mapping-profiles.json` (`31283715`).

  No `dbce-controls.ini` exists.
- **Conditions:** shared lease, game closed. The owner-activity policy was honoured,
  with idle recorded truthfully. No launch, Wheelkit Apply, force or device operation.
- **Backup:** `results/shared-input-rc17-install/before-install` holds the exact rc.13
  `Mods/ArtOfSimRally` and the Plugins native copy.
- **Rollback:** with the game closed, run this package's `install.ps1 -Uninstall`, which
  removes the owned `dbce_shim.dll`. Then run the rc.13 package
  (`results/ffb-reference-v2-rc13-install/package/install.ps1`), or copy the backup
  back. Settings are untouched either way.
- **Unqualified:**
  - Rewired's real keyboard-source detection, and what two opposing held keys report;
  - physical force feedback, clutch pedal and H-pattern shifter with the shared profile.

  Details: `docs/research/2026-10-12-shared-input-candidate-package.md`.

## Previous installation — 0.4.2-rc.13 (force feedback reference v2)

Installed October 11, 19:46:05 UTC from source `8bc94b1`. The `.dirty` identity came
from an index-only line-ending state. Package `B8D45C60…`, mod `F199D405`, native
`F8361A08`. Receipt and backup: `results/ffb-reference-v2-rc13-install/`. See
`docs/archive/reviews/2026-10-11-ffb-reference-v2.md`.

## Earlier installation — 0.4.2-rc.6 (profile controls)

Installed October 10, 07:10:38 UTC (02:10 CT), clean source `905d04b`.
All 20 gates and both original force-corpus cases passed before installation.
ZIP SHA-256: `647CCD0B2A8E5F8A2009FB88E3EFD5F68C5839358A710208C29D929DBEE2ABCD`.
Private exact backup/receipt: `results/profile-controls-rc6-install/`.
All packaged payloads and the second native copy verified; eleven protected
settings, loader, game-assembly and probe files are unchanged. Installed under
the shared lease after the idle gate, with the game closed. No launch, profile
Apply, force output or device operation. Lease released. Public 0.4.1 unchanged.
The post-Apply live regression subsequently passed at 02:44 CT: production
Wheelkit Apply, launch-file verification and the original session-5 playback
(13,455 poses, zero application error), normal closure and exact owner restore.
Physical output was muted. Raw input qualification remains pending; see
`archive/reviews/2026-10-09-profile-controls.md` for the capability and acceptance limits.

## Previous installation — 0.4.2-rc.4 (KI-47 input recovery)

Installed October 10, 01:21:34 UTC (October 9 local), clean source `20fc617`.
All 20 package gates and the original force corpus pass. ZIP SHA-256:
`2B4E73868E2928462CDB7B4AFB184AF31299318FEA2611C7E0FE7044068D15AD`.
Private receipt and original backup: `results/input-recovery-rc4-install/`.
Every package payload, second native copy and protected settings/probe file
was hash-checked. Game closed, no launch or physical force. Native 901 and
accepted force tune remain unchanged. Attended control/recovery checks are
pending; the original disappearance is not attributed to a proven USB cause.
See [investigation](archive/reviews/2026-10-09-input-recovery.md).

Owner then launched at 20:30 local and confirmed steering, throttle and
handbrake respond. Fresh-launch control operation is accepted; automatic
recovery after a new device interruption remains untested.

## Previous installation — 0.4.2-rc.3 (steering wording + native lifecycle)

Installed October 8, 09:26 UTC from clean source
`914ea3a983aa52bd78cc0cf8416968db9f0b10b6`. Includes the Steering strength label
and visible effects/tuning link, plus reviewed native 901 / 48-export lifecycle
fixes. Managed toolkit remains v0.15.0; force models and owner tune are unchanged.

- All 20 offline gates and both original force captures pass (215,928 corpus
  assertions, zero model/device mismatch).
- ZIP SHA-256: `16F79235A23039720D5D046CB9C9A541959BF23659B961968FA696DB7319B200`.
- Backup/receipt: `results/native901-20261008/`; exact payloads and second native
  copy verified. 11 protected files unchanged.
- Original owner session 5 cold replay passes all 13,455 driving poses, normal
  exit 0, no gear mismatch/divergence. All 58 raw preferences and three owner
  files restored and independently compared.
- Physical output muted; UI rendering, device recovery and feel remain attended
  checks. Public stable remains 0.4.1; this native component pin is a local RC.

See [native adoption evidence](archive/reviews/2026-10-08-native-lifecycle-adoption.md).

## Previous installation — 0.4.2-rc.2 (KI-46 prompts + frame-rate readout)

Installed 2026-10-07 09:13 UTC (04:13 local) with the game closed, from clean source
`2fbff6d9d543245925777f327d8dc5ba964f7f09` (main; not tagged). Runtime = rc.1 plus the frame-rate readout
(STD-023/024): settings Cameras page shows the last 10 s average/1% low/worst frame, "Show frame rate on screen"
(off by default) draws a counter at the top right of the centre screen, the log gets a line every 30 s.

- All 20 offline gates: `results/rc-0.4.2-rc.2-6a84523d87da458596c77d4050fa0743/automated.json`.
- ZIP SHA-256: `77E8D520522A7203C28E74119E0F6B43E268DD7182B3B025445D9C668906C43F` (fresh extraction of the gated ZIP).
- Backup/receipt: `results/rc-042-rc2-install-20261007-0413/receipt.json`. Payloads match; the seven protected
  files kept their hashes.
- In-game checks pending: KI-46 prompts with the wheel; the frame-rate readout and counter.

## Previous installation — 0.4.2-rc.1 (KI-46 test build)

Installed 2026-10-07 06:55 UTC (01:55 local) with the game closed, from clean source
`2922100d31f645a04383835478a867bc790a1bc8` (main; not tagged). Runtime = 0.4.1 plus KI-46:
menu prompts (e.g. "next weather") show the wheel button's short name ("B12") in the keyboard
badge when the wheel has no artwork, instead of a keyboard badge over a stale gamepad glyph
(`PromptGlyphFallback`, 1f63094).

- All 20 offline gates: `results/rc-0.4.2-rc.1-01052b3ee0094e9996edcbc26774f54b/automated.json`.
- ZIP SHA-256: `61DFCCBBDFDD3861AD4BAA49BC6131331D7F562DCCC3E93DF5EE1EB073AEA8C5` (installed from a fresh
  extraction of `dist/ArtOfSimRally-0.4.2-rc.1.zip`; the gate's own package folder is modified by its upgrade test
  and refuses to install, which is correct).
- Backup/receipt: `results/rc-042-rc1-install-20261007-0154/receipt.json`. All payloads match the package; the
  seven protected files kept their hashes.
- In-game check pending: KI-46 prompts with the wheel (the menu "next weather" prompt and the pause menu).

## Previous installation and stable release — 0.4.1

Installed 2026-10-07 01:08 UTC (October 6 local) with the game closed, and published
the same archive as [v0.4.1](https://github.com/d-b-c-e/dbce-mods-art-of-rally/releases/tag/v0.4.1).
Clean source `7c3de40dce45fe6f5c9e95aadcf88d614876cb8e` (tag `v0.4.1`); runtime source is
identical to 0.4.1-rc.2 (`ddabada`), only documentation changed since.

- Owner tested 0.4.1-rc.2 at the rig on 2026-10-06 (crash rattle, camera keys and
  steps, first-launch logging reset) and approved release ("I approve, let's ship it").
  The owner reported KI-46 (overlapping menu prompts) for later.
- All 20 final gates: `results/rc-0.4.1-3b4ea6074dc14bf5bf410278307680f2/automated.json`.
  The two original owner force captures were revalidated separately against this source
  (215,928 assertions, zero model/device mismatches).
- ZIP SHA-256: `8BD632E89377447B03DCC98EBA9FA44DF6AF7EF6AB3C7A17A4A892605A28BA60`;
  the published ZIP and checksum were downloaded again and match:
  `results/release-041-published-download/verified.json`.
- Backup/receipt: `results/release-041-install-20261006-2008/receipt.json`. All payloads and
  the Plugins native copy match the package; the seven protected files kept their hashes.
- SessionTools was not re-released; the installed 0.4.0 probe folder (`Mods/ArtOfSimRally.DevRecorder`) was left as it was.
## Previous installation — 0.4.1-rc.2

Installed 2026-10-06 04:01:39 UTC with the game closed, from clean source
`ddabadaccb02f43a93a6492b0fb647555133b33e` (pushed to main 2026-10-06; not tagged). Same as
rc.1 plus the crash push + 25 Hz rattle chosen in the owner's CrashFeel run (KI-38).

- All 20 gates: `results/rc-0.4.1-rc.2-6f0e4ac0732d445f873d660c3afe5388/automated.json`.
- ZIP SHA-256: `506CA6C062DC3300639133B5582BBA764838ADD5F2ACCFE452980B491DE62111`.
- Backup/receipt: `results/rc-041-rc2-install-20261005-2301/receipt.json`. Payloads
  and the Plugins native copy match the manifest; protected files unchanged,
  Settings.xml still `8B32882D…`, so the game has not run since rc.1.
- In-game checks pending: KI-45's first-launch logging reset, the crash rattle
  (the saved Crash strength is 19.52381; the standalone pick was made at 50),
  and STD-005/006 camera feel.

Owner authorized merging the pending work on 2026-10-05 and will test tomorrow.
The five existing commits through e979c25 are now on origin/main. Offline force
normalization separately revalidated both original steering captures (11,921
rows, zero model/device mismatches); this does not accept the new crash cue or
camera/logging behavior. Installed package/settings remain as receipted above.

## Previous installation — 0.4.1-rc.1

Installed 2026-10-06 01:39:17 UTC with the game closed, from clean source
`c4b64809c2894cce1885b8254fd46007427b1f5e` (not yet pushed or tagged). Adds the
2026-10-05 T300 feedback fixes (KI-45: support logging off at each launch,
`ffb.log` trimmed over 8 MB, game steering options in support files) to the
unreleased STD-005/006 camera layout and steps. Stable public release is still 0.4.0.

- All 20 gates: `results/rc-0.4.1-rc.1-ead2ae530a3644dfb72a20fd533e2c10/automated.json`.
- ZIP SHA-256: `4996A5213703A46CB74ED347D394B4B67B7F70413110F707AC8C50131ECA2A8E`.
- Backup/receipt: `results/rc-041-rc1-install-20261005-2039/receipt.json`. Eight
  payloads and the Plugins native copy match the manifest; seven protected
  files kept their hashes, including Settings.xml (`8B32882D…`, unchanged since 0.4.0).
  The SessionTools probe folder was left as installed.
- The installed Settings.xml has `DiagnosticLogging` true, so the first launch
  should log "Log detail for support was left on…" and show the toggle off.
  That is KI-45's in-game check. Camera numpad feel (STD-005/006) is still pending.

## Stable release and previous installation — 0.4.0

Installed 2026-10-05 02:07:52 UTC and published 02:13:18 UTC from clean source
`30d421b26eccebc564cd50bf0733c1ed6a82d849`, tag `v0.4.0`. The normal unified
mod and separately opted-in SessionTools probe are installed. The owner accepted
the original session-5 route and requested release. The exact final artifact
then completed that route through finish/results/menu and closed normally.

- All 20 final gates: `results/rc-0.4.0-5e45855793444a9b8ac1c4e24a4d4d09/automated.json`.
- Normal ZIP SHA-256: `C498F8139FD64D13DD395CC036DE71C50614087C216280A17AA3A4724A96B3CA`.
- Optional ZIP SHA-256: `08DA5536F641E7ADAF7B8414DD3C45205A40A90F6F3A43629B14F2B5C8476C94`.
- Backup/receipt: `results/release-040-install-20261004-2107/receipt.json`.
  Eight normal payloads and the second native copy verified; six protected
  settings/game files retained their hashes. Optional seal and launcher matched.
- Final run: `results/sessions/2026-10-04-owner-session-5/replay-20261004-210753`.
  13,455 poses, zero detected position error, exit code 0; environment restored
  02:12:32 UTC. All 58 preferences retain their original types/bytes. Player.log
  reports native 2560x1440 FullScreenWindow, with no exclusive-mode fallback.
- [Published release](https://github.com/d-b-c-e/dbce-mods-art-of-rally/releases/tag/v0.4.0).
  Both archives and both checksum files independently downloaded and matched:
  `results/release-040-published-download/verified.json`.
- Physical output was muted during playback; original-signal analysis remains
  available. Owner visual acceptance is scoped to this route. KI-44's exact
  binding handoff, physical forces and broader hardware/layout cases stay open.

The release comes from `codex/session-playback`. Concurrent remote `main` has a
different component-layout migration; it is not the tested release source and
was not overwritten. Use tag `v0.4.0` or the exact published archives for recovery.

## Previous installation and stable release — 0.2.7

Published and installed **2026-09-30 04:40:57 UTC** with the game closed. This
final-labelled release promotes RC2's runtime behavior. The USB Bind recovery,
off-by-default shift vibration and all other settings are unchanged. The owner
requested stable promotion while physical shift feel, crash at strength 50%
and fresh T300/TSS binding remain unverified. The full attended matrix is open.

- Identity: `0.2.7+255c6f94d1e5ffeea3a6c95d5c7f3828231c3b3d.clean`.
- ZIP SHA-256: `4C0BC7C053F71206DAFD23EFE8F9D621DA673EAECD057C9CDAE01D7E81302577`.
- [Complete final local gate](../results/rc-0.2.7-81f05a57c0ca4f728ac80da0b2831bc4/automated.json):
  all 16 checks passed from clean source, including two recorded-drive cases,
  Unity Mono checks and installer tests. The generated manual checklist remains
  pending; no hardware cases were fabricated as passed.
- [Install receipt and RC2 backup](../results/release-027-install-e4dfcd0e6ae64b9cb3795c4b9e9e57d7/receipt.json):
  six payloads and the second native plugin copy match the tested ZIP. The
  settings file and five other protected files retained their pre-install
  hashes. The game was not launched during installation.
- [GitHub stable release](https://github.com/d-b-c-e/art-of-sim-rally/releases/tag/v0.2.7):
  the release is marked Latest. Independently downloaded ZIP and checksum match
  the tested local files; tag `v0.2.7` points to the clean source commit above.

## Previous installation and prerelease — 0.2.7-rc.2

Installed **2026-09-30 04:00:36 UTC** with the game closed. This candidate adds
an **off-by-default** gear-engagement wheel rumble, initial strength 5% and
range 0–20%, with a shared sine handle and landing/crash priority. The crash
waveform, saved crash strength, steering, telemetry and toolkit remain unchanged.
The owner's saved crash strength is 19.52381%: manually choose 50% for the
next physical comparison. See the [current investigation](research/2026-09-29-crash-shift-feedback.md).

- Identity: `0.2.7-rc.2+c12a826d6ad31a9948c88a7b3861d3e4e52c7678.clean`.
- ZIP SHA-256: `939F0FBA9D1327540F561B9809E21B46041EA0860484E620E1963E13B4878F8D`.
- [Complete local gate](../results/rc-0.2.7-rc.2-f1fa4f2f3f4a4e349b30a6272a010ece/automated.json):
  all 16 checks passed from clean source, including both recorded-drive cases,
  actual Unity Mono hook checks and installer tests.
- [Install receipt and RC1 backup](../results/shift-027-rc2-install-fdc1713154884071822df3e14839e28d/receipt.json):
  six payloads and the second native copy match the tested ZIP. Settings.xml
  and five other protected game/probe files retain their pre-install hashes.
- **Attended status:** not launched or driven after installation. Shift feel,
  real shift timing and crash strength 50% remain to be tested, as do T300/TSS
  and the broader hardware/layout matrix. This is a published test prerelease,
  not a stable-release hardware acceptance.
- [GitHub prerelease](https://github.com/d-b-c-e/art-of-sim-rally/releases/tag/v0.2.7-rc.2):
  both published assets were downloaded and matched the tested local ZIP and
  SHA-256 file. Tag `v0.2.7-rc.2` points to the clean source commit above.

## Previous local installation — 0.2.7-rc.1

Installed **2026-09-30 02:24:47 UTC** with the game closed, following the
0.2.6 T300/TSS binding report. The candidate retains the previous settings and
does not change steering, crash, landing, telemetry or toolkit payloads. It
keeps healthy USB readers open during routine Bind, retries an initially failed
reader, keeps Calibrate on the saved axis, and makes paused/assigned-controls
state visible at the top of Controls. This is an offline-validated candidate,
not a T300/TSS hardware fix confirmation. See [KI-43](KNOWN-ISSUES.md#ki-43--026-binding-and-calibration-may-not-capture-a-separate-tss-handbrake).

- Identity: `0.2.7-rc.1+48033d395a3b2de8a6d1bc3266d43c812d92654c.clean`.
- ZIP SHA-256: `6F59A0575AD390B2D8A4C65B49117473E758283EB6ACF043D5CE7002BE9999AE`.
- [Complete local gate](../results/rc-0.2.7-rc.1-bd73f449b8f442dab18c0884c1c2f76e/automated.json),
  SHA-256 `5FA8BFA7E677C606AF6D296935438248E92E354657F63A18CB79D9DDB7E1C950`:
  all 16 checks passed from a clean source; both recorded-drive cases passed.
- [Install receipt and 0.2.6 backup](../results/binding-027-rc1-install-9fecf946f0214f819912e9a330c83c48/receipt.json):
  six payloads plus the second native plugin match the tested ZIP. Settings and
  five other protected game/probe files kept their pre-install hashes.
- **Post-install owner test:** cleared and rebound the MOZA handbrake, then
  reported that the drive played well. The current game log recorded the bind;
  Settings.xml contains `MOZA R12 Base` axis 7 with explicit calibration and
  assigned controls enabled. This does not establish T300/TSS behavior, crash
  effect feel, complete UI layout or the full hardware matrix. The
  [attended checklist](../results/rc-0.2.7-rc.1-bd73f449b8f442dab18c0884c1c2f76e/manual.json)
  remains pending. No release publication is implied.

## Previous public release and installation — 0.2.6

Published [0.2.6](https://github.com/d-b-c-e/art-of-sim-rally/releases/tag/v0.2.6)
and installed **2026-09-27 06:02:52 UTC** with the game closed. The owner
accepted the preceding RC16 overall and requested publication. The final-labelled
artifact passed all 16 local gates, including the recorded-drive corpus, actual
Unity Mono checks and installer tests. The final build has not been driven; the
full attended matrix remains pending.

- Identity: `0.2.6+8fc5c4da3ff3e8228281bf46dc49e052b166f0ea.clean`.
- ZIP SHA-256: `B3BE9E06653A65385AAC7ACC3EC3D96E87FC9E47F581DE38E9FABC18B5391AC4`.
- [Final gate](../results/rc-0.2.6-4fb6adad132a419992299a3a48dd11e5/automated.json),
  SHA-256 `10A5C698731BFD9D6F57CDFBA715BD4A78206F34B74E4A2EC3827880D570538B`;
  16 passed checks from a clean source tree.
- [Install receipt and RC16 backup](../results/release-026-install-4b1c25d6403246c98b48010ffe906fd9/receipt.json):
  six mod payloads and the second native plugin match the ZIP. Settings and five
  other protected game/probe files kept their pre-install SHA-256 hashes.
- The published ZIP and checksum file were downloaded independently and match
  the local assets byte-for-byte. Download folder:
  `results/release-026-download-91a897b0a81e4172b3ba40ada3080887`.
- Official toolkit v0.15.0 repin changed VERSION/manifest labels only; its five
  payloads match the previously tested local toolkit pin. No recorder is shipped.
- No game launch, physical-force test, SimHub change or completed full attended
  matrix is implied by installation or the offline gate.

See [release review](archive/reviews/2026-09-27-release-0.2.6.md).

## Previous candidate — 0.2.6-rc.16

Installed **2026-09-23 04:58:32 UTC**, game closed, after all16 local RC gates.
The FFB device selector and its expanded choices are now bounded and show a
selected marker; the saved-disconnected row stays visible but disabled. Refresh
is a compact right-aligned command. The shared shifter picker has the same
choice treatment. FFB device identity, persistence, tune and input are unchanged.

- Identity: `0.2.6-rc.16+e854b994125494fb38442a1c41476c42befad934.clean`.
- ZIP SHA-256: `3577C288C08B68EF10C9431D4CB0DFBB3404DEADC3DB61B2184FCE524AA5877B`.
- [Complete gate](../results/rc-0.2.6-rc.16-dc65c1ebb31442f992ad29bb613369a6/automated.json),
  SHA-256 `469CD0D87AC329A61D9F858AD198BEB5EC06CF9D158E9C8431760F92950BB1B9`.
  Includes both recorded drives and all16 checkpoints. The first attempt used
  Windows PowerShell and stopped at an expected-failure replay check because
  stderr became a terminating error; the clean-source gate passed under pwsh.
- [Install receipt and RC15 backup](../results/ux-0.2.6-rc.16-install-36486d1e3ede46f4b1583ebd0cb4a7e3/receipt.json).
  All six mod payloads and the second native plugin match the archive. Six
  protected owner/game files retain their immediate pre-install hashes.
- Settings SHA-256 `4B8E04D1308CC329504C8024E0C860C609AC36EA3203B65D6F5809924EC726AD`;
  UMM scale remains selected, crash on19.52381 and landing on20. Toolkit
  binaries358add5/native0.8.0 and probe0.2.5.4 remain unchanged.
- No post-install game launch, rendered RC16 acceptance, physical-force test
  or public release. KI-41 and the wider KI-39/40 matrix remain pending.

## Previous candidate — 0.2.6-rc.15

Installed **2026-09-23 04:30:35 UTC** (September22 local), game closed, after
all16 local RC gates. RC15 defers the UMM→native bindings route until the actual
initiating control releases for two neutral frames, then clears the general menu
barrier before opening ControlsSettings. Persistent Rewired wheel/pedal axes and
parked joystick buttons cannot latch this explicit handoff. It also replaces
remaining full-width command/disclosure bars with compact actions, segmented
choices and flat links, and makes UMM scale the default for new settings.

- Identity: `0.2.6-rc.15+b0f8d4b2bc59a8080718d089d701f0bbc140201e.clean`.
- ZIP SHA-256: `7E4D7166F43E04D3C409E80F67FA44FDB9BFC368841C15D98F9BC99D78936B07`.
- [Complete gate](../results/rc-0.2.6-rc.15-1937e90db01845a2be10b3cb5ba83707/automated.json),
  SHA-256 `7910935679FD065104CE04C5D1442B0BFAD64ABDEC001B194F1F2B0EDB4D2E72`.
  Includes134 settings assertions,19 native-route assertions, both recorded
  drives and9,507 recorded landing/crash assertions.
- [Install receipt and RC14 backup](../results/ux-0.2.6-rc.15-install-07892038fed24701b7bf707ceda858cf/receipt.json).
  All six payloads and second native plugin match; six protected owner/game files
  retain their pre-install hashes.
- Settings SHA-256 `334F3BE34C33F6687FBBACD951F95BB6FB023FEE29D8FC783AE06DEE53942ACA`;
  UMM scale selected, crash on19.52381, landing on20. UMM Params and probe0.2.5.4
  were retained exactly. Toolkit binaries358add5/native0.8.0 are unchanged.
- Shared toolkit UX guidance `76cd02ac030c05cf79b7ed1aa83500d7015911c2`
  records compact-action and initiating-control-only handoff rules; documentation only.
- No game launch, attended bindings navigation, rendered RC15 acceptance,
  physical-force test or public release.

## Previous candidate — 0.2.6-rc.14

Installed **2026-09-23 03:59:32 UTC** (September22 local), game closed, after
all16 local RC gates. This successor responds to the owner's RC13 screenshots
with bordered higher-contrast cards, flat tabs with a selected underline, five
core pages without redundant Setup, smaller header/row commands and guidance to
their left. Old saved page values retain their meaning. No game was launched;
actual rendered fit and interaction remain pending (KI-41 and existing KI-39/40).

- Identity: `0.2.6-rc.14+8d37db001aea2ff21ca30455723f604523d78ac4.clean`.
- ZIP SHA-256: `68E956B06FC421D2A6C0215BF885033A22268A75DB252407D394EC26A35E7EFA`.
- [Complete gate](../results/rc-0.2.6-rc.14-84916e9de11f4b7bac505ba5118892b8/automated.json),
  SHA-256 `E6677D44D96675E5455BF2C6642DD43A839D7ED2214860C88E32285CAB23F548`.
  Includes130 settings policy assertions, both recorded drives and9,507 recorded
  landing/crash assertions. The suite compiles the mod but does not render Unity IMGUI.
- [Install receipt and RC13 backup](../results/ux-0.2.6-rc.14-install-3de661486d8f4ce8981448ab5cd196a8/receipt.json).
  All six mod payloads and second native plugin match. The six current owner/game
  files are byte-identical before/after installation.
- Settings SHA-256 `A9291BDBE10AE1DC667A8CA20F48E0EF8D529F11F9F28500284419090F536536`;
  crash on19.52381 and landing on20. UMM preferences and probe0.2.5.4 retained.
  Toolkit binaries358add5/native0.8.0 remain unchanged in both plugin locations.
- Shared UX guidance `be5519541820ce3408953588d78f16e78794f1d6`; no physical-force
  test or public release.

## Previous candidate — 0.2.6-rc.13

Installed **2026-09-22 04:15:29 UTC** (September21 local), game closed, after
all16 local RC gates and independent source review. Binding groups and local
preconditions, camera keyboard/USB ownership, Show/Hide disclosures and compact
header/Setup address the owner's clutter report. No game was launched; actual
rendered fit and interaction remain pending (KI-41, plus existing KI-39/40).
See the [visual grouping review](archive/reviews/2026-09-21-visual-grouping.md).

- Identity: `0.2.6-rc.13+470e398b89198d60c861ce3ffad800b0ff93854a.clean`.
- ZIP SHA-256: `E9F37818C82D957DE67EC5E039BEF89AE6FDD55B872FFFB4397993E26297A058`.
- [Complete gate](../results/rc-0.2.6-rc.13-56173d7bf1914b7aaa76da395dbc7bbe/automated.json),
  SHA-256 `BC85F5E0561CF7FA3B6C09EEE689BE82BE6C066D3B77BD374DD988FBE5E0E61D`.
  Includes126 settings policy assertions, both recorded drives and9,507 recorded
  landing/crash assertions. The settings suite does not render the panel.
- [Install receipt and RC12 backup](../results/ux-0.2.6-rc.13-install-f013528bf9c24b15bbf08ca587f0264e/receipt.json).
  All six mod payloads and second native plugin match. The six current owner/game
  files are byte-identical before/after installation. This preserves the owner's
  latest settings rather than restoring the older September19 snapshot.
- Settings SHA-256 `A9291BDBE10AE1DC667A8CA20F48E0EF8D529F11F9F28500284419090F536536`;
  crash on19.52381 and landing on20. UMM preferences and probe0.2.5.4 retained.
  Toolkit358add5/native0.8.0 remains unchanged in both plugin locations.
- Shared visual guidance `673d451`; no physical-force test or public release.

## Previous candidate — 0.2.6-rc.12

Installed **2026-09-19 20:35:39 UTC**, game closed, after all16 local gates and
independent source closure of the custom-scale row overflow. RC11 was validated
but not installed. RC12's limited default4K menu check passed; the full rendered
and hardware matrix remains pending. No public release.

- Identity: `0.2.6-rc.12+cbcf76c5d6401dd8a0c511acfe7ded201df6a99d.clean`.
- ZIP SHA-256: `88A9B71D43F4A0788112CC987EEEB02407BCB43F9D6AECDA8BCAC93F1927B0C4`.
- [Complete gate](../results/rc-0.2.6-rc.12-bc652082c30b43dba419e09883d67c42/automated.json),
  SHA-256 `9FC72450E3F8306FE9AF44FC52AA112E07A62008D6D70523073B3F5ED3E32856`.
- [Installation receipt and RC10 backup](../results/ux-0.2.6-rc.12-install-787e550c1c6b4fa2bc307e3efb32d5ae/receipt.json).
  All six mod payloads and second native DLL match. Settings, UMM, game assembly
  and probe hashes are unchanged. Crash on19.52381, landing on20; settings SHA
  `C373DA18429788ACDFEF7A032496981BDCC5571D7285543B249769846E5876EF`.
- 126 settings assertions,13 Unity Mono hook/host-size checks, both recorded
  drives and9,507 landing/crash assertions pass. Toolkit358add5/native0.8.0 and
  probe0.2.5.4 unchanged. Exact artifacts/evidence155files hash-verified in main.
- The [smoke/correction review](archive/reviews/2026-09-19-rc10-ui-smoke.md) distinguishes
  RC10's narrow keyboard pass from its failed layout and RC12's limited layout pass.
- [RC12 smoke and restoration receipt](../results/ux-rc12-smoke-d24e3e71d58f498699f4eba1ea8c28f7/receipt.json):
  readable header/all six page buttons and five Simple page tops fit the default
  4K host; Escape closes only the panel and subsequent native Quit works.
  Normal exit and all16 protected hashes restored at20:51:49.521UTC; no output
  or recording. The remaining camera check followed in a second run.
- [RC12 camera cancellation and restoration receipt](../results/ux-rc12-smoke-74c19cbf1bf5462ead1dad05bbdaff67/receipt.json):
  Cameras and nested scrolling fit; Bind→Escape at8.218s canceled the active edit
  without closing the panel or activating native Quit. Advanced Cameras top
  was observed. Normal exit and all16 protected hashes restored at21:05:25.672UTC.
  Comparison of original/after-smoke XML found only the intentional temporary
  FFB/telemetry Off changes; bindings and force strengths were unchanged. Both
  original files are restored byte-for-byte. Full Advanced/scale/input coverage
  remains pending; no output, driving or recording occurred.

Before driving, choose **F6 → FFB → Use steering wheel** or reselect the MOZA;
the saved old name/index lacks an FFB GUID. The installer preserves that choice.

## Previous candidate — 0.2.6-rc.10

Installed **2026-09-19 20:10:28 UTC**, game closed, after all16 local gates.
This replaces RC9's confirmed menu leakage and small4K content with reviewed
dispatch/held-release guards and scoped Auto/UMM text scaling. Live keyboard
isolation/recovery passed, but default4K layout clips controls (KI-40); see
[RC10 smoke](archive/reviews/2026-09-19-rc10-ui-smoke.md). A layout correction/retest is
pending; this is not a public release or complete UX-1 acceptance.

- Identity: `0.2.6-rc.10+2ab6fb5f27be570693ebe856b48259456f55007f.clean`.
- ZIP SHA-256: `F4A46410F5B8F9CD82121C8B183BC161E2788BB47E6F0A1BFF222FC7AD89B9E6`.
- [Complete gate](../results/rc-0.2.6-rc.10-49b815102d9e4f43962f90e1eb0f4d04/automated.json),
  SHA-256 `96B492FFDACB6CCA3BB42B0AE1A404F94ADFFA2AEC8E4B8CD34D490E422B617F`.
- [Install receipt and RC9 backup](../results/ux-0.2.6-rc.10-install-073c05f30a374727b3a29ac3ac50046a/receipt.json).
  All six mod payloads and second native DLL match; settings, UMM preferences,
  game assembly and separate probe are byte-identical. Crash on19.52381,
  landing on20; settings SHA `C373DA18429788ACDFEF7A032496981BDCC5571D7285543B249769846E5876EF`.
- 96 settings checks, actual Unity Mono hook attachment, two real captures and
  9,507 landing/crash assertions pass. Saved landing remains row4230. Toolkit
  358add5/native0.8.0 and probe0.2.5.4 unchanged; no recording or force test.
- Exact artifacts, gate/failed-first-gate and installation evidence are copied
  and hash-verified in main's ignored dist/results. Public stable remains0.2.5.

Before the next drive, explicitly select **F6 → FFB → Use steering wheel** or
reselect the MOZA: the saved old name/index has no FFB GUID. Installation does
not guess an output device. See [RC9 failure and successor review](archive/reviews/2026-09-19-rc9-ui-smoke.md).

## Previous candidate — 0.2.6-rc.9

Installed **2026-09-19 19:31 UTC** with the game closed after all 16 local gates.
RC9 aligns the installer's completion text with the new F6/FFB/Controls/Help
paths; runtime source is unchanged from RC8. It is not a public release.

- Identity: `0.2.6-rc.9+44e11dd7a7c0e6dd99cef7f2c8be0620e9ce717a.clean`.
- ZIP SHA-256: `8D92D7758AD25F8F62A870A4275B72BE685C533D79FF3976A98FE17EDA3E1199`.
- [Complete gate](../results/rc-0.2.6-rc.9-6506d4fa38bf41eeb011083b62204b39/automated.json)
  and [installation receipt/RC8 backup](../results/ux-0.2.6-rc.9-install-3570eb7cbaf247c4b2c74ee9824ef17a/receipt.json).
  Original RC6 backup remains in the RC8 receipt below. Exact artifacts/evidence
  are hash-verified in both the UX worktree and main checkout.
- All six mod files and second native copy verified. Settings/probe/game/UMM
  files remain byte-identical; crash on/19.52381 and landing on/20, settings SHA
  `C373DA18429788ACDFEF7A032496981BDCC5571D7285543B249769846E5876EF`.
- Owner's old FFB selection has a name/index but no verified GUID. Before the
  next drive, use **F6 → FFB → Use steering wheel**, or explicitly reselect
  the MOZA. Steering already has a saved GUID; the new strict selector never
  guesses an output device from an old name/index. No setting was silently changed.
- The no-output UI smoke found native-menu leakage and small4K text (KI-40).
  Normal Quit and exact settings/UMM/payload restoration completed19:52:01UTC.
  See [the evidence and successor retest](archive/reviews/2026-09-19-rc9-ui-smoke.md).
  KI-38 crash feel and KI-39 full hardware/layout remain open. Toolkit358add5
  and probe0.2.5.4 are unchanged; no recording or SimHub edit.

## Previous candidate — 0.2.6-rc.8

Installed **2026-09-19 19:21 UTC** with the game closed after all 16 local gates.
The new Simple/Advanced settings pages include transactional binding/calibration,
USB shortcuts, persistent Stop FFB, and native keyboard conflict checks. Use
[the candidate guide](UX-SETTINGS-CANDIDATE.md); public 0.2.5 has the older UI.

- Identity: `0.2.6-rc.8+2517162d71fc12e030ecda64d8b46d31f0d56b20.clean`.
- ZIP SHA-256: `0FFB6CEAE47E7268AD2A4815B69F69984D8CEA56C7028A5377E1594B337352C3`.
- [All 16 gates](../results/rc-0.2.6-rc.8-a16bd4f3f545419d91ccc2ca7f774b90/automated.json)
  passed, including both real drives, 9,507 landing/crash assertions, Unity Mono
  hook compatibility and installer entrypoints. No physical wheel output.
- [Installation receipt and RC6 backup](../results/ux-rc8-install-8a331d2029c14571892bb2e2cb0f5b4f/receipt.json):
  fresh ZIP extraction and all six installed payloads plus the second native copy
  verified. Settings, game assembly, UMM parameters and probe files are unchanged.
- Saved **crash on/19.52381, landing on/20**; settings SHA-256
  `C373DA18429788ACDFEF7A032496981BDCC5571D7285543B249769846E5876EF`.
- Toolkit `local+358add53af220eb95bf606e240b230e23a6ea197.clean`, native SHA-256
  `A07DDF7E10ADBD016DB204324D5E035B951E21A7D8DD373405C91257EB7AD288`, unchanged
  from RC6. Probe remains 0.2.5.4; no recorder is packaged. Crash feel KI-38 and
  the settings hardware/layout matrix KI-39 remain open.
- Source fast-forwarded and pushed to main with `[skip ci]`; built locally.
  UMM displays 0.2.6; support/build metadata identifies RC8. No public release.

Built in `E:/Source/art-of-sim-rally-ux`; the exact ZIP/checksum, gate evidence,
RC6 backup and receipt are hash-verified in main's ignored `dist`/`results` too.
RC7 passed offline but was not installed; RC8 adds native keyboard conflict checks.
An allocated, force/telemetry-disabled UI smoke remains pending at installation.

## Previous candidate — 0.2.6-rc.6

Installed **2026-09-17 05:05 UTC** with the game closed after all 16 local
gates. Crashes use method A's finite constant-force push/release, with a
new-settings default of 50 and range 0–100. Landing stays default 5/range 0–40.
The stronger in-game effect still needs an attended test; KI-38 remains open.

- Identity: `0.2.6-rc.6+1cd7814ac20c647f176c57925875e6af47858e91.clean`.
- ZIP SHA-256: `0FDAE9D3822F69F3F83212BD4B1FEFDBE356528C899CD2CC057930B90D352335`.
- [All 16 gates](../results/rc-0.2.6-rc.6-dea9d3926a274f2f923aed528a1140e8/automated.json)
  passed, including both recorded drives, 9,503 landing/crash assertions,
  actual Unity Mono hooks and real installer entrypoints. The saved jump still
  has one landing at row 4230. These checks do not apply physical wheel force.
- [Installation receipt and RC4 backup](../results/constant-crash-rc6-install-4c471c524bbc43d083915f16aee51942/receipt.json):
  all six mod payloads and the second native copy match the package. Settings,
  game assembly, UMM parameters and separate probe files are byte-identical.
- Preserved settings: **crash on/19.52381, landing on/20**. Select crash strength
  **50** manually for comparison; installation does not overwrite a saved tune.
  Settings SHA-256: `C373DA18429788ACDFEF7A032496981BDCC5571D7285543B249769846E5876EF`.
- Toolkit pin: `local+358add53af220eb95bf606e240b230e23a6ea197.clean`, unpublished
  package 0.15.0/native 0.8.0. Native SHA-256:
  `A07DDF7E10ADBD016DB204324D5E035B951E21A7D8DD373405C91257EB7AD288`.
  The native API, tests and reusable findings are pushed upstream. Probe stays
  0.2.5.4; no game launch, recording, Pit House or SimHub change.
- UMM displays **0.2.6**; support/build metadata identifies **0.2.6-rc.6**.
  [Implementation, upstream contribution and testing limits](archive/reviews/2026-09-17-constant-crash.md).
  Public stable is 0.2.5. Official toolkit repin and attended checks remain.

Built in `E:/Source/art-of-sim-rally-timing`; exact ZIP/checksum, gate evidence,
backup and receipt are also retained in the main checkout's ignored `dist`/`results`.
Preliminary RC5 passed all gates but was not installed: its packaged guide still
described the old effect. RC6 corrects those instructions with the same runtime.

## Previous candidate — 0.2.6-rc.4

Installed **2026-09-17 03:57 UTC** (09-16 local) with the game closed after all
16 local gates. Crashes now request a peak-start kick and fading rebound at the
same configured amplitude. Landing requests, steering and telemetry are unchanged.
**Attended follow-up failed:** owner felt no crash effect at about 20 in RC4,
while normal steering worked. Three shape-accepted commands completed without
early managed stops. KI-38 remains open; RC4 is not release-ready.
[Support analysis](archive/reviews/2026-09-17-rc4-crash-feel.md).

- Identity: `0.2.6-rc.4+91cedd8307010c796efe79590661368a52b56d27.clean`.
- ZIP SHA-256: `39643F929A680027937B195F2B18623DC69C289229A65C1AE27AE7AD0078F2BA`.
- [All 16 gates](../results/rc-0.2.6-rc.4-b232db3bfe2346e7b200c2059258a9a3/automated.json)
  passed, including both real corpus cases, 9,342 landing/crash assertions,
  Unity Mono checks and real installer entrypoints. No physical wheel output.
- [Installation receipt and RC3 backup](../results/crash-kick-rc4-install-87c17f07d71242bba8ddc5faf1cbb770/receipt.json):
  all six mod payloads and the second native plugin copy match the exact package.
  Settings, game assembly, UMM parameters and separate probe files are byte-identical.
- Preserved owner settings: **crash on/19.52381, landing on/20**. Settings SHA-256:
  `C373DA18429788ACDFEF7A032496981BDCC5571D7285543B249769846E5876EF`.
  These supersede RC3's earlier installation-time crash off/5 values.
- Toolkit pin: `local+c319b0258b11d1f07492a48dbc110d3e50c14dfd.clean`, unpublished
  package 0.14.0/native 0.7.0. Native SHA-256:
  `BCA81756B5FD920802D51C111AB55D5ED7F7219FCEA40588A65707A89D226EA4`.
  Probe stays 0.2.5.4. No game launch, recording, Pit House or SimHub change.
- UMM displays **0.2.6**; support/build metadata identifies **0.2.6-rc.4**.
  [Candidate behavior, evidence and test plan](archive/reviews/2026-09-17-crash-kick.md).
  Public stable remains 0.2.5; official toolkit repin and attended checks remain.

Built in `E:/Source/art-of-sim-rally-timing`; the exact ZIP/checksum, gate evidence
and installation backup/receipt are also retained in the main checkout's ignored
`dist`/`results`, with copied artifact hashes verified.

## Previous candidate — 0.2.6-rc.3

Installed **2026-09-17 03:23 UTC** (2026-09-16 local), with the game closed, after
all 16 local gates. Both wheel-impact sliders now extend to 40%. Existing values
retain their output, so the owner's landing on/20 is unchanged. Crash stays
off/default 5. Telemetry boosting is explicitly deferred; no SimHub gains changed.

- Identity: `0.2.6-rc.3+b85c27a735eac7f55661cd84e9f39b0a20d17f7f.clean`.
- ZIP SHA-256: `E1C784BC889A720D3C621EF88E258ECEE10471D8665A65C4287293277C18BE15`.
- [All 16 gates](../results/rc-0.2.6-rc.3-d692d149e51b4b70bcb0c4bc561298e6/automated.json)
  passed, including both real corpus cases and 9,248 landing/crash assertions.
  Increased output limits, overlap ownership, stop behavior and settings roundtrip
  have offline coverage; actual stronger wheel feel remains untested.
- [Installation receipt and RC2 backup](../results/strength-rc3-install-d28b515512a34942a5c10c168023dc3b/receipt.json):
  six mod payloads and the second native copy match the exact package. Settings,
  game assembly, UMM parameters and separate probe files remain byte-identical.
- Settings SHA-256: `3819C39EABB5B7FE151E08547251F18285812664F5B5256DF4FE512A02E307B1`.
  Toolkit remains the same local/unpublished native 0.6.1 candidate; probe remains
  0.2.5.4. No game launch or recording. Public stable is still 0.2.5.
- UMM shows **0.2.6**; support/build metadata identifies **0.2.6-rc.3**.
  [Change and test notes](archive/reviews/2026-09-16-stronger-impact-effects.md).
  Public release still needs official toolkit repin and attended checks.

Built in `E:/Source/art-of-sim-rally-timing`; exact ZIP/checksum, gate evidence and
deployment backup/receipt were also copied to the main checkout's ignored
`dist`/`results`, with copied artifact hashes verified.

## Previous candidate — 0.2.6-rc.2

Installed **2026-09-16 21:24 UTC**, with the game closed, after all 16 local gates.
Removes default per-force native disk tracing and adds smaller-hitch/last-landing
diagnostics. Landing timing/gain, steering arithmetic, telemetry and crash policy
are unchanged. Settings are byte-identical, landing on/20 and crash off/default 5.

- Identity: `0.2.6-rc.2+6f7302595b25e8196e67af2591cf5a15780c82c0.clean`.
- ZIP SHA-256: `4DC58A75A5417629FF42748AA34C50C3AE7BBEA976DF793F1DF4E5E16EA10AD0`.
- [All 16 local gates](../results/rc-0.2.6-rc.2-272c0b582dc449df8ce4a0bb348b388b/automated.json):
  native binding/version checks, arithmetic/lifecycle, actual Unity Mono hooks,
  both recorded corpus cases, unchanged single landing, support persistence,
  replay protocol, package and installer checks passed.
- [Installation receipt and RC1 backup](../results/timing-rc2-install-a56c9d50a03048b4bd4f72a2d60fdfad/receipt.json):
  all six payloads and the second native copy match the package. Settings, game
  assembly, UMM parameters and every separate probe file are preserved.
- Toolkit is **local/unpublished** `82c789117f115034a074bcb93133fefc8b955e35.clean`,
  managed 0.13.1/native 0.6.1. Final release is blocked until official repin.
  Native SHA-256: `FE85A1ECEC10E84134EA14F8362683293894BE8D485076F8CB38CA22AE9FF2F0`.
- Separate probe remains 0.2.5.4. No game launch, recording, physical force test,
  SimHub/profile adjustment or public publication. UMM shows **0.2.6**;
  support/build metadata identifies **0.2.6-rc.2**.
- [Investigation and attended comparison](archive/reviews/2026-09-16-landing-startup-investigation.md).
  Haapajarvi startup/early landing are not reproduced; normal FFB with the new
  native candidate and full attended matrix remain pending. Public stable is 0.2.5.

Build/evidence originated in isolated `E:/Source/art-of-sim-rally-timing`.
Exact package, gate logs and deployment backup/receipt were also copied into
the main checkout's ignored `dist`/`results`; copied package hash was verified.
The prior failed version-guard run is retained as
`results/rc-0.2.6-rc.2-00bbd090d8b54c91a383120dd60c53c6`.

## Previous candidate — 0.2.6-rc.1

Installed **2026-09-16 20:06 UTC**, with the game closed, after all 16 local gates.
Optional crash vibration defaults **off**, strength 5/cap 20. Owner settings are
byte-identical, including landing enabled/20. Public stable remains 0.2.5.

- Identity: `0.2.6-rc.1+2e32256f99514db9a01f72888698726d31f4b2b9.clean`.
- ZIP SHA-256: `23131C59FEB11409D42C8379B6B5360FA15FCB36CBFA9C2C239868D2B1660AB3`.
- [All 16 local gates](../results/rc-0.2.6-rc.1-4036497765f24657a723521a0dfe365f/automated.json):
  both real corpus cases, unchanged single landing, crash/overlap lifecycle,
  actual Unity Mono shipping/probe hooks, packaging and installer checks passed.
- [Installation receipt and 0.2.5 backup](../results/crash-rc1-install-12724d6ddcfa4bdb9a13d4cabff440ec/receipt.json):
  six mod payloads and the second native copy match the exact package. Settings,
  game assembly, UMM parameters and all separate probe files are preserved.
- Developer probe remains 0.2.5.4, SHA-256
  `4FBACE0242834376CEA8903AAA0A08C86D3A6EBA83E63AF0659BD92518916EE7`.
  Toolkit remains v0.13.0/native 0.6.0. No game launched, no recording started.
- Steam/Stream Deck target and SimHub profile/gains unchanged. UMM shows numeric
  **0.2.6**; build/support identify **0.2.6-rc.1**.
- [Crash test plan](CRASH-EFFECTS.md); live contact detection, timing, feel and
  SimHub input/output comparison remain pending. No public publication.

## Previous installed stable — 0.2.5

**[0.2.5 is published](https://github.com/d-b-c-e/art-of-sim-rally/releases/tag/v0.2.5)
and was installed on 2026-09-13 03:57 UTC.** Wheel landing vibration defaults on
at strength 5; existing saved settings, including the owner's enabled/20, remain.
The accepted built-in SimHub profile and gains are unchanged.

- Identity: `0.2.5+c6242a0a163315f7a390d3860c6204b4ca215619.clean`.
- ZIP SHA-256: `8A7F9FC044058B0890F7323138E3DFCBC998B31C67C57E14272D410C6BFCA502`.
- [All 16 final local gates passed](../results/rc-0.2.5-ab78d6a9af8446b9ac562904e7062c6a/automated.json), including actual Unity Mono and the real drive corpus.
- [Installation receipt and RC8 backup](../results/release-025-install-2bd3016bb6334b8ea4effb6142c15c2b/receipt.json): all payloads verified, settings and separate probe 0.2.5.3 preserved. Game closed throughout; Steam/Stream Deck target unchanged.
- [Published download verification](../results/release-025-published-b7832204b25e4f5cb6d1db48bd0adc3f/verification.json): ZIP and checksum match the exact final artifact.
- Official toolkit v0.13.0 replaces the local pin with byte-identical RC8 binaries. No SimHub helper or recorder is shipped.
- [Owner acceptance and release limits](archive/reviews/2026-09-13-release-0.2.5.md): overall RC8 acceptance authorizes publication; full attended matrix and a separate final-artifact drive remain unverified.

## Crash recorder update — 2026-09-15 UTC

The separately installed developer probe is now **0.2.5.4/schema 4**, ready to
observe body collisions during the next explicitly started recording. Stable
0.2.5 and the installed settings remain unchanged. No recorder is shipped in the
release package; nothing is recording and the game is closed.

- Probe SHA-256: `4FBACE0242834376CEA8903AAA0A08C86D3A6EBA83E63AF0659BD92518916EE7`.
- [Install receipt and 0.2.5.3 backup](../results/crash-probe-install-0a257bd79c1e403882130196c3d7421d/receipt.json):
  exact gated DLL/manifest installed, 13 game/mod/settings hashes preserved.
- [All 16 local gates](../results/rc-0.2.5-rc.11-104bedc73e144db98e651c4c22d27263/automated.json),
  including both real drive cases and actual-Mono collision hook attachment.
- The local rc.11 ZIP is packaging validation only, not a changed game build to
  deploy. No public release or SimHub profile change.
- [Crash baseline, fixes and live-test limits](archive/reviews/2026-09-15-crash-capture.md).

## Documentation/installer audit — no game deployment

The 2026-09-13 audit validates revised installer/readme packaging as local
`0.2.5-rc.10`; all 16 gates pass. It changes no game/force code. Installed stable
0.2.5 above remains the current game payload, and published assets are unchanged.
The new installer/template are for the next package; no game redeployment was
needed. [Audit and exact validation evidence](archive/reviews/2026-09-13-docs-install-audit.md).

## Previous candidate — RC8 restored after RC9 withdrawal

**RC8 restored on 2026-09-13 01:36 UTC.** The owner rejected RC9's extra SimHub
helper; its game code/plugin/profile were removed. The exact RC8 archive below
was reinstalled, with settings/probe preserved and the Stream Deck target unchanged.
[New receipt and RC9 backup](../results/landing-rc8-install-c9b92ca721ed45d892901e8381a45b75/receipt.json).
SimHub now has a built-in-effects-only 30 Hz comparison profile; the original
profile/gains are preserved. [Correction, evidence and retest](archive/reviews/2026-09-12-builtin-shaker-correction.md).

**0.2.5-rc.8** was first installed on 2026-09-12 23:54 UTC. It adds opt-in landing
vibration with independent strength to RC7. The existing settings were preserved;
landing vibration defaults to off, with strength 5 when enabled.

- Identity: `0.2.5-rc.8+b9598f5cb6b0a48965b37f4f8d27f350fd59be49.clean`.
- ZIP SHA-256: `90F3F0BD942FD6F446702BAF185842196090B54882D821AE737AE1046CDAFADB`.
- [All 16 gates passed, including the real drive corpus and landing detection](../results/rc-0.2.5-rc.8-669ae7f5aae140ca9e793fd9ad0b1781/automated.json).
- [Installation receipt and RC7 backup](../results/landing-rc8-install-a1e5dbdfefd9432d98ebeea43f31b65d/receipt.json).
- Six mod payloads and the native plugin copy match the exact validated package.
  Settings and separate probe 0.2.5.3 were preserved byte-for-byte. The game stayed
  closed; the Steam/Stream Deck installation target is unchanged.
- Toolkit pin is `local+dd0ef20ad0cdaccc7a67f10a707dbd2a27a6efe9.clean`, managed
  0.13.0/native 0.6.0. This is an unpublished development candidate. Final
  packaging rejects local toolkit pins; official publication/repin is required.
- [Landing A/B test](LANDING-EFFECTS.md), FFB startup/recovery, digital gauge
  clearing, quit and font scaling remain attended checks. The first nonzero
  [owner landing test](archive/reviews/2026-09-12-landing-wheel-test.md) logs eight accepted
  wheel cues. Owner says wheel FFB is fine; the desired stronger ButtKicker thud
  is a separate telemetry/haptic-output follow-up.
  No public release; stable remains 0.2.4.

## Previous candidate — RC7

**0.2.5-rc.7** was installed on 2026-09-12 16:44 UTC. It adds the KI-32 font
scaling correction and clearer handbrake/logging/smoothing help to RC6.

- Identity: `0.2.5-rc.7+8dfa61c5fb2212273059ec3714421c9e4fc7debe.clean`.
- ZIP SHA-256: `948D410F34102C7739ECD5148E81EA8EEDE3A29250BEF7C16BFDAE5F9343E0F2`.
- [All 16 gates passed, including the real drive corpus](../results/rc-0.2.5-rc.7-cf98987b0c984f4eb37175090bafa10f/automated.json).
- [Installation receipt and RC6 backup](../results/support-questions-rc7-install-38dee4a087ec466c92ca750c9b2b800b/receipt.json).
- Six mod payloads and the native plugin copy match the exact validated package.
  Settings and the separate 0.2.5.3 probe were preserved byte-for-byte. The game
  stayed closed; the Steam/Stream Deck installation target is unchanged.
- Font scaling still needs an in-game visual check; all prior hardware gates
  remain pending. No public release or support reply was sent. Stable is 0.2.4.

## Previous candidate — RC6

**0.2.5-rc.6** was installed on 2026-09-11 03:34 UTC, from the exact package
that passed all 16 local gates **including one real recorded drive**.

- Identity: `0.2.5-rc.6+8ea54775efa42fe60b367760c72bb0f9668a1c9a.clean`.
- ZIP SHA-256: `A0A99CFA2678142D09C1F63F391D5127706B171C98FC7FF9F84B2B39FD39A4E4`.
- [Gate](../results/rc-0.2.5-rc.6-3166e9988c98438dbf669170f92253c7/automated.json),
  [installation receipt and RC5 backup](../results/bug-investigation-20260911/install-rc6/receipt.json).
- All six installed payloads and the second native plugin copy match the package;
  settings were preserved byte-for-byte. The Steam/Stream Deck game directory
  remains the same. Game stayed closed; no public release or attended pass.
- Optional developer probe **0.2.5.3** is installed, hash
  `F911A673F992963C99E5A10D048C9F849D82A8582BBC87EF9B9D64E815778D49`.
  It remains separate from the release ZIP; previous probe backed up.
- Local DSS speed/RPM dashboard bindings were backed up and repaired. SimHub
  exited normally and restarted on 03:35 UTC; logs confirm both round displays
  reloaded their dashboard. Physical end-of-race clearing remains untested.
- [Fixes, signal study and remaining tests](archive/reviews/2026-09-11-bug-follow-up.md).

## Previous candidate — RC5

**0.2.5-rc.5** was installed after passing all 16 local checks, with settings and
the Steam 550320 launch target preserved. [Current candidate evidence and receipt](archive/reviews/2026-09-09-overnight-025.md).
The earlier 0.2.4 installation below is backed up. No candidate hardware result
or public publication was implied by deployment. The subsequent owner drive
reported FFB startup and telemetry gauge failures (KI-28/KI-29); RC5's attended
gate now fails those cases. Do not publish this candidate.

- UMM displays **0.2.5**; support/build.json identify
  `0.2.5-rc.5+43e0b4a2978121e712d20c8f170567b587554bdf.clean`.
- [RC5 install receipt and RC3 backup](../results/overnight-025-install-156deb45275a414bba20b5ee1f2135a5/install-receipt.json).
- Six mod payloads and the native plugin copy match the exact package; settings
  were unchanged at deployment. Game stayed closed, developer probe was absent,
  Stream Deck preserved.
- RC4 passed offline but was superseded before installation by the shifter
  selection-display correction in RC5. Published stable remains 0.2.4.

For the owner's attended drive on 2026-09-10 local time, the separate developer
probe **0.2.5.2** is installed after correcting KI-26 and adding the KI-27 menu-quit
save hook. Explicit menu-only saving and CSV hashes passed in Unity; the automatic
quit-save path still needs a drive. RC5/settings were preserved during replacement. The probe
was present; remove the current probe with the game closed before the comparison drive
without instrumentation. [Capture setup and receipts](archive/reviews/2026-09-10-attended-rc5.md).

## Previous stable installation

- **0.2.4 stable**, installed 2026-09-09 03:09:23 UTC with the game closed, from
  the downloaded [GitHub release](https://github.com/d-b-c-e/art-of-sim-rally/releases/tag/v0.2.4).
- Identity: `0.2.4+dc14fe70002205e59c8b462c8c8636b72132abdd.clean`.
- UMM displays **0.2.4**; build.json/support now show the final identity without an RC suffix.
- ZIP SHA-256: `08AA70055C4957DA8C96EAB2078BA0F52211E51B7249C38A0B409BBC6AD50942`.
- [Install receipt and RC5 backup location](../results/release-0.2.4-install-3b9ec98a27bf415b807d7974ff3484c3/install-receipt.json).
- All six mod payloads plus the second native plugin copy match the published
  package manifest; Settings.xml preserved byte-for-byte. RC5 install/settings
  backed up before replacement. Published asset digests independently verified.
- Existing Steam 550320 Stream Deck button launches this installation; developer
  recorder remains absent. No game was launched as part of deployment.
- [Final release evidence](archive/reviews/2026-09-09-release-0.2.4.md): all 16 local
  automated checks pass. Owner accepted RC5, which has the same production source
  and toolkit; detailed attended cases and final-labelled drive remain pending.

## Deployment rules

1. Use the newest eligible local artifact on this project's current development
   line. Validate its clean source identity, all required automated checks and
   archive hash against `results/rc-*/automated.json`, using
   `tools/testing/rc_gate.py`'s `verify_automated` where useful. Exclude candidates
   with failed attended cases, withdrawn/superseded builds and version downgrades.
   An unchanged installed identity needs no deployment or rebuild.
2. If `artofrally.exe` is running, defer. Never close the game to perform a routine
   update. Extract the exact validated ZIP, validate its manifest, and back up
   the existing mod, Settings.xml and `Plugins/x86_64/UnityForceFeedback.dll`.
3. Run the extracted package's `install.ps1 -GameDir` against the existing install,
   then match all six mod files, native plugin copy and build identity to the
   manifest/report. Verify the settings hash is unchanged; retain the backup and
   report failures instead of claiming success.
4. Save a local receipt under `results`, update this installed-candidate record
   and session handoff, and report the actual deployed version. Preserve wheel
   tuning, controls, Stream Deck action and optional developer-probe state. Do not
   launch a game or mark attended checks passed as part of deployment.

## Feature completion checklist

- [ ] Build and validate the completed feature/fix in an immutable local artifact.
- [ ] Deploy that exact artifact with the game closed, backing up the previous
  install and preserving settings.
- [ ] Verify all payloads, native plugin copy, identity and unchanged settings;
  save the receipt and report the installed version in the handoff.

If the game is running, leave deployment pending and pick it up at the next active
work session. Do not close the game or set up periodic checks. The owner stopped
scheduled polling on 2026-09-09 UTC; automation
`keep-art-of-sim-rally-installed-build-current` is paused. An unchanged build or
documentation-only work needs no rebuild or redeployment.
