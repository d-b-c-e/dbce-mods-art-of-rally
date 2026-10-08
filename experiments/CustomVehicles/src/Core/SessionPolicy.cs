namespace RallyCustomVehicles
{
    // Kept independent of Unity so lifecycle/taint behavior can be exercised offline.
    public sealed class SessionPolicy
    {
        public bool UploadGuardReady { get; set; }
        public bool LocalGuardReady { get; set; }
        public bool RuntimeQualified { get; set; }
        public bool ExperimentEnabled { get; set; }
        public bool PhysicsUsed { get; private set; }
        public bool CanApply(bool isFreeRoam, bool selectedHasPhysics) => RuntimeQualified && UploadGuardReady && LocalGuardReady && ExperimentEnabled && isFreeRoam && selectedHasPhysics;
        public void MarkPhysicsUsed() { PhysicsUsed = true; }
        public bool CanUpload => !PhysicsUsed;
        public bool CanUnload => !PhysicsUsed;
        public string Status => PhysicsUsed ? "Custom physics session: scores, ghosts and progression disabled until restart." : "Scores and progression: unchanged";
    }
}
