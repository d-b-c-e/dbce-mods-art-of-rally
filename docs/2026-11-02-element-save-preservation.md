# Art XML single-element preservation, November 2

Kole assignment 497574; compiler GO 497785. Worktree
`E:/Source/_work/napo-art-element-bytes-20261102`, based on main fc23361.
PUBLIC repo: local main only, no push. Installed 0.4.4-rc.36 and accepted
camera/vehicle/force settings are untouched.

## Change

SettingsPersistence.WriteElement previously loaded XmlDocument and saved the
whole document. PreserveWhitespace did not preserve original BOM, mixed EOL,
quote/entity spellings or every unowned byte. Its intended one-field write could
therefore alter unrelated settings markup.

The writer now decodes strict UTF-8 while retaining BOM state, validates the
complete byte document with XmlReader (DTD/external resolution prohibited), maps
the unique top-level scalar through reader line locations, and replaces only its
original content span. Opening/closing tags and unowned markup are retained.
Self-closing scalars and missing fields/root content get only the necessary owned
markup insertion. Escaped values preserve CR through character references.

Duplicate owned elements, structured/comment/CDATA content inside the owned
element, foreign roots/namespaces, malformed XML and unsupported encodings refuse
before disk writes. Unowned comments, entities, CDATA and attributes are untouched.
UTF-16/legacy encodings are not silently transcoded; they refuse. No promise of
admission against concurrent external editors is added.

Atomic replacement retains unique CreateNew staging and now records ownership
before cleanup, flushes durable bytes and preserves primary errors. Full object
Write still uses the same XML serializer/UTF-8 format; it gains the same flush but
does not become a byte-preserving whole-document merge. STD-044 remains partial.

## Offline evidence

- Original source fails the new exact-byte negative witness.
- Final SettingsUi runner: **3,549 assertions pass**, zero build warnings.
- Existing Regression runner: **478,849 assertions pass**, including full serializer
  round trips, locked-file failure/retry and force arithmetic without output.
  Its 16 nullable warnings are in unchanged source; changed persistence code has
  explicit nullable checks. Initial intermediate warning logs are retained.
- Production net48 Mod Release: zero warnings/errors. DLL SHA-256
  `71F99465E0EF8C4D45F237092F8E826D3C6A2620C5ED96FCF50FDB9D7F12E8A5`.

The new cases check exact UTF-8/BOM, mixed CRLF/LF, missing final newline, single
quotes, entities/comments/attributes, quoted `>` characters, duplicates/structured
targets/DTD refusal, self-closing roots/elements, escaped values and missing-field
insertion. Existing malformed/read-only replacement cases retain their pass.

Retained ignored evidence: this worktree's `results/napo-element-bytes-20261102/`,
with original-source build/failure, final fixtures/build logs, result.json and
named lease logs. No game/mod DLL load, UI rendering, device/force, owner files,
display, Pit House/SimHub, packaging or install. TEMP/TMP restored and lease released.

Next: owner authorization for public source push; later reviewed package and live
F7/save/restart qualification under kole GO, muted output and exact state restore.
These offline results do not establish Unity Mono or rendered/owner acceptance.
