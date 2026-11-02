using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    // Bonnet/bumper placement per car (owner 2026-11-01): each car starts from the placement calculated from its own shape
    // (CameraMountFit.Base), and the player's tweaks are kept as offsets on top - position per car and set
    // (Settings.CameraMounts), field of view per camera and set for every car (Settings.*FovOffset). Reset clears both.
    // Runs from the mounted camera only, so the stock views never pay for it.
    internal static class CameraMountMemory
    {
        private const string CustomVisual = "CustomVehicle_VisualOnly";
        private const string RealisticSuffix = "|realistic";
        // Position indexes in the CameraMountFit value order (FOV, at 4 and 9, is kept per camera instead).
        private static readonly int[] Positions = { 0, 1, 2, 3, 5, 6, 7, 8 };
        private static Transform _target, _root;
        private static bool _custom, _realistic;
        private static string _key;
        private static float[] _extent, _base, _loaded;

        // Each car keeps two sets, Game and Realistic (F7), stored separately (owner 2026-10-31: adjustments in one mode
        // must not carry into the other).
        // The set follows the camera on screen: bonnet and bumper each keep their own F7 choice (owner 2026-11-01).
        internal static void Track(Transform target, Settings cfg, bool bumper)
        {
            bool realistic = Triple.TripleScreen.RealisticFor(bumper ? Triple.CameraOverlay.Kind.Bumper : Triple.CameraOverlay.Kind.Bonnet);
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
            _base = BaseFor(cfg);
            var saved = cfg.CameraMounts.LastOrDefault(m => m.Car == _key);
            var offsets = saved?.Offsets?.Length == Positions.Length ? saved.Offsets : null;
            string how = "calculated";
            // Migration: a set adjusted under the old absolute values keeps its position as an offset; FOV starts from stock.
            if (offsets == null && saved != null && saved.Tuned && saved.Values?.Length == CameraMountFit.Values)
            {
                offsets = Positions.Select(i => saved.Values[i] - _base[i]).ToArray();
                Store(cfg, offsets); how = "calculated + migrated adjustment";
            }
            else if (offsets == null && saved != null) cfg.CameraMounts.Remove(saved);   // an old fitted set, never adjusted
            else if (offsets != null) how = "calculated + saved adjustment";
            var v = (float[])_base.Clone();
            if (offsets != null) for (int i = 0; i < Positions.Length; i++) v[Positions[i]] += offsets[i];
            v[4] += FovOffset(cfg, false); v[9] += FovOffset(cfg, true);
            // Only the calculated start is kept outside the body; the player's own tweaks may go anywhere (owner 2026-11-01).
            if (offsets == null) CameraMountFit.KeepOutside(v, _extent);
            Apply(cfg, v);
            _loaded = Snapshot(cfg);
            ModLog.Info("Camera mounts for " + _key + ": " + how + (_extent == null ? " (no measurement: defaults)" : "") +
                $"; bonnet {v[1]:0.00} m up, {v[2]:0.00} m forward; bumper {v[6]:0.00} up, {v[7]:0.00} forward.");
        }

        // The calculated placement for the current car and set; the mod's defaults when the car could not be measured. The
        // Realistic set starts at the seat's true field of view, the Game set at the mod's 75°/80°.
        private static float[] BaseFor(Settings cfg)
        {
            var d = Snapshot(new Settings());
            float bonnetFov = d[4], bumperFov = d[9];
            if (_realistic && Triple.TripleScreen.RealisticStartDegrees is double seat) bonnetFov = bumperFov = (float)Math.Round(seat, 1);
            var v = CameraMountFit.Base(_extent, bonnetFov, bumperFov, d[3], d[8]) ?? d;
            v[4] = bonnetFov; v[9] = bumperFov;
            return v;
        }

        private static float FovOffset(Settings c, bool bumper) => _realistic
            ? (bumper ? c.BumperFovRealisticOffset : c.BonnetFovRealisticOffset)
            : (bumper ? c.BumperFovOffset : c.BonnetFovOffset);

        private static void SetFovOffset(Settings c, bool bumper, float value)
        {
            if (_realistic) { if (bumper) c.BumperFovRealisticOffset = value; else c.BonnetFovRealisticOffset = value; }
            else { if (bumper) c.BumperFovOffset = value; else c.BonnetFovOffset = value; }
        }

        /// <summary>Whether the current set's field of view was changed from stock (the overlay's "(Custom)").</summary>
        internal static bool FovCustom(Settings c, bool bumper, bool realistic) => Math.Abs(realistic
            ? (bumper ? c.BumperFovRealisticOffset : c.BonnetFovRealisticOffset)
            : (bumper ? c.BumperFovOffset : c.BonnetFovOffset)) > .01f;

        /// <summary>Reset key / F6 button: this car's position and this camera's field of view (every car) back to stock.</summary>
        internal static void Reset(Settings cfg, bool bumper)
        {
            if (_key == null || _base == null) { cfg.ResetCameraMount(bumper); return; }
            var v = Snapshot(cfg);
            int from = bumper ? 5 : 0;
            for (int i = from; i < from + 5; i++) v[i] = _base[i];
            Apply(cfg, v);
            Remember(cfg);
            ModLog.Info("Camera mounts for " + _key + ": " + (bumper ? "bumper" : "bonnet") + " reset to the calculated placement.");
        }

        private static void Remember(Settings cfg)
        {
            if (_key == null || _loaded == null) return;
            var now = Snapshot(cfg);
            if (now.SequenceEqual(_loaded)) return;
            _loaded = now;
            SetFovOffset(cfg, false, now[4] - _base[4]); SetFovOffset(cfg, true, now[9] - _base[9]);
            Store(cfg, Positions.Select(i => now[i] - _base[i]).ToArray());
        }

        private static void Store(Settings cfg, float[] offsets)
        {
            cfg.CameraMounts.RemoveAll(m => m.Car == _key);
            if (offsets.Any(o => Math.Abs(o) > .0001f)) cfg.CameraMounts.Add(new CameraMount { Car = _key, Offsets = offsets });
            if (cfg.CameraMounts.Count > 128) cfg.CameraMounts.RemoveAt(0);
        }

        private static float[] Snapshot(Settings c) => new[] { c.BonnetSide, c.BonnetHeight, c.BonnetForward, c.BonnetPitch, c.BonnetFOV,
            c.BumperSide, c.BumperHeight, c.BumperForward, c.BumperPitch, c.BumperFOV };

        private static void Apply(Settings c, float[] v)
        {
            c.BonnetSide = v[0]; c.BonnetHeight = v[1]; c.BonnetForward = v[2]; c.BonnetPitch = v[3]; c.BonnetFOV = v[4];
            c.BumperSide = v[5]; c.BumperHeight = v[6]; c.BumperForward = v[7]; c.BumperPitch = v[8]; c.BumperFOV = v[9];
        }

        private static Collider[] BodyColliders() =>
            _root.GetComponentsInChildren<Collider>().Where(c => c is MeshCollider && c.enabled && !c.isTrigger).ToArray();

        // A renderer's mesh box in its own transform (null owner when it has no mesh).
        private static (Transform, Bounds) LocalBox(Renderer r)
        {
            if (r is SkinnedMeshRenderer skinned) return (skinned.transform, skinned.localBounds);
            var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
            return mesh == null ? (null, default) : (r.transform, mesh.bounds);
        }

        private static bool Visible(Renderer r) => (r is MeshRenderer || r is SkinnedMeshRenderer) && r.enabled && !r.forceRenderingOff &&
            r.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0;

        // Visible car meshes (a Custom Vehicles body hides the donor's), in the camera target's space: the box (bottom,
        // top, front, width) and, when its shape can be read, the bonnet height and windscreen base (CameraMountFit).
        private static float[] Measure(Transform target)
        {
            // The body only: a Custom Vehicles body's own meshes, else the car's solid colliders (lights, glows and effect
            // meshes made a van measure 7 m wide; owner log 2026-10-31), else every visible mesh.
            var visual = _root.Find(CustomVisual);
            // Each mesh's own local box, turned with the car. World-aligned Renderer/Collider.bounds grew with the car's yaw
            // and made cars measure 5.5-7 m wide (owner logs 2026-10-31/11-01).
            var boxes = visual != null
                ? visual.GetComponentsInChildren<Renderer>().Where(Visible).Select(LocalBox).Where(b => b.Item1 != null).ToList()
                : BodyColliders().OfType<MeshCollider>().Where(c => c.sharedMesh != null).Select(c => (c.transform, c.sharedMesh.bounds)).ToList();
            if (boxes.Count == 0) boxes = _root.GetComponentsInChildren<Renderer>().Where(Visible).Select(LocalBox).Where(b => b.Item1 != null).ToList();
            bool any = false; float bottom = 0, top = 0, front = 0, back = 0, left = 0, right = 0;
            foreach (var (owner, b) in boxes)
            {
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = target.InverseTransformPoint(owner.TransformPoint(corner));
                    if (!any) { bottom = top = p.y; front = back = p.z; left = right = p.x; any = true; }
                    bottom = Mathf.Min(bottom, p.y); top = Mathf.Max(top, p.y); front = Mathf.Max(front, p.z); back = Mathf.Min(back, p.z);
                    left = Mathf.Min(left, p.x); right = Mathf.Max(right, p.x);
                }
            }
            if (!any) return null;
            var box = new[] { bottom, top, front, right - left };
            var z = ProfileZ(front, back);
            var profile = MeshProfile(visual ?? _root, target, front, back, (right - left) / 4f);
            var hood = CameraMountFit.HoodProfile(z, profile);
            if (hood == null) { profile = ColliderProfile(target, front, back, bottom, top); hood = CameraMountFit.HoodProfile(z, profile); }
            // The centre-line heights front to back, so a wrong bonnet/windscreen can be checked from the log.
            ModLog.Info("Camera mounts: profile " + _root.name + " z " + front.ToString("0.00") + ".." + back.ToString("0.00") + ": " +
                string.Join(" ", profile.Select(h => float.IsNaN(h) ? "-" : h.ToString("0.00"))));
            var shaped = hood == null ? box : new[] { bottom, top, front, right - left, hood[0], hood[1] };
            if (!CameraMountFit.Plausible(box)) { ModLog.Info($"Camera mounts: {_root.name} measured {bottom:0.00}..{top:0.00} m high, {right - left:0.00} m wide: not a car body, no fit."); return null; }
            ModLog.Info($"Camera mounts: measured {_root.name}: bottom {bottom:0.00}, top {top:0.00}, front {front:0.00}, width {right - left:0.00}" +
                (hood == null ? "; bonnet shape not found (box fit)." : $"; bonnet {hood[0]:0.00} m high, windscreen base at {hood[1]:0.00} m."));
            return CameraMountFit.Plausible(shaped) ? shaped : box;
        }

        private const int Samples = 40;
        private static float[] ProfileZ(float front, float back)
        {
            var z = new float[Samples];
            for (int i = 0; i < Samples; i++) z[i] = front - (front - back) * i / (Samples - 1);
            return z;
        }

        // The top of the body's cross-sections at the centre line and a quarter-width either side, from meshes the mod
        // can read (Custom Vehicles bodies are; the game's own usually are not). NaN where nothing was found.
        private static float[] MeshProfile(Transform scope, Transform target, float front, float back, float halfStrip)
        {
            var h = Enumerable.Repeat(float.NaN, Samples).ToArray();
            float length = front - back; if (length <= .5f) return h;
            foreach (var filter in scope.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh; var r = filter.GetComponent<Renderer>();
                if (mesh == null || !mesh.isReadable || r == null || !Visible(r)) continue;
                var toTarget = target.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices; var xyz = new float[vertices.Length * 3];
                for (int v = 0; v < vertices.Length; v++)
                {
                    var p = toTarget.MultiplyPoint3x4(vertices[v]);
                    xyz[v * 3] = p.x; xyz[v * 3 + 1] = p.y; xyz[v * 3 + 2] = p.z;
                }
                CameraMountFit.Slice(h, front, length, xyz, mesh.triangles, new[] { -halfStrip / 2f, 0f, halfStrip / 2f });
            }
            return h;
        }

        // The car's own colliders probed from above along the centre line. NaN where nothing was hit.
        private static float[] ColliderProfile(Transform target, float front, float back, float bottom, float top)
        {
            var h = Enumerable.Repeat(float.NaN, Samples).ToArray();
            var z = ProfileZ(front, back);
            float reach = top - bottom + 2f;
            for (int i = 0; i < Samples; i++)
            {
                var origin = target.TransformPoint(new Vector3(0f, top + 1f, z[i]));
                foreach (var hit in Physics.RaycastAll(origin, -target.up, reach, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!(hit.collider is MeshCollider) || !hit.collider.enabled || hit.collider.isTrigger || !hit.collider.transform.IsChildOf(_root)) continue;
                    float y = target.InverseTransformPoint(hit.point).y;
                    if (float.IsNaN(h[i]) || y > h[i]) h[i] = y;
                }
            }
            return h;
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
