using UnityEngine;
using Dbce.Wheel.Ui.Unity;

namespace ArtOfSimRally.Testing
{
    // Only this host/lifecycle adapter is game-specific. Text, layout and drawing
    // are source-vendored from the toolkit; the normal player mod has no recorder UI.
    internal sealed class RecorderHud : MonoBehaviour
    {
        private static GameObject host;
        private readonly SessionStatusHud hud = new SessionStatusHud();
        private bool failed;
        private static System.Action<string> log;

        internal static void Install(System.Action<string> logger)
        {
            log = logger;
            if (host != null || SessionTape.HudLine == null) return;
            host = new GameObject("ArtOfSimRally.RecorderStatus");
            DontDestroyOnLoad(host);
            host.AddComponent<RecorderHud>();
        }

        internal static void Remove()
        {
            if (host != null) Destroy(host);
            host = null;
        }

        private void OnGUI()
        {
            try { hud.Draw(SessionTape.HudLine, Screen.width >= Screen.height * 2.9f); }
            catch (System.Exception ex)
            {
                if (!failed) { failed = true; log?.Invoke("Recording indicator could not draw: " + ex); }
            }
        }
    }
}
