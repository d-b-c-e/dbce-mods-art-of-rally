# Shared input wire contract

Art consumes the existing shared input shim's experimental C ABI 2 in private
shared-input candidates. It does not fork the profile parser, binding resolver,
calibration, device selection or normalization. The historical source slices below
record their original scope. Current private-package pins are in `CANDIDATE.json`;
the default package ships no shim.

The initial `dbce_shim.h` came from DRIVE main
`f161c02d7ea7eba898aac5b18cc56194522d160a`, whose native artifact provenance pins
outrun2-decomp `a724dab` (`input-shim` core `7952ad2`). The header SHA-256 is
`57029AE63E182BF84DCB7E67477154BF69046F76941A7A80456D839F28811530`.

The optional offline ABI probe checks DRIVE's existing x64 DLL, SHA-256
`5ADC99691DD644C756C259D4D774B7F021C49424592F6B9F19E96770F83FA420`, by calling
only `dbce_shim_abi_version`, `dbce_shim_frame_size`, `dbce_shim_open_args_size`
and `dbce_shim_action_name`. It never calls open, tick, record or stop and creates
no input/force handle. This proves metadata interoperability, not host operation.

Art's `ShimFrame` is a strict managed decoder of the pinned C layout. It uses APIs
available on .NET Framework 4.8 / Unity 2019 Mono: no `Span`, `BinaryPrimitives`,
`NativeLibrary`, `float.IsFinite`, record types or new runtime dependencies. The
managed sink is built into the existing Art assembly. The parser remains in Core.

The initial `dbce_shim_bridge.h` came from shared shim source candidate
`d46096c40fa3806bdde6223212af07b541163b5f`, additive extension 1. SHA-256:
`5CA3154A803E8FD3D38CABE8FD349F22371B213C04E9D15195BE093E8F3889F3`.
It pairs with optional toolkit snapshot exports from
`ecc49bd9d9f40128792bf3122e102f2d9e3fab1a`. Neither replaces a vendored DLL.
The explicit offline `--bridge` test loads the candidate DLL and opens only its
copied-memory provider with a fake source: it has no DirectInput/device/force
operation. This actual DLL path passes on .NET8 and Unity 2019 Mono.

ABI 2 is not a stable version promise. Runtime adoption still requires reviewed
native pins, exact-identity Rewired isolation and explicit profile activation.
See `docs/reviews/2026-10-11-shared-input-axis-slice.md`.

## Owned interface path follow-on

Both current headers are copied unchanged from isolated shim `574bb88`, which
includes the shared metadata change `f49cfc5` as `1e88feb`. Current SHA-256:

- `dbce_shim.h`: `3C63B472FC78770E8B9FC90AE92DEBBF979EEDD7F8C68800F56B8690F4F65B74`
- `dbce_shim_bridge.h`: `1F9FBFE5BD0D01796DB78282F6AD4FBC38BFBE272E3C64BCE8C2C7B541419021`

The optional `dbce_shim_bridge_devices_with_paths` export atomically copies
owned paths with unchanged metadata structs. Toolkit `64ccf67` supplies the
generation-checked `GetReadDevicePathV1` on the already-owned handle. No DLL is
vendored or installed. The old metadata export remains tested, while Art's
explicit diagnostic uses the new path export and refuses if it is unavailable.
The shared Core is still the sole profile parser.

## Shared-input candidate binaries (2026-10-12, romi 32354 and 32775)

Only the private owner-test package (`tools/package/package.ps1 -SharedInputCandidate`)
uses these. `CANDIDATE.json` pins both. The default package still ships `lib/toolkit`'s
native and no shim.

