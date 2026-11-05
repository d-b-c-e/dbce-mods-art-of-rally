using System;
using HarmonyLib;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    // Same model/package identity for mounted and stock chase offsets. Runtime
    // objects and clone suffixes never become persistent camera keys.
    internal static class CameraCarIdentity
    {
        internal const string CustomVisual = "CustomVehicle_VisualOnly";
        internal static Transform Root(Transform target)
        {
            if (target == null) return null;
            var car = target.GetComponentInParent<CarDynamics>();
            return car == null ? target : car.transform;
        }
        internal static string Key(Transform target)
        {
            var root = Root(target);
            if (root == null) return null;
            string model = root.name.Replace("(Clone)", "").Trim();
            if (model.Length == 0) return null;
            if (root.Find(CustomVisual) == null) return model;
            try
            {
                var type = AccessTools.TypeByName("RallyCustomVehicles.Main");
                string id = type == null ? null : Traverse.Create(type).Field("activePackage").Property("Manifest").Field("id").GetValue<string>();
                return string.IsNullOrWhiteSpace(id) ? null : model + "+" + id;
            }
            catch (Exception) { return null; }
        }
    }
}
