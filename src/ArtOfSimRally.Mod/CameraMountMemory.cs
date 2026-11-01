using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    // Remembers the bonnet/bumper placement per car (Settings.CameraMounts) and fits a starting placement for a car never
    // tuned (CameraMountFit). Runs from the mounted camera only, so the stock views never pay for it.
    internal static class CameraMountMemory
    {
        private const string CustomVisual = "CustomVehicle_VisualOnly";
        private const string RealisticSuffix = "|realistic";
        private static Transform _target, _root;
        private static bool _custom, _realistic;
        private static string _key;
        private static float[] _extent, _loaded;

        // Each car keeps two sets, Game and Realistic (F7), stored separately (owner 2026-10-31: adjustments in one mode
        // must not carry into the other).
        internal static void Track(Transform target, Settings cfg)
        {
            bool realistic = Triple.TripleScreen.RealisticActive;
            if (target != _target)
            {
                _target = target;
                _root = target.GetComponentInParent<CarDynamics>()?.transform ?? target;
                _key = null;
            }
            bool custom = _root.Find(CustomVisual) != null;
            if (_key != null && custom == _custom && realistic == _realistic) { Remember(cfg); return; }
            if (_key != null) Remember(cfg);   // keep the set being left
            _custom = custom; _realistic = realistic;
            Load(cfg);
        }

        private static void Load(Settings cfg)
        {
            _key = _root.name.Replace("(Clone)", "").Trim() + (_custom ? "+" + CustomId() : "") + (_realistic ? RealisticSuffix : "");
            _extent = Measure(_target);
            var saved = cfg.CameraMounts.LastOrDefault(m => m.Car == _key && m.Values?.Length == CameraMountFit.Values);
            string how = "saved";
            if (saved != null) Apply(cfg, saved.Values);
            else
            {
                // Start from the last tuned car in the same set, fitted to this car's size; the Realistic set otherwise starts
                // from this car's Game values at the seat's true field of view.
                var reference = cfg.CameraMounts.LastOrDefault(m => m.Values?.Length == CameraMountFit.Values && CameraMountFit.Valid(m.Extent)
                    && m.Car.EndsWith(RealisticSuffix) == _realistic);
                if (reference != null && CameraMountFit.Valid(_extent)) { Apply(cfg, CameraMountFit.Fit(reference.Values, reference.Extent, _extent)); how = "fitted from " + reference.Car; }
                else how = "current values";
                if (_realistic && reference == null && Triple.TripleScreen.RealisticStartDegrees is double seat)
                { cfg.BonnetFOV = cfg.BumperFOV = (float)Math.Round(seat, 1); how += ", realistic field of view " + seat.ToString("0") + "°"; }
                Store(cfg, Snapshot(cfg));
            }
            _loaded = Snapshot(cfg);
            ModLog.Info("Camera mounts for " + _key + ": " + how + ".");
        }

        private static void Remember(Settings cfg)
        {
            if (_key == null || _loaded == null) return;
            var now = Snapshot(cfg);
            if (now.SequenceEqual(_loaded)) return;
            _loaded = now;
            Store(cfg, now);
        }

        // The latest stored set is the reference for the next untuned car in the same set.
        private static void Store(Settings cfg, float[] values)
        {
            cfg.CameraMounts.RemoveAll(m => m.Car == _key);
            cfg.CameraMounts.Add(new CameraMount { Car = _key, Values = values, Extent = CameraMountFit.Valid(_extent) ? _extent : null });
            if (cfg.CameraMounts.Count > 128) cfg.CameraMounts.RemoveAt(0);
        }

        private static float[] Snapshot(Settings c) => new[] { c.BonnetSide, c.BonnetHeight, c.BonnetForward, c.BonnetPitch, c.BonnetFOV,
            c.BumperSide, c.BumperHeight, c.BumperForward, c.BumperPitch, c.BumperFOV };

        private static void Apply(Settings c, float[] v)
        {
            c.BonnetSide = v[0]; c.BonnetHeight = v[1]; c.BonnetForward = v[2]; c.BonnetPitch = v[3]; c.BonnetFOV = v[4];
            c.BumperSide = v[5]; c.BumperHeight = v[6]; c.BumperForward = v[7]; c.BumperPitch = v[8]; c.BumperFOV = v[9];
        }

        // Visible car meshes (a Custom Vehicles body hides the donor's), in the camera target's space.
        private static float[] Measure(Transform target)
        {
            bool any = false; float bottom = 0, top = 0, front = 0, left = 0, right = 0;
            foreach (var r in _root.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || r.forceRenderingOff ||
                    r.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = target.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (!any) { bottom = top = p.y; front = p.z; left = right = p.x; any = true; }
                    bottom = Mathf.Min(bottom, p.y); top = Mathf.Max(top, p.y); front = Mathf.Max(front, p.z);
                    left = Mathf.Min(left, p.x); right = Mathf.Max(right, p.x);
                }
            }
            return any ? new[] { bottom, top, front, right - left } : null;
        }

        // Custom Vehicles' active package id, by name so this mod does not depend on it.
        private static string CustomId()
        {
            try
            {
                var type = AccessTools.TypeByName("RallyCustomVehicles.Main");
                return type == null ? "custom" : Traverse.Create(type).Field("activePackage").Property("Manifest").Field("id").GetValue<string>() ?? "custom";
            }
            catch (Exception) { return "custom"; }
        }
    }
}
