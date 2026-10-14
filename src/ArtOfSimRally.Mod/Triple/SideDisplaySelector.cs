using System;
using System.Collections.Generic;
using System.Linq;
using Dbce.Wheel.Display;

namespace ArtOfSimRally.Mod.Triple;

// Chooses the Unity display indices for the side views from the racing row Windows shows (sana 80507/81386), so a
// dashboard or other extra display is never chosen and the saved LeftDisplayIndex/RightDisplayIndex are no longer used.
// Unity 2019.4 sorts its display list by virtual-desktop x, y unless the player runs with -multidisplay (Manual:
// Multi-display), with the primary at index 0. That sort is the only link before activation, and its tie order is not
// documented, so this choice is a prediction: SideActivationWitness proves each side on activation. Pure.
internal static class SideDisplaySelector
{
    internal const string UnsortedFlag = "-multidisplay";

    internal sealed class Result
    {
        public bool Ok { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public int LeftIndex { get; set; } = -1;
        public int RightIndex { get; set; } = -1;
        public MonitorRect Left { get; set; }
        public MonitorRect Right { get; set; }
    }

    /// <param name="monitors">Windows' displays, in any order.</param>
    /// <param name="unitySizes">Unity's display list (index order): each display's system width and height.</param>
    /// <param name="commandLine">The player's command line.</param>
    internal static Result Select(IReadOnlyList<MonitorRect> monitors, IReadOnlyList<(int Width, int Height)> unitySizes,
        IEnumerable<string> commandLine, int panelWidth, int panelHeight)
    {
        if (commandLine.Any(arg => string.Equals(arg, UnsortedFlag, StringComparison.OrdinalIgnoreCase)))
            return Refuse("DISPLAY_ORDER_UNSORTED",
                "The game was started with -multidisplay, so Unity does not sort its displays by position and the side displays cannot be found. Start it without that option.");
        var row = RacingRowPolicy.Select(monitors);
        if (row.Kind != RacingRowKind.Separate)
            return Refuse("RACING_ROW_UNAVAILABLE",
                "Independent displays need the Windows primary in the center with an equal-size display directly on each side: " + row.Reason + ".");
        var left = row.Displays[0];
        var right = row.Displays[2];
        if (left.Width != panelWidth || left.Height != panelHeight)
            return Refuse("RACING_ROW_SIZE_MISMATCH",
                $"The racing row's displays are {left.Width}x{left.Height}; the layout's panel is {panelWidth}x{panelHeight}.");
        var predicted = DisplayIdentityEvidence.PredictXOrder(monitors);
        if (predicted.Count != unitySizes.Count)
            return Refuse("DISPLAY_IDENTITY_UNPROVEN",
                $"Unity reports {unitySizes.Count} displays and Windows {monitors.Count}, so Unity's display order cannot be matched to the row.");
        for (var index = 0; index < predicted.Count; index++)
            if (predicted[index].Width != unitySizes[index].Width || predicted[index].Height != unitySizes[index].Height)
                return Refuse("DISPLAY_IDENTITY_UNPROVEN",
                    $"Unity display {index} is {unitySizes[index].Width}x{unitySizes[index].Height} but the position order predicts {predicted[index]}, so the order cannot be trusted.");
        var leftIndex = IndexOf(predicted, left);
        var rightIndex = IndexOf(predicted, right);
        return new Result
        {
            Ok = true, Code = "SIDE_DISPLAYS_PREDICTED", LeftIndex = leftIndex, RightIndex = rightIndex, Left = left, Right = right,
            Message = $"Predicted left = Unity {leftIndex} ({left}), right = Unity {rightIndex} ({right}); each is verified on activation.",
        };
    }

    private static int IndexOf(IReadOnlyList<MonitorRect> list, MonitorRect monitor)
    {
        for (var index = 0; index < list.Count; index++)
            if (list[index].Equals(monitor)) return index;
        return -1;
    }

    private static Result Refuse(string code, string message) => new() { Ok = false, Code = code, Message = message };
}
