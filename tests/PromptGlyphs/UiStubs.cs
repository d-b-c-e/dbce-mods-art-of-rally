using System;

namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
    public class HarmonyPostfix : Attribute { }
}
namespace UnityEngine
{
    public static class Time { public static int frameCount; }
    public class GameObject
    {
        public bool activeSelf;
        public void SetActive(bool value) { activeSelf = value; }
    }
}
namespace UnityEngine.UI
{
    public class Image { public bool enabled = true; public object sprite; }
    public class Text { public bool enabled = true; public string text; }
}
public static class PadManager
{
    public static readonly Rewired.Player Player = new Rewired.Player();
    public static Rewired.Player GetPlayer() => Player;
}
public class ControlsRemapper
{
    public class UIActionSet
    {
        public bool isNegativeSplitAxis;
        public UnityEngine.UI.Image glyphImage = new UnityEngine.UI.Image();
        public UnityEngine.GameObject keyboardGlyph = new UnityEngine.GameObject();
        public UnityEngine.UI.Text keyboardText = new UnityEngine.UI.Text();
    }
}
public class ControllerButtonDisplay { public enum AxisPolarity { NONE, NEGATIVE, POSITIVE } }
public class ControllerGlyphs
{
    public object Artwork;
    public object GetGlyph(Guid hardware, int element, int range) => Artwork;
}
