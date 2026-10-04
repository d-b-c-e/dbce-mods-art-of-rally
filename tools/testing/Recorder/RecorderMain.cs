using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using Dbce.Wheel.Telemetry;
using Dbce.Wheel.Playback;

namespace ArtOfSimRally.Testing
{
    public static class RecorderMain
    {
        private static readonly CaptureSession Session = new CaptureSession();
        private static SubjectAccess subject;
        private static Harmony patches;
        private static ControlServer server;
        private static Action<string> log;
        private static bool observing, sent;
        private static int device;
        private static CarDynamics motionCar;
        private static Rigidbody motionBody;
        private static BufferedTapeWriter packetWriter;
        private static BufferedTapeWriter effectWriter;
        private static readonly byte[] packetBuffer = new byte[ForzaPacket.Size];
        private static int packetCount;
        private const string PatchId = "ArtOfSimRally.DevRecorder";
        private struct Step { public bool Valid; public ForceSample Sample; }

        public static bool Load(UnityModManager.ModEntry entry)
        {
            try
            {
                log = entry.Logger.Log;
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Attach(assemblies.Single(a => a.GetName().Name == "ArtOfSimRally.Mod"),
                    assemblies.Single(a => a.GetName().Name == "Dbce.Wheel.Ffb"));
                server = new ControlServer("ArtOfSimRally.DevRecorder." + Process.GetCurrentProcess().Id);
                SessionTape.TryArm(m => entry.Logger.Log(m));
                entry.OnUpdate = (mod, delta) => { server?.Pump(Command); SessionTape.Tick(); };
                entry.OnUnload = Unload;
                entry.Logger.Log("Developer capture probe ready. Use tools/testing/Record-Drive.ps1; no recording starts automatically.");
                return true;
            }
            catch (Exception ex)
            {
                patches?.UnpatchAll(PatchId); server?.Dispose(); server = null;
                entry.Logger.Error("Developer probe could not attach: " + ex.Message); return false;
            }
        }
        private static void Attach(System.Reflection.Assembly mod, System.Reflection.Assembly force, bool includeCollision = true)
        {
            subject = new SubjectAccess(mod, force);
            patches = new Harmony(PatchId);
            patches.Patch(subject.Drive, prefix: Hook(nameof(BeforeForce)), postfix: Hook(nameof(AfterForce)));
            patches.Patch(subject.Send, prefix: new HarmonyMethod(typeof(RecorderMain), nameof(BeforeSend)) { priority = Priority.First }, postfix: Hook(nameof(AfterSend)));
            patches.Patch(AccessTools.Method(mod.GetType("ArtOfSimRally.Mod.TelemetryPump", true), "SendFrame"),
                prefix: new HarmonyMethod(typeof(RecorderMain), nameof(ObservePacket)) { priority = Priority.First });
            var native = force.GetType("Dbce.Wheel.Ffb.WheelFfbNative", true);
            foreach (var method in native.GetMethods().Where(m => new[] { "UpdatePeriodic", "PlayConstantBurst", "PlayPeriodicBurst", "PlayShapedPeriodicBurst", "UpdateCondition" }.Contains(m.Name)))
                patches.Patch(method, prefix: new HarmonyMethod(typeof(RecorderMain), nameof(ObserveEffect)) { priority = Priority.First });
            patches.Patch(subject.Reset, postfix: Hook(nameof(Reset)));
            patches.Patch(subject.Update, postfix: Hook(nameof(Frame)));
            patches.Patch(subject.Shutdown, postfix: Hook(nameof(Shutdown)));
            patches.Patch(subject.Exit, prefix: Hook(nameof(BeforeGameExit)));
            if (includeCollision) patches.Patch(subject.Collision, prefix: Hook(nameof(BeforeCollision)));
        }
        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(RecorderMain), name);
        internal static void StartSessionSignals(string directory, bool physicalOutput, bool synthetic)
        {
            var identity = subject.Identity(Application.version, Application.unityVersion);
            identity.SetAttributeValue("physicalOutput", physicalOutput);
            identity.SetAttributeValue("captureSource", synthetic ? "synthetic-integration" : "player");
            identity.SetAttributeValue("forceUnits", "signed-normalized-request; device column uses nominal +/-10000 units, not measured torque");
            identity.SetAttributeValue("deliveryMeaning", physicalOutput ? "native-api-acceptance" : "muted-native-api-acceptance");
            if (!Session.Start(false, identity, Path.Combine(directory, "signals"))) throw new InvalidOperationException(Session.Status);
            packetCount = 0;
            packetWriter = new BufferedTapeWriter(Path.Combine(directory, "telemetry.tsv"));
            packetWriter.WriteLine("sample\ttime_s\tforza_horizon324_base64");
            effectWriter = new BufferedTapeWriter(Path.Combine(directory, "effects.tsv"));
            effectWriter.WriteLine("time_s\tphysics_time_s\tmethod\trequested_arguments_before_mute");
        }
        internal static void StopSessionSignals(string directory)
        {
            packetWriter?.Dispose(); packetWriter = null;
            effectWriter?.Dispose(); effectWriter = null;
            if (!Session.Pending) return;
            if (!Session.Stop(false)) throw new IOException(Session.Status);
            var receipt = System.Xml.Linq.XDocument.Load(Path.Combine(Session.SavedDirectory, "manifest.xml"));
            if ((string)receipt.Root.Attribute("complete") != "true") throw new IOException("Signal capture is incomplete: " + receipt.Root.Element("error")?.Value);
            // A flat, sealed analysis bundle beside the trajectory. Existing offline tools accept it directly.
            foreach (var file in Directory.GetFiles(Session.SavedDirectory))
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)), false);
        }
        private static void ObservePacket(TelemetryFrame __0)
        {
            if (packetWriter == null) return;
            try
            {
                ForzaPacket.Write(__0, packetBuffer);
                packetWriter.WriteLine(packetCount++ + "\t" + Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\t" + Convert.ToBase64String(packetBuffer));
            }
            catch (Exception ex) { SessionTape.CaptureError("telemetry: " + ex.Message); Session.Incomplete("telemetry: " + ex.Message); }
        }
        private static void ObserveEffect(System.Reflection.MethodBase __originalMethod, object[] __args)
        {
            if (effectWriter == null) return;
            try
            {
                var parameters = __originalMethod.GetParameters();
                string args = string.Join(";", __args.Select((value, i) => parameters[i].Name + "=" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)));
                effectWriter.WriteLine(Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                    Time.fixedTime.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\t" + __originalMethod.Name + "\t" + args);
            }
            catch (Exception ex) { SessionTape.CaptureError("effect requests: " + ex.Message); Session.Incomplete("effect requests: " + ex.Message); }
        }
        private static string Command(string command)
        {
            var tape = SessionTape.Command(command);
            if (tape != null) return tape;
            if (command == "STATUS") return "OK " + Session.Describe;
            if (command == "START")
            {
                if (subject.Driving() || Session.Pending) return "ERROR pause and finish any pending capture before START";
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArtOfSimRally", "dev-captures");
                bool ok = Session.Start(false, subject.Identity(Application.version, Application.unityVersion), root);
                return (ok ? "OK " : "ERROR ") + Session.Status;
            }
            bool stopped = Session.Stop(subject.Driving());
            return (stopped ? "OK " : "ERROR ") + Session.Status;
        }
        private static bool Unload(UnityModManager.ModEntry entry)
        {
            SessionTape.Unload();
            if (Session.Pending && !Session.Stop(subject.Driving())) { entry.Logger.Warning(Session.Status); return false; }
            patches?.UnpatchAll(PatchId); server?.Dispose(); server = null; return true;
        }
        private static void BeforeForce(CarDynamics __0, out Step __state)
        {
            __state = default; observing = false; sent = false;
            try
            {
                if (!Session.Active || !subject.Enabled() || !subject.Driving() || !subject.ForceEnabled() || !subject.Ready() ||
                    subject.SettingsVisible() || !subject.Focused()) return;
                var front = __0.axles?.frontAxle;
                if (front?.leftWheel == null || front.rightWheel == null) return;
                var left = front.leftWheel; var right = front.rightWheel; var tune = subject.ReadTune();
                var sample = new ForceSample
                {
                    Time = Time.realtimeSinceStartup,
                    Fy = left.Fy + right.Fy,
                    Slip = .5f * (Math.Abs(left.slipAngle) + Math.Abs(right.slipAngle)),
                    Ideal = left.idealSlipAngle,
                    Speed = __0.velo * 3.6f,
                    Reference = tune.Reference,
                    Gain = tune.Gain,
                    Smoothing = tune.Smoothing,
                    Invert = tune.Invert ? 1 : 0,
                    Previous = subject.Smoothed(),
                    Motion = ReadMotion(__0)
                };
                __state = new Step { Valid = true, Sample = sample }; observing = true;
            }
            catch (Exception ex) { Session.AbortSampling(ex.Message); }
        }
        // Observe at the same CarDynamics postfix boundary as steering force.
        // Unity's ordering of other components still needs a real capture.
        private static MotionSample ReadMotion(CarDynamics car)
        {
            if (motionCar != car)
            {
                motionCar = car; motionBody = car.GetComponent<Rigidbody>();
            }
            var front = car.axles?.frontAxle; var rear = car.axles?.rearAxle;
            if (motionBody == null || front?.leftWheel == null || front.rightWheel == null ||
                rear?.leftWheel == null || rear.rightWheel == null) return default;
            var fl = front.leftWheel; var fr = front.rightWheel; var rl = rear.leftWheel; var rr = rear.rightWheel;
            var position = motionBody.position; var velocity = motionBody.velocity; var rotation = motionBody.rotation;
            var local = Quaternion.Inverse(rotation) * velocity;
            return new MotionSample
            {
                Valid = 1, PhysicsTime = Time.fixedTime,
                ContactMask = (fl.onGroundDown ? 1 : 0) | (fr.onGroundDown ? 2 : 0) | (rl.onGroundDown ? 4 : 0) | (rr.onGroundDown ? 8 : 0),
                Px = position.x, Py = position.y, Pz = position.z, Vx = velocity.x, Vy = velocity.y, Vz = velocity.z,
                Qx = rotation.x, Qy = rotation.y, Qz = rotation.z, Qw = rotation.w, LocalVx = local.x, LocalVy = local.y, LocalVz = local.z,
                CompressionFL = fl.compression, CompressionFR = fr.compression, CompressionRL = rl.compression, CompressionRR = rr.compression,
                TravelFL = fl.suspensionTravel, TravelFR = fr.suspensionTravel, TravelRL = rl.suspensionTravel, TravelRR = rr.suspensionTravel
            };
        }
        private static void AfterForce(Step __state)
        {
            observing = false;
            if (!__state.Valid) return;
            try
            {
                if (!sent) { Session.Incomplete("Native force observation hook did not execute"); return; }
                var sample = __state.Sample; sample.Output = subject.Smoothed(); sample.Device = device;
                Session.Force(sample);
            }
            catch (Exception ex) { Session.AbortSampling(ex.Message); }
        }
        private static void BeforeSend(int __0) { if (observing) { device = __0; sent = true; } }
        // Passive prefix: the original callback may finish the stage for
        // terminal damage. Never change its arguments, return value or physics.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void BeforeCollision(PlayerCollider __instance, Collision __0)
        {
            try
            {
                if (!Session.Active || !subject.Enabled() || !subject.Driving() || subject.Restarting() ||
                    __instance == null || __0 == null) return;
                var body = __instance.GetComponent<Rigidbody>();
                if (body == null || body != (subject.PlayerBody() as Rigidbody)) return;
                var other = __0.collider;
                if (other == null) return;
                var relative = __0.relativeVelocity; var impulse = __0.impulse;
                var p = body.position; var q = body.rotation; var v = body.velocity;
                int contacts = __0.contactCount, examined = Math.Min(contacts, CollisionFormat.ContactLimit), selected = -1;
                Vector3 normal = Vector3.zero, point = Vector3.zero;
                float strongest = -1;
                for (int i = 0; i < examined; i++)
                {
                    var contact = __0.GetContact(i);
                    float projection = Math.Abs(Vector3.Dot(relative, contact.normal));
                    if (projection > strongest)
                    { strongest = projection; selected = i; normal = contact.normal; point = contact.point; }
                }
                Session.Collision(new CollisionSample
                {
                    Time = Time.realtimeSinceStartup, PhysicsTime = Time.fixedTime,
                    BodyId = body.GetInstanceID(), OtherId = other.GetInstanceID(), OtherLayer = other.gameObject.layer,
                    Road = other.CompareTag("Road") ? 1 : 0, Crowd = other.CompareTag("Crowd") ? 1 : 0,
                    Contacts = contacts, Examined = examined, Selected = selected,
                    Rvx = relative.x, Rvy = relative.y, Rvz = relative.z, Ix = impulse.x, Iy = impulse.y, Iz = impulse.z,
                    Mass = body.mass, Px = p.x, Py = p.y, Pz = p.z, Qx = q.x, Qy = q.y, Qz = q.z, Qw = q.w,
                    Vx = v.x, Vy = v.y, Vz = v.z, Nx = normal.x, Ny = normal.y, Nz = normal.z,
                    Cpx = point.x, Cpy = point.y, Cpz = point.z
                });
            }
            catch (Exception ex) { Session.AbortSampling("Collision observation: " + ex.Message); }
        }
        private static void AfterSend(bool __result) { if (observing) Session.Delivery(__result); }
        private static void Reset() => Session.Reset();
        private static void Frame()
        {
            try { if (Session.Active) Session.Frame(subject.Frame(Time.frameCount, Time.realtimeSinceStartup, Time.unscaledDeltaTime)); }
            catch (Exception ex) { Session.AbortSampling(ex.Message); }
        }
        // Runs after the shipping watchdog has released all force/input resources.
        private static void Shutdown()
        {
            SessionTape.Stop("game shutdown");
            if (Session.Pending) { Session.Stop(false); log?.Invoke(Session.Status); }
            if (!Session.Pending) { server?.Dispose(); server = null; }
            motionCar = null; motionBody = null;
        }
        private static bool BeforeGameExit()
        {
            SessionTape.Stop("game exit");
            bool allow = Session.BeforeProcessExit(() => subject.Shutdown.Invoke(null, new object[] { false }));
            if (!allow) log?.Invoke(Session.Status);
            return allow;
        }
    }
}
