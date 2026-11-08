// MIT. Source-vendored racing-row selector for native mods: which displays a triple-screen game should span, from the
// observed monitor rectangles and the primary display alone. The C++ counterpart of RacingRow.cs: the same rules, the
// same reasons, kept equal by racing-row-vectors.txt, which both run. Pure geometry: no Windows, engine, device or
// display-setting calls, and it never changes the topology. Enumeration lives in dbce_windows_monitors.hpp; consumers
// pin this file's toolkit commit. Header-only C++17; throws nothing of its own (std::string may throw std::bad_alloc).
#pragma once
#include <cmath>
#include <string>
#include <utility>
#include <vector>

namespace dbce::display {

// One display's desktop rectangle (virtual-desktop pixels) and whether Windows calls it primary.
struct MonitorRect
{
    int x = 0, y = 0, width = 0, height = 0;
    bool primary = false;
    int right() const { return x + width; }
    int bottom() const { return y + height; }
    bool operator==(const MonitorRect& o) const
    {
        return x == o.x && y == o.y && width == o.width && height == o.height && primary == o.primary;
    }
    bool operator!=(const MonitorRect& o) const { return !(*this == o); }
    std::string to_string() const
    {
        return std::to_string(width) + "x" + std::to_string(height) + "@" + std::to_string(x) + "," + std::to_string(y) +
               (primary ? " primary" : "");
    }
};

enum class RacingRowKind
{
    // No racing row: the game keeps its own single-screen behavior; nothing is spanned.
    None,
    // The primary display is at least the caller's wide-primary aspect wide, so it is used as one wide surface: the
    // mods' existing wide-primary path. A shape test only, not proof of NVIDIA Surround or AMD Eyefinity grouping.
    Surround,
    // The primary and its equal-size left and right neighbors: three separate displays to span.
    Separate,
};

inline const char* to_string(RacingRowKind kind)
{
    return kind == RacingRowKind::Surround ? "Surround" : kind == RacingRowKind::Separate ? "Separate" : "None";
}

// The selected row: its rectangle, the displays in it (left to right) and why, for logs and fixtures.
struct RacingRow
{
    RacingRowKind kind = RacingRowKind::None;
    int x = 0, y = 0, width = 0, height = 0;
    std::vector<MonitorRect> displays;
    // One plain sentence: what was selected, or why there is no row.
    std::string reason;
    std::string to_string() const
    {
        if (kind == RacingRowKind::None) return "no racing row: " + reason;
        return std::string(dbce::display::to_string(kind)) + " " + std::to_string(width) + "x" + std::to_string(height) + "@" +
               std::to_string(x) + "," + std::to_string(y);
    }
};

// The toolkit's wide-primary rule (RacingRowPolicy.SurroundAspect in RacingRow.cs): 7680x1440 is 5.3:1, a 32:9
// monitor 3.6:1. A mod passes its own existing rule explicitly, so adopting the row never changes its wide path.
constexpr double kSurroundAspect = 2.9;

namespace detail {
inline RacingRow none(std::string reason)
{
    RacingRow row;
    row.reason = std::move(reason);
    return row;
}
inline bool same(const MonitorRect& a, const MonitorRect& b) { return a.width == b.width && a.height == b.height && a.y == b.y; }
inline std::string join(const std::vector<MonitorRect>& monitors)
{
    std::string text;
    for (const auto& m : monitors) text += (text.empty() ? "" : ", ") + m.to_string();
    return text;
}
// "2.9", "4": at most two decimals, without trailing zeros, and independent of the process locale.
inline std::string aspect_text(double aspect)
{
    const long long hundredths = std::llround(aspect * 100);
    std::string text = std::to_string(hundredths / 100);
    if (const long long fraction = hundredths % 100)
        text += fraction % 10 ? "." + std::to_string(fraction / 10) + std::to_string(fraction % 10) : "." + std::to_string(fraction / 10);
    return text;
}
} // namespace detail

// The automatic racing-row policy (owner, 2026-10-13: three 2560x1440 displays at -2560/0/2560 plus a 1920x1080
// diagnostic display above). The primary display is the center of the row:
//  - A primary at least surround_aspect:1 wide takes the existing wide-primary path (Surround in the mods' wording; a
//    shape test, not proof of driver grouping): the row is the primary alone, and other displays are ignored, never
//    appended to it.
//  - Otherwise the row is the primary plus exactly one display touching each side with the same size, top and
//    bottom. Displays wholly above or below the row are ignored.
//  - Anything else refuses auto-span (None) and never guesses: a missing side, a gap, a vertical offset or a different
//    size on a side, two candidates for a side, a display overlapping the row, a fourth display in the row's band, or
//    not exactly one primary.
// It never picks another triple or spans more than three displays. surround_aspect is the caller's existing rule
// (kSurroundAspect for the toolkit's); one that is not a positive number refuses.
inline RacingRow select_racing_row(const std::vector<MonitorRect>& all, double surround_aspect)
{
    using detail::none;
    if (!(surround_aspect > 0) || !std::isfinite(surround_aspect)) return none("the wide-primary aspect is not a positive number");
    std::vector<MonitorRect> primaries;
    for (const auto& m : all)
        if (m.primary) primaries.push_back(m);
    for (const auto& m : all)
        if (m.width <= 0 || m.height <= 0) return none("a display reports an empty rectangle");
    if (primaries.size() != 1) return none(primaries.empty() ? "no primary display" : "more than one primary display");
    const MonitorRect p = primaries[0];
    if (static_cast<double>(p.width) >= static_cast<double>(p.height) * surround_aspect)
    {
        RacingRow row{RacingRowKind::Surround, p.x, p.y, p.width, p.height, {p},
                      "the primary display is at least " + detail::aspect_text(surround_aspect) + ":1 wide, used as one surface (" +
                          p.to_string() + ")"};
        return row;
    }

    // Displays that share any of the primary's rows of pixels: only these can be left or right of it. A display that
    // merely touches the row's top or bottom edge (the diagnostic display above) is outside the band.
    std::vector<MonitorRect> band, left, right;
    for (const auto& m : all)
        if (!m.primary && m.y < p.bottom() && m.bottom() > p.y) band.push_back(m);
    if (band.empty()) return none("a single display row: no display beside the primary");
    for (const auto& m : band)
    {
        if (m.right() == p.x && detail::same(m, p)) left.push_back(m);
        if (m.x == p.right() && detail::same(m, p)) right.push_back(m);
    }
    if (left.size() > 1 || right.size() > 1) return none("two displays claim the same side of the primary");
    if (left.empty() || right.empty())
    {
        const char* side = left.empty() && right.empty() ? "either side" : left.empty() ? "the left" : "the right";
        return none(std::string("no equal-size display directly on ") + side +
                    " of the primary (missing, gap, overlap, offset or different size)");
    }
    std::vector<MonitorRect> extra;
    for (const auto& m : band)
        if (m != left[0] && m != right[0]) extra.push_back(m);
    if (!extra.empty())
        return none("another display shares the racing row's band (" + detail::join(extra) +
                    "); the row is ambiguous, so nothing is spanned");
    std::vector<MonitorRect> displays{left[0], p, right[0]};
    std::string reason = "three equal displays with the primary in the center (" + detail::join(displays) + ")";
    RacingRow row{RacingRowKind::Separate, left[0].x, p.y, right[0].right() - left[0].x, p.height, std::move(displays), std::move(reason)};
    return row;
}

} // namespace dbce::display
