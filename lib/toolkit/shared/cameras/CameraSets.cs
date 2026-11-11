// Managed twin of native/cameras/camera_sets.hpp (STD-041/042/046 camera sets). Same behavior, same names, no Unity or
// game types. Compiles for net35 (Mono 2.0) and net6.0; dotnet/Dbce.Wheel.Cameras is the compile check and
// dotnet/Dbce.Wheel.Cameras.Tests runs the same cases as native/cameras/test.cpp.
//
// - INI keys in the game's [Triple] section: <View>Realistic (true = Realistic) and <View>GameOffsetDegrees /
//   <View>RealisticOffsetDegrees. Offsets are added to the lens the game writes that frame, so they never build up.
// - Starting choices (STD-046): cockpit, dash and bumper Realistic; bonnet and chase Game. An explicit older
//   MatchGameFov (true = Game) sets every mounted view.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dbce.Wheel.Cameras
{
    public enum View { None = -1, Chase = 0, Bonnet, Bumper, Dash, Cockpit }

    // Numpad + zooms in (narrower), - zooms out (STD-048); 5 resets the current set to the calculation.
    public enum Zoom { In, Out, Reset }

    // The one key and value a change writes (STD-044: only its own key). Both are null when nothing changed.
    public struct Write
    {
        public string Key, Value;
        public bool IsEmpty { get { return string.IsNullOrEmpty(Key); } }
    }

    // Looks up an ini key in [Triple]. Returns true when the key exists.
    public delegate bool IniGet(string key, out string value);

    // A view counts once it has held for `frames` frames (camera cuts and the mirror's own lag do not flicker).
    public sealed class Watch
    {
        public View Shown = View.None, Pending = View.None;
        public int Held;
        public bool Step(View now) { return Step(now, 5); }
        public bool Step(View now, int frames)   // true when Shown changed this frame
        {
            if (now == Shown) { Pending = now; Held = 0; return false; }
            if (now != Pending) { Pending = now; Held = 1; } else ++Held;
            if (Held < frames) return false;
            Shown = now; Held = 0;
            return true;
        }
    }

    public sealed class Settings
    {
        public bool[] Realistic = { false, false, true, true, true };
        // [view][0 Game, 1 Realistic], degrees added to the vertical FOV
        public double[][] Offset = { new double[2], new double[2], new double[2], new double[2], new double[2] };
    }

    public static class CameraSets
    {
        public const int Count = 5;
        public static readonly string[] ViewKeys = { "ChaseRealistic", "BonnetRealistic", "BumperRealistic", "DashRealistic", "CockpitRealistic" };
        public static readonly string[] ViewNames = { "Chase Camera", "Bonnet Camera", "Bumper Camera", "Dash Camera", "Cockpit Camera" };
        public static readonly string[] ViewStems = { "Chase", "Bonnet", "Bumper", "Dash", "Cockpit" };
        public static readonly bool[] StartRealistic = { false, false, true, true, true };   // STD-046
        public const double MinFov = 1, MaxFov = 179, FovStep = 2;   // Only the final lens has a range; saved offsets stay finite/unbounded.

        const double RadToDeg = 57.29577951308232;
        const int ValueBuffer = 64;   // The C++ reads each value into a 64-byte buffer, so at most 63 characters.

        static bool Finite(double d) { return !double.IsNaN(d) && !double.IsInfinity(d); }

        public static int ParseBool(string b)   // 1 true, 0 false, -1 unrecognised
        {
            if (Eq(b, "1") || Eq(b, "true") || Eq(b, "on") || Eq(b, "yes")) return 1;
            if (Eq(b, "0") || Eq(b, "false") || Eq(b, "off") || Eq(b, "no")) return 0;
            return -1;
        }
        static bool Eq(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        public static string OffsetKey(View v, bool realistic)
        {
            return ViewStems[(int)v] + (realistic ? "Realistic" : "Game") + "OffsetDegrees";
        }

        public static Settings ReadSettings(IniGet get)
        {
            Settings s = new Settings();
            string b;
            int matchGame = -1;
            if (get("MatchGameFov", out b)) matchGame = ParseBool(Clip(b));
            for (int v = 0; v < Count; ++v)
            {
                s.Realistic[v] = v == (int)View.Chase ? false : matchGame >= 0 ? matchGame == 0 : StartRealistic[v];
                if (get(ViewKeys[v], out b)) { int x = ParseBool(Clip(b)); if (x >= 0) s.Realistic[v] = x == 1; }
                for (int r = 0; r < 2; ++r)
                {
                    if (!get(OffsetKey((View)v, r == 1), out b)) continue;
                    b = Clip(b);
                    double d;
                    int end;
                    bool underflow;
                    bool parsed = StrToD(b, out d, out end, out underflow);
                    while (end < b.Length && IsSpace(b[end])) ++end;
                    char c = end < b.Length ? b[end] : '\0';
                    bool comment = c == ';' || c == '#';
                    // Retain inline comments, refuse malformed/truncated numeric input.
                    if (parsed && Finite(d) && !underflow && (end >= b.Length || comment) &&
                        (b.Length < ValueBuffer - 1 || comment)) s.Offset[v][r] = d;
                }
            }
            return s;
        }

        static string Clip(string b)   // What fits the C++ buffer.
        {
            if (b == null) return "";
            return b.Length > ValueBuffer - 1 ? b.Substring(0, ValueBuffer - 1) : b;
        }

        public static bool ValidLens(float a, float b)
        {
            return Finite(a) && Finite(b) && a > 0 && b > 0;
        }

        // The game's native/seat lens plus the raw saved offset. Apply one final fence and derive both tangents from
        // that same angle; ordinary zero offsets stay bit-exact. Returns the two tangents.
        public static float[] Target(Settings s, View v, float[] game, float[] measured)
        {
            float[] o = { game[0], game[1] };
            if (v <= View.None || (int)v >= Count) return o;
            int vi = (int)v;
            bool r = s.Realistic[vi];
            float[] bs = r ? measured : game;
            o[0] = bs[0]; o[1] = bs[1];
            double off = s.Offset[vi][r ? 1 : 0];
            if (!ValidLens(bs[0], bs[1]) || !Finite(off)) return o;
            double degrees = 2 * Math.Atan((double)bs[1]) * RadToDeg;
            if (off == 0 && degrees >= MinFov && degrees <= MaxFov) return o;
            double requested = degrees + off;
            double effective = requested < MinFov ? MinFov : requested > MaxFov ? MaxFov : requested;
            double t = Math.Tan(effective / RadToDeg / 2);
            double h = ((double)bs[0] / bs[1]) * t;
            if (Finite(h) && h > 0 && h <= float.MaxValue) { o[0] = (float)h; o[1] = (float)t; }
            return o;
        }

        // The C++ leaves an out-of-range view undefined; this returns an empty Write.
        public static Write Toggle(Settings s, View v)
        {
            Write w = new Write();
            if (v <= View.None || (int)v >= Count) return w;
            int vi = (int)v;
            s.Realistic[vi] = !s.Realistic[vi];
            w.Key = ViewKeys[vi];
            w.Value = s.Realistic[vi] ? "true" : "false";
            return w;
        }

        public static Write ZoomView(Settings s, View v, Zoom z)
        {
            Write w = new Write();
            if (v <= View.None || (int)v >= Count) return w;
            int vi = (int)v;
            int r = s.Realistic[vi] ? 1 : 0;
            double old = s.Offset[vi][r];
            double next = z == Zoom.Reset ? 0 : old + (z == Zoom.In ? -FovStep : FovStep);
            if (z != Zoom.Reset && next == old)
                next = NextAfter(old, z == Zoom.In);
            // At the finite representation edge, refuse rather than save infinity.
            if (!Finite(next)) return w;
            string value = FormatG17(next);
            if (value.Length > 31) return w;   // The C++ value buffer is 32 bytes and refuses a truncated number.
            w.Key = OffsetKey(v, r == 1);
            w.Value = value;
            s.Offset[vi][r] = next;
            return w;
        }

        // "Cockpit Camera - Realistic FOV (Custom)" (STD-042/046); empty for None.
        public static string Name(Settings s, View v)
        {
            if (v <= View.None || (int)v >= Count) return "";
            int vi = (int)v;
            bool r = s.Realistic[vi];
            return ViewNames[vi] + " - " + (r ? "Realistic" : "Game") + " FOV" + (s.Offset[vi][r ? 1 : 0] != 0 ? " (Custom)" : "");
        }

        // The on-screen label with the key reminder. Unlike the C++ there is no buffer size, so nothing is cut off.
        public static string Label(Settings s, View v, string game) { return Label(s, v, game, null); }
        public static string Label(Settings s, View v, string game, string extra)
        {
            string nm = Name(s, v);
            if (nm.Length == 0) return "This camera keeps " + game + "'s own FOV (not identified yet)";
            const string dot = "  ·  ";   // The C++ writes this middle dot as UTF-8 (C2 B7).
            return nm + (extra != null ? dot + extra : "") + dot + "Numpad + / - zoom, 5 reset" + dot + "F7 " +
                (s.Realistic[(int)v] ? "Game" : "Realistic") + " FOV";
        }

        // ---- C library stand-ins (net35 has none of these) ----

        // C isspace in the "C" locale.
        static bool IsSpace(char c) { return c == ' ' || (c >= '\t' && c <= '\r'); }

        // strtod for the forms that can give a finite number: decimal, and hex floats (MSVC accepts them). inf/nan and
        // overflow come back non-finite so the caller refuses them, like the C++. `end` is the index after the number.
        // `underflow` is true when a nonzero number rounded to zero (C++ checks errno == ERANGE && d == 0).
        static bool StrToD(string b, out double d, out int end, out bool underflow)
        {
            underflow = false;
            d = 0; end = 0;
            int i = 0, n = b.Length;
            while (i < n && IsSpace(b[i])) ++i;
            bool neg = false;
            if (i < n && (b[i] == '+' || b[i] == '-')) { neg = b[i] == '-'; ++i; }
            if (i < n && (b[i] == 'i' || b[i] == 'I' || b[i] == 'n' || b[i] == 'N'))
            {
                if (StartsWithCI(b, i, "inf") || StartsWithCI(b, i, "nan")) { d = double.PositiveInfinity; end = i + 3; return true; }
                return false;
            }
            if (i + 1 < n && b[i] == '0' && (b[i + 1] == 'x' || b[i + 1] == 'X') && HexStart(b, i + 2))
                return HexFloat(b, i + 2, neg, out d, out end, out underflow);
            int start = i, digits = 0;
            bool nonzero = false;
            string mant = "";
            while (i < n && b[i] >= '0' && b[i] <= '9') { if (b[i] != '0') nonzero = true; ++i; ++digits; }
            int intEnd = i;
            int fracStart = i, fracEnd = i;
            if (i < n && b[i] == '.')
            {
                ++i;
                fracStart = i;
                while (i < n && b[i] >= '0' && b[i] <= '9') { if (b[i] != '0') nonzero = true; ++i; ++digits; }
                fracEnd = i;
            }
            if (digits == 0) return false;
            mant = (intEnd > start ? b.Substring(start, intEnd - start) : "0") + "." +
                (fracEnd > fracStart ? b.Substring(fracStart, fracEnd - fracStart) : "0");
            string exp = "";
            if (i < n && (b[i] == 'e' || b[i] == 'E'))
            {
                int j = i + 1;
                if (j < n && (b[j] == '+' || b[j] == '-')) ++j;
                if (j < n && b[j] >= '0' && b[j] <= '9')
                {
                    while (j < n && b[j] >= '0' && b[j] <= '9') ++j;
                    exp = "e" + b.Substring(i + 1, j - i - 1);
                    i = j;
                }
            }
            end = i;
            try { d = double.Parse(mant + exp, NumberStyles.Float, CultureInfo.InvariantCulture); }
            catch (OverflowException) { d = double.PositiveInfinity; }
            if (nonzero && d == 0) underflow = true;
            if (neg) d = -d;
            return true;
        }

        static bool StartsWithCI(string b, int i, string w)
        {
            return b.Length - i >= w.Length && string.Compare(b, i, w, 0, w.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }
        static int HexVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
        static bool HexStart(string b, int i)
        {
            if (i < b.Length && HexVal(b[i]) >= 0) return true;
            return i + 1 < b.Length && b[i] == '.' && HexVal(b[i + 1]) >= 0;
        }
        static bool HexFloat(string b, int i, bool neg, out double d, out int end, out bool underflow)
        {
            int n = b.Length;
            double mant = 0;
            int exp = 0;
            while (i < n && HexVal(b[i]) >= 0) { mant = mant * 16 + HexVal(b[i]); ++i; }
            if (i < n && b[i] == '.')
            {
                ++i;
                while (i < n && HexVal(b[i]) >= 0) { mant = mant * 16 + HexVal(b[i]); exp -= 4; ++i; }
            }
            if (i < n && (b[i] == 'p' || b[i] == 'P'))
            {
                int j = i + 1;
                bool eneg = false;
                if (j < n && (b[j] == '+' || b[j] == '-')) { eneg = b[j] == '-'; ++j; }
                if (j < n && b[j] >= '0' && b[j] <= '9')
                {
                    int e = 0;
                    while (j < n && b[j] >= '0' && b[j] <= '9') { if (e < 100000) e = e * 10 + (b[j] - '0'); ++j; }
                    exp += eneg ? -e : e;
                    i = j;
                }
            }
            end = i;
            d = mant;
            // Scale in steps so a large intermediate does not overflow early.
            while (exp > 0 && Finite(d)) { int st = exp > 512 ? 512 : exp; d *= Math.Pow(2, st); exp -= st; }
            while (exp < 0 && d != 0) { int st = -exp > 512 ? 512 : -exp; d /= Math.Pow(2, st); exp += st; }
            underflow = mant != 0 && d == 0;
            if (neg) d = -d;
            return true;
        }

        // nextafter(x, -infinity) when towardNegative, else nextafter(x, +infinity).
        static double NextAfter(double x, bool towardNegative)
        {
            if (double.IsNaN(x)) return x;
            if (x == 0) return towardNegative ? -double.Epsilon : double.Epsilon;
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool up = (x > 0) != towardNegative;   // true when the magnitude grows
            bits += up ? 1 : -1;
            return BitConverter.Int64BitsToDouble(bits);
        }

        // printf("%.17g") with the MSVC exponent form (at least two digits). Exact digits, round half to even, so it
        // does not depend on how old Mono formats doubles.
        public static string FormatG17(double d)
        {
            const int P = 17;
            long bits = BitConverter.DoubleToInt64Bits(d);
            bool neg = bits < 0;
            if (d == 0) return neg ? "-0" : "0";
            int be = (int)((bits >> 52) & 0x7FF);
            ulong frac = (ulong)bits & 0xFFFFFFFFFFFFFUL;
            ulong m;
            int e;
            if (be == 0) { m = frac; e = -1074; } else { m = frac | (1UL << 52); e = be - 1075; }
            // value = m * 2^e. Build the exact digits of m * 2^e (e >= 0) or m * 5^-e * 10^e (e < 0).
            List<int> dig = new List<int>();   // little-endian decimal digits
            for (ulong t = m; t != 0; t /= 10) dig.Add((int)(t % 10));
            int mul = e >= 0 ? 2 : 5, times = e >= 0 ? e : -e;
            for (int k = 0; k < times; ++k)
            {
                int carry = 0;
                for (int j = 0; j < dig.Count; ++j) { int x = dig[j] * mul + carry; dig[j] = x % 10; carry = x / 10; }
                while (carry != 0) { dig.Add(carry % 10); carry /= 10; }
            }
            StringBuilder sb = new StringBuilder();
            for (int j = dig.Count - 1; j >= 0; --j) sb.Append((char)('0' + dig[j]));
            string all = sb.ToString();
            int X = all.Length - 1 + (e < 0 ? e : 0);   // decimal exponent of the first digit
            string ds;
            if (all.Length > P)
            {
                char[] kept = all.Substring(0, P).ToCharArray();
                char first = all[P];
                bool rest = false;
                for (int j = P + 1; j < all.Length; ++j) if (all[j] != '0') { rest = true; break; }
                bool up = first > '5' || (first == '5' && (rest || ((kept[P - 1] - '0') & 1) == 1));
                if (up)
                {
                    int j = P - 1;
                    while (j >= 0 && kept[j] == '9') { kept[j] = '0'; --j; }
                    if (j >= 0) kept[j]++;
                    else { kept[0] = '1'; ++X; }   // all nines: 1000... with one more power of ten
                }
                ds = new string(kept);
            }
            else ds = all.PadRight(P, '0');
            string text;
            if (X < -4 || X >= P)
            {
                string tail = ds.Substring(1).TrimEnd('0');
                int ax = X < 0 ? -X : X;
                text = ds.Substring(0, 1) + (tail.Length > 0 ? "." + tail : "") + "e" + (X < 0 ? "-" : "+") +
                    (ax < 10 ? "0" : "") + ax.ToString(CultureInfo.InvariantCulture);
            }
            else if (X >= 0)
            {
                string fr = ds.Substring(X + 1).TrimEnd('0');
                text = ds.Substring(0, X + 1) + (fr.Length > 0 ? "." + fr : "");
            }
            else
            {
                text = "0." + new string('0', -X - 1) + ds.TrimEnd('0');
            }
            return neg ? "-" + text : text;
        }
    }
}