- `native/WheelFfb.dll` `44E46799945438BC07DF738472602DB1E3ECC04C3F77CAD5B12FCACF49C69272`
  is vendored here.
  - Source: toolkit `mufa/native-091-read-snapshots` `bbbf6e3`. That is Art's accepted
    native line (`native-0.9.1-fleet-candidate` `c8ec2ee`, shipped as `F8361A08`) plus
    only the read-snapshot and interface-path exports. Version 901, 51 exports.
  - Independent review passed (romi 32797).
  - Public-repository basis: the same component, from the same MIT toolkit, as the native
    this repository already vendors in `lib/toolkit/native` and ships in its public
    releases.
- `dbce_shim.dll` `7DDC962132819EDAE76EF84CE6509F1936B8C38177A36D498B8ABBCFDCFFBCAF` is
  NOT kept in this public repository.
  - It is the earlier copied-provider build of outrun2-decomp `574bb88`, built
    with ClangCL (Clang 19.1.5, P21 configure log).
  - Public-repository check, romi 32775. No existing public record covers this binary:
    - DRIVE's vendored shim (`vendor/dbce-shim`, "vendored, provisional") is in a
      private repository;
    - `574bb88` is not on outrun2-decomp's public remote.
  - Commit `4b757be` added the binary to this branch's history, which is not rewritten.
    The tip removes it.
  - The private candidate build passes a private copy with `-SharedInputShim`, checked
    against the pin. Its bytes are the copy every ordinary shared run used:
    `games/.worktrees/art-shim-probe-evidence/apply-20261012-021108/dbce_shim.dll`.
  - Publishing the shim binary (and its source commit) is a separate owner decision.

## Pure profile inspection candidate (2026-10-13)

The private candidate now pins `dbce_shim.dll` from isolated native source
`5b96ba8f3d649f353e2aad1aac8342f4334e0ad0`, built with x64 ClangCL 19.1.5:
666,112 bytes, SHA-256
`723E8A38CDA8DDA12546264580A5B3FF5AFFDF469E25E8777149CB792A789276`.
It remains a private packaging input, never a committed public binary.

The additive optional `dbce_shim_profile_action_info` export uses the same parser
and pure admission planner as Core to inspect exact in-memory profile bytes.
Art uses it before force recovery to resolve a fresh shared-only user's configured
steering identity, without opening devices or enabling force. This is an extension
compatibility requirement for shared-follow mode, not an ABI 2 version upgrade.
The retained historical headers describe the base ABI and copied-provider seam;
the optional declaration is in the pinned native source's `windows/shim_dll/dbce_shim.h`.
All existing exports and 296/128/144/232-byte layouts remain unchanged.

Native Core 169, ABI 164, copied-provider 77 and fake-COM 106 checks passed. Art
SettingsUi 146, shared FFB startup 16, ordinary startup 14 and lifecycle 33 checks
passed; ShimInput passed 12,327 checks each on .NET 8 and installed Unity Mono,
including the actual new export. See the
[review](../../docs/reviews/2026-10-13-shared-steering-ffb-target.md) for logs and limits.
The existing `native/WheelFfb.dll` and all force settings/tuning remain unchanged.
Fresh shared-only runtime qualification is still pending; these offline checks
do not establish physical FFB or general shared-input parity.

## Canonical toolkit shim (2026-10-13)

The private candidate now pins the canonical toolkit shim instead of an isolated
outrun2-decomp build. This supersedes the header records in the sections above.

- **Source:** dbce-wheel-mod-toolkit `c6594fa` (`native/shim`), built at
  `421bd4b234c5e0869f082f3dc26ed08845cc7bff`. `c6594fa` adds docs and comments only.
  It merges the copied-provider bridge (`574bb88`), the fenced latched session
  (`f38b855`) and the pure profile inspection (`5b96ba8`).
