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
