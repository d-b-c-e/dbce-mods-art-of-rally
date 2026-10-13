using ArtOfSimRally.Testing;
using System.Text.Json;

// Compile the production scene-load adapter. The JSON below reduces the
// owner's sealed load-0001.json: MainMenu, CUSTOM, non-null empty Season.
internal static class ScenePlaybackTests
{
    internal static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "art-scene-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (int menu in new[] { 0, 3 })
            {
                Write(root, menu, 1, "[]");
                GameModeManager.Current = new Season { Rallies = new[] { 42 } };
                GameModeManager.GameMode = GameModeManager.GAME_MODES.CUSTOM;
                int scene = 32;
                new ScenePlayback().BeforeLoad(root, false, ref scene);
                check(scene == menu, "recorded menu target retained");
                check(GameModeManager.Current.Rallies.SequenceEqual(new[] { 42 }), "menu placeholder must preserve completed rally");
            }
            Write(root, 32, 1, "[7,8]");
            int stage = 99;
            new ScenePlayback().BeforeLoad(root, false, ref stage);
            check(stage == 32 && GameModeManager.Current.Rallies.SequenceEqual(new[] { 7, 8 }), "stage still restores recorded season and scene");

            Write(root, 32, 0, "[]");
            GameModeManager.GameMode = GameModeManager.GAME_MODES.NULL;
            new ScenePlayback().BeforeLoad(root, false, ref stage);
            check(GameModeManager.Current.Rallies.Length == 2, "NULL mode cannot overwrite season");

            GameModeManager.GameMode = GameModeManager.GAME_MODES.CUSTOM;
            bool refused = false;
            try { new ScenePlayback().BeforeLoad(root, false, ref stage); }
            catch (InvalidDataException) { refused = true; }
            check(refused && GameModeManager.Current.Rallies.Length == 2, "mode mismatch refused before season mutation");

            File.WriteAllText(Path.Combine(root, "load-0000.json"), "{\"sceneIndex\":32,\"scenePath\":\"other-build\",\"mode\":1,\"season\":{\"Rallies\":[]}}");
            refused = false;
            try { new ScenePlayback().BeforeLoad(root, false, ref stage); }
            catch (InvalidDataException) { refused = true; }
            check(refused && GameModeManager.Current.Rallies.Length == 2, "build mismatch refused before season mutation");

            // Ignored fading calls must not consume a recorded load.
            Write(root, 32, 1, "[9]");
            var playback = new ScenePlayback();
            SceneLoader.Fading = true;
            stage = 99;
            playback.BeforeLoad(root, false, ref stage);
            check(stage == 99, "fading load ignored");
            SceneLoader.Fading = false;
            playback.BeforeLoad(root, false, ref stage);
            check(stage == 32 && GameModeManager.Current.Rallies.Single() == 9, "next real load consumes same selection");
        }
        finally { SceneLoader.Fading = false; Directory.Delete(root, true); }
    }

    private static void Write(string root, int scene, int mode, string rallies) =>
        File.WriteAllText(Path.Combine(root, "load-0000.json"),
            "{\"sceneIndex\":" + scene + ",\"scenePath\":\"scene-" + scene + "\",\"mode\":" + mode + ",\"season\":{\"Rallies\":" + rallies + "}}");
}

public sealed class Season { public int[] Rallies = Array.Empty<int>(); }
public static class GameModeManager
{
    public enum GAME_MODES { NULL, CUSTOM }
    public static GAME_MODES GameMode;
    public static Season Current = new();
    public static Season GetSeasonDataCurrentGameMode() => Current;
}
public static class SceneLoader { public static bool Fading; }
namespace UnityEngine
{
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
        public static string ToJson(object value) => JsonSerializer.Serialize(value, Options);
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
        public static void FromJsonOverwrite(string json, object target) =>
            ((Season)target).Rallies = FromJson<Season>(json).Rallies;
    }
}
namespace UnityEngine.SceneManagement
{
    public static class SceneUtility { public static string GetScenePathByBuildIndex(int index) => "scene-" + index; }
}
