using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArtOfSimRally.Testing
{
    // Capture resolved selections at the load boundary, after all menu randomness.
    // Fixed game-owned data types only; never deserialize CLR type names from a tape.
    internal sealed class ScenePlayback
    {
        [Serializable]
        private sealed class Selection
        {
            public int sceneIndex;
            public string scenePath;
            public GameModeManager.GAME_MODES mode;
            public Season season;
        }
        private int index;
        internal void BeforeLoad(string directory, bool recording, ref int scene)
        {
            if (SceneLoader.Fading) return; // stock ignores this call too
            string path = Path.Combine(directory, "load-" + index.ToString("0000") + ".json");
            if (recording)
            {
                var selection = new Selection { sceneIndex = scene, scenePath = SceneUtility.GetScenePathByBuildIndex(scene), mode = GameModeManager.GameMode };
                // Menu loads have no season to restore.
                if (scene != 0 && scene != 3 && selection.mode != GameModeManager.GAME_MODES.NULL)
                    selection.season = GameModeManager.GetSeasonDataCurrentGameMode();
                File.WriteAllText(path, JsonUtility.ToJson(selection));
            }
            else
            {
                if (!File.Exists(path)) throw new InvalidDataException("Missing recorded scene load " + index);
                var selection = JsonUtility.FromJson<Selection>(File.ReadAllText(path));
                if (selection.scenePath != SceneUtility.GetScenePathByBuildIndex(selection.sceneIndex))
                    throw new InvalidDataException("Scene build identity differs from recording");
                if (selection.mode != GameModeManager.GameMode) throw new InvalidDataException("Game mode differs at scene load " + index);
                if (selection.season != null)
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(selection.season), GameModeManager.GetSeasonDataCurrentGameMode());
                scene = selection.sceneIndex;
            }
            index++;
        }
    }
}
