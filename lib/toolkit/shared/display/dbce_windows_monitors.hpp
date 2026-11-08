// MIT. Source-vendored Windows monitor reader for dbce_racing_row.hpp, the C++ counterpart of WindowsMonitors.cs: each
// display's desktop rectangle and the primary flag, read with EnumDisplayMonitors + GetMonitorInfoW. Read-only: it
// changes no display setting, profile or window. Coordinates are those of the calling process's DPI context. Kept
// apart from the pure policy so fixtures and non-Windows hosts never need it. Include <windows.h> first, as the mod
// already does, with whatever WIN32_LEAN_AND_MEAN/NOMINMAX settings it uses.
#pragma once
#include <windows.h>
#include <vector>
#include "dbce_racing_row.hpp"

#ifdef _MSC_VER
#pragma comment(lib, "gdi32.lib")
#endif

namespace dbce::display {

// The displays Windows reports now, in its enumeration order. False, with `out` empty, if Windows refuses the query.
// Call it from the mod's own code: a mod that hooks the game's GetMonitorInfoW import (to report the span) does not
// change what this reads.
inline bool read_windows_monitors(std::vector<MonitorRect>& out)
{
    struct State
    {
        std::vector<MonitorRect>* list;
        bool failed;
    } state{&out, false};
    out.clear();
    const BOOL enumerated = EnumDisplayMonitors(
        nullptr, nullptr,
        [](HMONITOR monitor, HDC, LPRECT, LPARAM data) -> BOOL {
            auto& s = *reinterpret_cast<State*>(data);
            MONITORINFO info{};
            info.cbSize = sizeof info;
            if (!GetMonitorInfoW(monitor, &info))
            {
                s.failed = true;
                return FALSE;
            }
            const RECT& r = info.rcMonitor;
            s.list->push_back({r.left, r.top, r.right - r.left, r.bottom - r.top, (info.dwFlags & MONITORINFOF_PRIMARY) != 0});
            return TRUE;
        },
        reinterpret_cast<LPARAM>(&state));
    if (!enumerated || state.failed)
    {
        out.clear();
        return false;
    }
    return true;
}

// The racing row for the displays Windows reports now (see select_racing_row), with the caller's wide-primary aspect.
inline RacingRow select_windows_racing_row(double surround_aspect)
{
    std::vector<MonitorRect> monitors;
    if (!read_windows_monitors(monitors)) return detail::none("Windows did not report the displays (EnumDisplayMonitors or GetMonitorInfo failed)");
    return select_racing_row(monitors, surround_aspect);
}

// The primary display's picture size in millimetres as Windows reports it (the monitor's EDID, through GDI's
// HORZSIZE/VERTSIZE on that display), for default triple measurements. False when Windows has no size (some
// projectors, TVs and virtual displays report 0) or the size does not match the display's shape within 15%. Uses GDI
// (CreateDC): call it from the mod's own thread, not inside DllMain. Links gdi32.
inline bool read_primary_size_mm(double& width_mm, double& height_mm)
{
    width_mm = height_mm = 0;
    MONITORINFOEXW info{};
    info.cbSize = sizeof info;
    if (!GetMonitorInfoW(MonitorFromPoint(POINT{0, 0}, MONITOR_DEFAULTTOPRIMARY), &info)) return false;
    HDC dc = CreateDCW(info.szDevice, info.szDevice, nullptr, nullptr);
    if (!dc) return false;
    const int w = GetDeviceCaps(dc, HORZSIZE), h = GetDeviceCaps(dc, VERTSIZE);
    DeleteDC(dc);
    const double pw = double(info.rcMonitor.right - info.rcMonitor.left), ph = double(info.rcMonitor.bottom - info.rcMonitor.top);
    if (w < 100 || h < 50 || w > 3000 || h > 2000 || pw <= 0 || ph <= 0) return false;
    const double shape = (double(w) / h) / (pw / ph);
    if (shape < 0.85 || shape > 1.15) return false;
    width_mm = w;
    height_mm = h;
    return true;
}

} // namespace dbce::display
