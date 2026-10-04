using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ArtOfSimRally.Testing
{
    internal static class MenuPlayback
    {
        internal static string Path(Transform transform)
        {
            var names = new List<string>();
            for (var t = transform; t != null; t = t.parent) names.Add(Uri.EscapeDataString(t.name));
            names.Reverse(); return string.Join("/", names);
        }
        private static Button[] Active() => UnityEngine.Object.FindObjectsOfType<Button>().Where(b => b.gameObject.activeInHierarchy && b.IsInteractable()).ToArray();
        internal static string Describe() => string.Join(" | ", Active().Select(b => Path(b.transform) + ":" + string.Join(";", b.GetComponentsInChildren<Text>().Select(t => t.text))));
        internal static void Click(string path)
        {
            var matches = Active().Where(b => Path(b.transform) == path).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected one active recorded button: " + path + ", got " + matches.Length);
            EventSystem.current?.SetSelectedGameObject(matches[0].gameObject);
            matches[0].onClick.Invoke();
        }
    }
}
