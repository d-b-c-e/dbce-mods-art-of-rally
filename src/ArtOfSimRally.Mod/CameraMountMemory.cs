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
        private const string CustomVisual = CameraCarIdentity.CustomVisual;
        private const string RealisticSuffix = "|realistic";
        // Position indexes in the CameraMountFit value order (FOV, at 4 and 9, is kept per camera instead).
        private static readonly int[] Positions = { 0, 1, 2, 3, 5, 6, 7, 8 };
        private static Transform _target, _root;
        private static bool _realistic;
        private static Settings _settings;
        private static string _key;
        private static string _car;
        private static float[] _extent, _base, _loaded;
        private static float _nextFit;
        private static double? _seat;

        // Each car keeps two sets, Game and Realistic (F7), stored separately (owner 2026-10-31: adjustments in one mode
        // must not carry into the other).
        // The set follows the camera on screen: bonnet and bumper each keep their own F7 choice (owner 2026-11-01).
        internal static void Track(Transform target, Settings cfg, bool bumper)
        {
            bool realistic = Triple.TripleScreen.RealisticFor(bumper ? Triple.CameraOverlay.Kind.Bumper : Triple.CameraOverlay.Kind.Bonnet);
            var seat = realistic ? Triple.TripleScreen.RealisticStartDegrees : null;
            string car = CameraCarIdentity.Key(target);
            string key = car == null ? null : car + (realistic ? RealisticSuffix : "");
            if (ReferenceEquals(cfg, _settings) && target == _target && key == _key && realistic == _realistic && seat == _seat &&
                (_extent != null || Time.unscaledTime < _nextFit))
            { Remember(cfg); return; }
            // Capture before changing target, key or set. Save/reload may also
            // replace Settings while Unity keeps the same target alive.
            Capture(_settings);
            bool measure = target != _target || car != _car || _extent == null;
            _target = target; _root = CameraCarIdentity.Root(target);
            _car = car; _key = key; _realistic = realistic; _seat = seat; _settings = cfg;
            if (_root == null || _key == null) { _loaded = null; _base = null; return; }
            Load(cfg, measure);
        }

        private static void Load(Settings cfg, bool measure)
        {
            if (measure)
            {
                _extent = Measure(_target);
                _nextFit = Time.unscaledTime + 1f; // a late body can replace an unmeasured start
            }
            _base = BaseFor(cfg);
            CameraMountFit.KeepOutside(_base, _extent);
            var saved = cfg.CameraMounts.LastOrDefault(m => m.Car == _key);
            var offsets = saved?.Offsets?.Length == Positions.Length ? saved.Offsets : null;
            string how = offsets == null ? "calculated" : "calculated + saved adjustment";
            var v = (float[])_base.Clone();
            if (offsets != null) for (int i = 0; i < Positions.Length; i++) v[Positions[i]] += offsets[i];
            v[4] += FovOffset(cfg, false); v[9] += FovOffset(cfg, true);
            // Only the calculated start is kept outside the body; saved tweaks may go anywhere.
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
            if (_realistic && _seat is double seat) bonnetFov = bumperFov = (float)Math.Round(seat, 1);
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
            if (!ReferenceEquals(cfg, _settings) || _key == null || _base == null) { cfg.ResetCameraMount(bumper); return; }
            var v = Snapshot(cfg);
            int from = bumper ? 5 : 0;
            for (int i = from; i < from + 5; i++) v[i] = _base[i];
            Apply(cfg, v);
            Remember(cfg);
            ModLog.Info("Camera mounts for " + _key + ": " + (bumper ? "bumper" : "bonnet") + " reset to the calculated placement.");
        }

        /// <summary>The same calculated baseline as whole-view Reset, for one F6 Default button.</summary>
        internal static float Default(Settings cfg, bool bumper, int field)
        {
            if (field < 0 || field > 4) throw new ArgumentOutOfRangeException(nameof(field));
            var baseline = ReferenceEquals(cfg, _settings) && _base != null ? _base : Snapshot(new Settings());
            return baseline[(bumper ? 5 : 0) + field];
        }

        // Called at the edit/save boundary too, so the last nudge need not wait
        // for another LateUpdate before its offsets can be serialized.
        internal static void Capture(Settings cfg)
        { if (cfg != null && ReferenceEquals(cfg, _settings)) Remember(cfg); }

        private static void Remember(Settings cfg)
        {
            if (!ReferenceEquals(cfg, _settings) || _key == null || _loaded == null) return;
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

    }
}
