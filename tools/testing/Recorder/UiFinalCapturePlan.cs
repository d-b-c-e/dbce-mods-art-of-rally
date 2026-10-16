using System.Collections.Generic;

namespace ArtOfSimRally.Testing
{
    // Render navigation only. Binding edits remain in UiCapture's existing, validated save route.
    internal sealed class UiFinalCaptureStep
    {
        public readonly string Name, View, Focus;
        public readonly int Page;
        public readonly float? Scroll;
        public readonly bool ExpandCameraBindings;

        public UiFinalCaptureStep(string name, string view, int page, float? scroll = null,
            string focus = null, bool expandCameraBindings = false)
        {
            Name = name; View = view; Page = page; Scroll = scroll;
            Focus = focus; ExpandCameraBindings = expandCameraBindings;
        }
    }

    internal static class UiFinalCapturePlan
    {
        public static IEnumerable<UiFinalCaptureStep> Create()
        {
            yield return new UiFinalCaptureStep("final-controls-top", "Simple", 0, 0);
            yield return new UiFinalCaptureStep("final-actions-top", "Simple", 0, 0, "art-LookLeft-bind");
            // Focus the following disclosure so Look forward's value and device lines remain above it.
            yield return new UiFinalCaptureStep("final-actions-lower", "Simple", 0, null, "disclosure-mod buttons and Settings key");
            yield return new UiFinalCaptureStep("final-cameras-top", "Simple", 2, 0);
            // Each CameraAdjustmentBinding draws a keyboard row followed by its USB row.
            // The next keyboard button (or the terminal reset button) scrolls the whole preceding pair into view.
            for (int pair = 0; pair < 11; pair++)
                yield return new UiFinalCaptureStep(pair == 10 ? "final-cameras-lower" : "final-camera-binding-" + pair.ToString("00"),
                    "Simple", 2, pair == 0 ? (float?)0 : null,
                    pair == 10 ? "cmd-Restore numpad defaults" : "camkey-bind-" + (pair + 1), true);
            yield return new UiFinalCaptureStep("final-advanced-cameras-top", "Advanced", 2, 0);
            yield return new UiFinalCaptureStep("final-help-top", "Simple", 4, 0);
            yield return new UiFinalCaptureStep("final-help-lower", "Simple", 4, 100000);
        }
    }
}
