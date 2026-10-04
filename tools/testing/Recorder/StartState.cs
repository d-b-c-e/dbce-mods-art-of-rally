using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ArtOfSimRally.Testing
{
    /// <summary>
    /// The player car's complete simulation state at the first tick of a stage:
    /// every value-type field of the game's car components (and plain objects they
    /// own, such as the clutch) plus rigidbody motion. Recorded once and restored
    /// once, so a strict replay starts the stage from the taped state; nothing is
    /// adjusted after that.
    /// </summary>
    /// <remarks>
    /// The countdown isn't tick-locked, so engine revs, clutch and wheel spin at
    /// the go differ slightly between runs, and the rally physics amplifies that.
    /// Drivetrain timestamps are game-clock values; they are moved onto the live
    /// clock instead of copied.
    /// </remarks>
    internal static class StartState
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly HashSet<string> TimeStamps = new HashSet<string>
            { "Drivetrain.ShiftDelay", "Drivetrain.lastShiftTime", "Drivetrain.nextStartImpulse", "Drivetrain.duration" };

        internal static void Save(GameObject car, string path)
        {
            var lines = new List<string> { "time\t" + N(Time.time) };
            foreach (var (key, component) in Components(car))
            {
                if (component is Rigidbody body)
                {
                    lines.Add(key + "\tposition\t" + V(body.position));
                    lines.Add(key + "\trotation\t" + Q(body.rotation));
                    lines.Add(key + "\tvelocity\t" + V(body.velocity));
                    lines.Add(key + "\tangularVelocity\t" + V(body.angularVelocity));
                    continue;
                }
                foreach (var (name, value) in Values(component, component.GetType().Name, 0))
                    lines.Add(key + "\t" + name + "\t" + value);
            }
            File.WriteAllLines(path, lines);
        }

        /// <summary>Restores a saved state; returns the number of values applied.</summary>
        internal static int Restore(GameObject car, string path, Action<string> log)
        {
            var lines = File.ReadAllLines(path);
            float recordedTime = float.Parse(lines[0].Split('\t')[1], CultureInfo.InvariantCulture);
            float clockShift = Time.time - recordedTime;
            var byKey = Components(car).ToDictionary(p => p.Item1, p => p.Item2);
            int applied = 0, missing = 0;
            foreach (var line in lines.Skip(1))
            {
                var p = line.Split('\t');
                if (p.Length < 3 || !byKey.TryGetValue(p[0], out var component)) { missing++; continue; }
                try
                {
                    if (component is Rigidbody body)
                    {
                        switch (p[1])
                        {
                            case "position": body.position = ParseV(p[2]); break;
                            case "rotation": body.rotation = ParseQ(p[2]); break;
                            case "velocity": body.velocity = ParseV(p[2]); break;
                            case "angularVelocity": body.angularVelocity = ParseV(p[2]); break;
                        }
                        applied++;
                        continue;
                    }
                    if (Apply(component, component.GetType().Name, p[1], p[2], clockShift)) applied++; else missing++;
                }
                catch { missing++; }
            }
            if (missing > 0) log?.Invoke("start state: " + missing + " values not applied");
            return applied;
        }

        // Game car components in a stable order: type name plus occurrence index.
        private static IEnumerable<(string, Component)> Components(GameObject car)
        {
            var seen = new Dictionary<string, int>();
            var all = car.GetComponentsInChildren<Component>(true)
                .Where(c => c is Rigidbody || (c is MonoBehaviour && c.GetType().Assembly == typeof(CarDynamics).Assembly));
            foreach (var c in all)
            {
                string type = c.GetType().Name;
                int index = seen.TryGetValue(type, out var n) ? n : 0;
                seen[type] = index + 1;
                yield return (type + "#" + index, c);
            }
        }

        private static IEnumerable<(string, string)> Values(object owner, string ownerType, int depth)
        {
            foreach (var field in owner.GetType().GetFields(Fields))
            {
                if (field.IsLiteral || field.IsInitOnly) continue;
                var value = field.GetValue(owner);
                string text = Format(field.FieldType, value);
                if (text != null) { yield return (field.Name, text); continue; }
                // Plain objects the component owns (e.g. Drivetrain.clutch), one level deep.
                if (depth == 0 && value != null && field.FieldType.IsClass && !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) &&
                    !field.FieldType.IsArray && field.FieldType != typeof(string) && field.FieldType.Assembly == typeof(CarDynamics).Assembly)
                    foreach (var (name, inner) in Values(value, field.FieldType.Name, 1))
                        yield return (field.Name + "." + name, inner);
            }
        }

        private static bool Apply(object owner, string ownerType, string name, string text, float clockShift)
        {
            int dot = name.IndexOf('.');
            if (dot > 0)
            {
                var outer = owner.GetType().GetField(name.Substring(0, dot), Fields);
                var inner = outer?.GetValue(owner);
                return inner != null && Apply(inner, inner.GetType().Name, name.Substring(dot + 1), text, clockShift);
            }
            var field = owner.GetType().GetField(name, Fields);
            if (field == null || field.IsLiteral || field.IsInitOnly) return false;
            object value = Parse(field.FieldType, text);
            if (value == null) return false;
            if (TimeStamps.Contains(ownerType + "." + name) && value is float stamp) value = stamp + clockShift;
            field.SetValue(owner, value);
            return true;
        }

        private static string Format(Type type, object value)
        {
            if (value == null) return null;
            if (type == typeof(float)) return N((float)value);
            if (type == typeof(double)) return ((double)value).ToString("R", CultureInfo.InvariantCulture);
            if (type == typeof(int) || type == typeof(bool) || type.IsEnum) return Convert.ToString(type.IsEnum ? (object)Convert.ToInt32(value) : value, CultureInfo.InvariantCulture);
            if (type == typeof(Vector3)) return V((Vector3)value);
            if (type == typeof(Vector2)) { var v = (Vector2)value; return N(v.x) + "," + N(v.y); }
            if (type == typeof(Quaternion)) return Q((Quaternion)value);
            return null;
        }

        private static object Parse(Type type, string text)
        {
            if (type == typeof(float)) return float.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(double)) return double.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(int)) return int.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(bool)) return bool.Parse(text);
            if (type.IsEnum) return Enum.ToObject(type, int.Parse(text, CultureInfo.InvariantCulture));
            if (type == typeof(Vector3)) return ParseV(text);
            if (type == typeof(Vector2)) { var p = text.Split(','); return new Vector2(F(p[0]), F(p[1])); }
            if (type == typeof(Quaternion)) return ParseQ(text);
            return null;
        }

        private static string N(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        private static string V(Vector3 v) => N(v.x) + "," + N(v.y) + "," + N(v.z);
        private static string Q(Quaternion q) => N(q.x) + "," + N(q.y) + "," + N(q.z) + "," + N(q.w);
        private static Vector3 ParseV(string s) { var p = s.Split(','); return new Vector3(F(p[0]), F(p[1]), F(p[2])); }
        private static Quaternion ParseQ(string s) { var p = s.Split(','); return new Quaternion(F(p[0]), F(p[1]), F(p[2]), F(p[3])); }
    }
}
