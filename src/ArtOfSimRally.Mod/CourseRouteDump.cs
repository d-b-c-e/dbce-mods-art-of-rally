using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// Read-only, one-launch, private capture of the current stage's own route and the game's own coarse progress, for
    /// the toolkit course seam (toolkit docs/COURSE-TELEMETRY.md). Off unless an operator's request file is claimed at
    /// load: only then is a <c>CarDynamics.FixedUpdate</c> postfix patched, so without a request there is no per-frame
    /// work at all. It never writes game state, never skips game code and never throws into the game, so steering,
    /// force feedback, telemetry and performance behave exactly as without it. See docs/COURSE-ROUTE-CAPTURE.md.
    /// </summary>
    internal static class CourseRouteDump
    {
        private static readonly FieldInfo StartField = AccessTools.Field(typeof(OutOfBoundsManager), "StartingWaypointIndex");
        private static readonly FieldInfo LastField = AccessTools.Field(typeof(OutOfBoundsManager), "LastWaypointIndex");
        private static CourseRouteCapture _capture;
        private static string _gameHash;
        private static long _ticks;
        private static float _lastFixedTime = float.NaN;
        private static int _finished;

        internal static string Status => _capture == null ? "off" : $"{_capture.State} rows={_capture.Rows} skipped={_capture.SkippedRows}";

        /// <summary>At load, after the mod's own patches: claims a request if one exists and only then patches.</summary>
        internal static void Initialize(string modPath, Harmony harmony)
        {
            try
            {
                string gameAssembly = Path.Combine(Application.dataPath, "Managed", "Assembly-CSharp.dll");
                // Hashed only when a request exists, once, off the physics thread; the route reuses it.
                var request = CourseRouteRequest.Consume(Path.Combine(modPath, CourseRouteRequest.FileName), DateTimeOffset.UtcNow,
                    () => _gameHash = NativeDiagnostics.FileHash(gameAssembly));
                if (request == null) return;
                _capture = new CourseRouteCapture(request.Id, request.MaxSeconds);
                harmony.Patch(AccessTools.Method(typeof(CarDynamics), "FixedUpdate"),
                    postfix: new HarmonyMethod(typeof(CourseRouteDump), nameof(Postfix)));
                ModLog.Info($"Course route capture {request.Id:N} armed for one stage: read-only, private, at most {request.MaxSeconds} s");
            }
            catch (Exception ex)
            {
                _capture = null;
                ModLog.Error("Course route request rejected; no capture this launch: " + ex.Message);
            }
        }

        /// <summary>Mod unload: completes a sampling capture and writes it on this thread.</summary>
        internal static void Stop()
        {
            var capture = _capture;
            if (capture == null || capture.Terminal) return;
            capture.Complete("mod unloaded");
            Finish(capture, synchronous: true);
        }

        private static void Postfix()
        {
            var capture = _capture;
            if (capture == null || capture.Terminal) return;
            // One row per physics step, whichever CarDynamics instance runs first.
            if (Time.fixedTime == _lastFixedTime) return;
            _lastFixedTime = Time.fixedTime;
            try
            {
                var manager = GameState.ExistingManager;
                if (manager == null) return;
                var status = manager.status;
                if (capture.State == CourseRouteState.WaitingForRoute)
                {
                    if (status != EventStatusEnums.EventStatus.UNDERWAY) return;
                    capture.SetRoute(ReadRoute(manager));
                    if (capture.Terminal) { Finish(capture, synchronous: false); return; }
                }
                var bounds = manager.outOfBoundsManager;
                var car = manager.playerManager?.PlayerObject;
                if (bounds == null || car == null) throw new InvalidOperationException("stage objects are gone");
                var p = car.transform.position;
                capture.AddSample(++_ticks, Time.fixedTime, new CoursePoint(p.x, p.y, p.z), bounds.GetCurrentWaypointIndex(), status.ToString());
                if (status == EventStatusEnums.EventStatus.FINISHING_STAGE_ANIMATION || status == EventStatusEnums.EventStatus.FINISHED ||
                    status == EventStatusEnums.EventStatus.FINISHED_SCREEN || status == EventStatusEnums.EventStatus.REPLAY)
                    capture.Complete("stage " + status);
                if (capture.Terminal) Finish(capture, synchronous: false);
            }
            catch (Exception ex)
            {
                capture.Fault(ex.GetType().Name + ": " + ex.Message);
                Finish(capture, synchronous: false);
            }
        }

        private static CourseRouteFacts ReadRoute(EventManager manager)
        {
            var bounds = manager.outOfBoundsManager ?? throw new InvalidOperationException("no OutOfBoundsManager");
            var list = bounds.GetWaypointList() ?? throw new InvalidOperationException("no waypoint list");
            // Read at most one point past the limit so an oversized route is refused, not truncated or read unbounded.
            var points = new List<CoursePoint>(Math.Min(list.Count, CourseRouteCapture.MaxWaypoints + 1));
            for (int i = 0; i < list.Count && i <= CourseRouteCapture.MaxWaypoints; i++)
                points.Add(new CoursePoint(list[i].x, list[i].y, list[i].z));
            var stage = GameModeManager.GetRallyDataCurrentGameMode()?.GetCurrentStage() ?? throw new InvalidOperationException("no current stage");
            return new CourseRouteFacts
            {
                GameAssemblySha256 = _gameHash,
                GameMode = GameModeManager.GameMode.ToString(),
                Area = stage.Area.ToString(),
                StageName = stage.Name,
                SceneConstant = stage.SceneConstant,
                StageForward = stage.IsForwardStage,
                RouteForward = bounds.IsForwardStage(),
                StageDistance = stage.Distance,
                Waypoints = points,
                StartIndex = StartField?.GetValue(bounds) as int?,
                LastIndex = LastField?.GetValue(bounds) as int?
            };
        }

        private static void Finish(CourseRouteCapture capture, bool synchronous)
        {
            if (Interlocked.Exchange(ref _finished, 1) != 0) return;
            if (capture.State != CourseRouteState.Completed && capture.State != CourseRouteState.LimitReached || capture.Rows == 0)
            {
                ModLog.Warning($"Course route capture {capture.RequestId:N} ended {capture.State}: {capture.Reason}; nothing written");
                return;
            }
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dbce", "CourseExtraction");
            void Write()
            {
                try
                {
                    string folder = capture.Write(root, DateTimeOffset.UtcNow);
                    ModLog.Info($"Course route capture {capture.RequestId:N} {capture.State} ({capture.Reason}): {capture.Rows} rows, " +
                                $"{capture.SkippedRows} skipped, {capture.Route?.Waypoints.Count} waypoints -> {folder}");
                }
                catch (Exception ex) { ModLog.Error($"Course route capture {capture.RequestId:N} not written: {ex.Message}"); }
            }
            // The capture is terminal, so nothing mutates it while the file work leaves the physics thread.
            if (synchronous) Write(); else Task.Run(Write);
        }
    }
}
