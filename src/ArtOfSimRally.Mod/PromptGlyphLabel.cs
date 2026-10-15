using System;
using System.Globalization;
using Dbce.Wheel.Input;
using Dbce.Wheel.Ui;

namespace ArtOfSimRally.Mod
{
    // Badge-sized physical element names. Shared bindings use DirectInput indices;
    // Rewired names already contain the controller's displayed button number.
    internal static class PromptGlyphLabel
    {
        private static readonly string[] HatDirections = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        internal static string Binding(ControlBinding binding)
        {
            if (binding == null) return null;
            switch (binding.Kind)
            {
                case ControlKind.Axis: return Element(ControlNames.Axis(binding.Index));
                case ControlKind.Button: return Element(ControlNames.Button(binding.Index));
                case ControlKind.Hat: return Hat(binding.Index, binding.Angle);
                default: return null;
            }
        }

        internal static string Hat(int index, int angle)
        {
            // Never round intermediate angles into another bound direction.
            return "H" + (index + 1).ToString(CultureInfo.InvariantCulture) +
                (angle % 4500 == 0 ? HatDirections[angle / 4500] : "/" +
                    (angle / 100.0).ToString("0.##", CultureInfo.InvariantCulture));
        }

        internal static string Element(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            const string button = "Button ";
            if (name.StartsWith(button, StringComparison.Ordinal)) return "B" + name.Substring(button.Length).Trim();
            switch (name)
            {
                case "X Rotation": case "Rotation X": return "Rx";
                case "Y Rotation": case "Rotation Y": return "Ry";
                case "Z Rotation": case "Rotation Z": return "Rz";
                case "Slider 1": return "S1";
                case "Slider 2": return "S2";
            }
            int space = name.IndexOf(' ');
            string head = space > 0 ? name.Substring(0, space) : name;
            return head.Length <= 4 ? head : head.Substring(0, 4);
        }
    }
}