- **Bytes:** `dbce_shim.dll`, 925,184 bytes, SHA-256
  `BC9B030A25834D8CCE562F22588EC5E0D3F5C1FA026AD7AD984776D60CD6254B`.
  - x64 ClangCL 19.1.5 `/MT`, built from clean source by the toolkit's
    `native/shim/build_test.ps1`.
  - Imports only `DINPUT8`, `bcrypt` and `KERNEL32`, so there is no new runtime dependency.
  - Like every earlier shim, it is a private packaging input and never a committed binary.
    The reviewed copy is
    `research/.worktrees/toolkit-shim-c6594fa-evidence/x64-mt-421bd4b/dbce_shim.dll`,
    with `build-receipt.json` and `SHA256SUMS` beside it.
- **Contract:**
  - Base ABI 2 and every earlier export are unchanged, including the
    296/128/144/232-byte layouts.
  - Bridge extension 1 has seven exports. Its open admits `DBCE_SHIM_LATCHED` only
    through the base ABI's fence and mute checks, and refuses `CAPTURE`.
  - `dbce_shim_profile_action_info` stays optional and pure.
- **Headers:** both are exact copies of toolkit `c6594fa` `native/shim/shim_dll/`, kept
  LF by `.gitattributes` so their git blobs equal the toolkit's.
  - `dbce_shim.h`: blob `12f74f5b`, SHA-256
    `5E92E45D5579D72905CEF067F41161B8A821C5E925139A91E5A5FD00FFC28419`. It now
    declares `dbce_shim_profile_action_info` itself.
  - `dbce_shim_bridge.h`: blob `22729ffb`, SHA-256
    `B76750776E8A37E904882301C82EAB8003B38EDADFE9F6E25C417F6B1A26C14B`. It documents
    `LATCHED` and `CAPTURE`.
  - Art calls the shim through P/Invoke, so the headers are references only. No
    managed layout changed.

Compatibility evidence by nene, with the same bytes, is in
`dbce-project-mgmt/artifacts/shim-bridge-merge-20261013/final-421bd4b`:

- Toolkit native: Core 192, provider 106, host 37, ABI 186 and bridge 77 checks; the
  control journal 366 checks and 18 reader cases.
- Art ShimInput:
  - `--bridge-latched`: 12,490 checks on .NET 8 and 12,510 on Unity Mono;
  - `--profile-info`: 12,327 each.

Art's adoption commit changes:

- the runtime pin (`SharedInputStartup.ExpectedShimSha256`) and `CANDIDATE.json`, which
  `package.ps1` cross-checks;
- the `--bridge` latched gate, which now reads the reviewed pin from `CANDIDATE.json`.
  It still expects `574bb88`'s refusal and keeps `f38b855` as reviewed history.
  Before this change it called the 723E8A38 pin, then BC9B030A, "unreviewed";
- the default pin in `Prepare-SharedInputCopy.ps1`, which now also reads `CANDIDATE.json`.

The probe flavour keeps its historical latched pin (`ProbeRequest.LatchedShimSha256`,
`Prepare-ShimProbe.ps1`).

Art checks against these bytes, all offline with device and force endpoints as fixtures,
are in `dbce-project-mgmt/artifacts/art-shim-canonical-20261013`:

- ShimInput `--bridge`: 12,409 checks on .NET 8 and 12,407 on Unity Mono, ending in
  a fenced latched open at the gate.
- WheelInput actual-DLL cases, with `ART_SHIM_DLL` and `ART_LATCHED_SHIM_DLL` set to
  these bytes:
  - shared startup 14, shared FFB selection 21, lifecycle 33, transport stop 8;
  - latched menu 3, drive trial 611;
  - the default suite 430.
- Builds: the mod Release (net48) and ShimInput had no warnings. WheelInput had only
  its four known CS0649 fixture warnings.
- Assertion counts include per-frame checks, so they vary between runs.

**Limits:** nothing was packaged, installed or run in the game. The installed rc.20
still carries `723E8A38` (`docs/LOCAL-DEPLOYMENT.md`).

These offline checks do not establish:

- fresh shared-only runtime qualification;
- physical FFB;
- input/FFB coexistence;
- general shared-input parity.

Publishing the shim binary remains a separate owner decision.