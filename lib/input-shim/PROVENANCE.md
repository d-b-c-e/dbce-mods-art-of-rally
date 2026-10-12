# Shared input wire contract

This is a source-only Art consumer of the existing shared input shim's experimental
C ABI 2. It does not fork the profile parser, binding resolver, calibration,
device selection or normalization. No native shim DLL is packaged; no production
bootstrap loads or attaches the source-only bridge.

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
