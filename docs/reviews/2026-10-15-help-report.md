# Art Help adapter: source candidate, October 15

Request163647 adds the three deliberate GitHub actions to Help: Submit GitHub
Issue, Mod on GitHub, and Wheelkit on GitHub. The Art adapter uses Unity's
ordinary Application.OpenURL action and existing F6 controls. Product/version
metadata is packaged; there is no player-side git, account, token or HTTP flow.

Exact toolkit source ab638898f52ee482718e371594457ae493549551 is vendored at
src/ArtOfSimRally.Mod/Toolkit/Support/SupportReport.cs, SHA-256
CA005A794086D66E3F7660EED692FB5583DE87B112BCA748FF60385A5944033A. Its MIT
license and provenance are beside it. Source and license retain exact bytes.

The form offers a multiline description and a bounded scrollable preview.
All three known log choices start off: UMM Log.txt, Art ffb.log and Unity
Player.log. Review explicitly reads only selected logs through the shared
bounded reader. Existing broad support-file collection is a separate action.
Changing title, description or any log choice clears the prepared snapshot,
review checkbox, status and export reference. Preparation clears them before
reading, so a read/validation failure cannot reuse a previous report. Review
and opening the draft are disabled while driving; there is no game-state write.

Opening requires the checkbox for the current immutable snapshot. The form
explains that opening transmits prefilled text to GitHub, and the user checks
and submits there. A long report exports this same snapshot only on that
explicit action, into a unique Desktop folder as support-report.txt. CreateNew
prevents replacement; the shared linked-path check applies. Its location stays
visible even if URL opening fails. Manual attachment uploads immediately; no
attachment or issue is sent automatically. Errors display without logging raw
report contents. The selected logs are never reread during export/open.

## Verification boundary

Source review and scoped diffcheck only. No compile, fixture, browser action,
game, config or installation has run for this adapter. The single deferred
common fixture is toolkit tests/SupportReport at exactab638898 (request163928),
plus the final Art normal net48 compile in the combined batch. Other hosts
reuse that common result. Runtime/rendered Help acceptance remains separate.
Niru163764's action fallback visibility correction is folded into this candidate:
shared common rows are read-only source summaries, and active Art-only rows
show source/conflict text without adding an editor or input authority.


## Actual companion helper pin

Zele164114 closed ART-HELPER-SUCCESSOR-01 (hub84e14d7f): focused67/67,
one publish, ZIP integrity passed, exact-token release and independent closure.
The companion pin now names actual a8583c59feef helper ZIP F3CF1345402CD31562B8A6112F440419DC79C6D28DDD276B4DBEDFEA084FC331,
manifest E9D54FFF4597DE943A2D2449071DC5DE8600416B7C3DEB7D573A66801D94A702,
527 files,80614825 bytes. Lola independently read ZIP SHA, embedded manifest SHA
and manifest count before pinning. This is artifact identity reuse; Art's actual
package/companion/CLI proof remains deferred. No helper rebuild is requested.
