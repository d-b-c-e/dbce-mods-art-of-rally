using UnityEngine;

namespace ArtOfSimRally.Testing
{
    /// <summary>Owns the same kinematic body mode used by the game's built-in replay.
    /// Recorded poses are authoritative; this is visual playback, not a physics test.</summary>
    internal sealed class VehiclePlayback
    {
        private Rigidbody body;
        private bool wasKinematic;
        private RigidbodyInterpolation interpolation;
        private Vector3 commandedPosition;
        private bool commanded;
        private Vector3 lastVelocity, lastAngularVelocity;
        internal int Applied { get; private set; }
        internal bool Active => body != null;
        internal float MaxApplicationError { get; private set; }
        internal void Apply(CarController car, SessionTape.CarRow row)
        {
            var next = car.GetComponent<Rigidbody>();
            if (body != next)
            {
                Release(); body = next; wasKinematic = body.isKinematic; interpolation = body.interpolation;
                body.position = row.Position; body.rotation = row.Rotation;
            }
            if (commanded)
            {
                float error = Vector3.Distance(body.position, commandedPosition);
                MaxApplicationError = Mathf.Max(MaxApplicationError, error);
                if (error > 0.25f) throw new System.InvalidOperationException("Another system moved the playback vehicle by " + error + " m");
            }
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // The stock replay also supplies velocity for sound, particles and speed display.
            body.velocity = lastVelocity = row.Velocity;
            body.angularVelocity = lastAngularVelocity = row.AngularVelocity;
            body.MovePosition(row.Position); body.MoveRotation(row.Rotation);
            commandedPosition = row.Position; commanded = true;
            car.steering = row.ResolvedSteer; car.throttle = row.ResolvedThrottle; car.brake = row.ResolvedBrake;
            ApplyDrivetrain(car.GetComponent<Drivetrain>(), row);
            Applied++;
        }
        internal static void ApplyDrivetrain(Drivetrain drive, SessionTape.CarRow row)
        {
            drive.gear = row.Gear; drive.nextGear = row.Gear; drive.changingGear = false;
            drive.velo = Mathf.Abs((Quaternion.Inverse(row.Rotation) * row.Velocity).z);
            drive.wheelTireVelo = drive.velo;
            drive.throttle = row.ResolvedThrottle;
            // Old tapes did not capture RPM. Estimate only the presentation for those tapes.
            float rpm = row.Rpm >= 0 ? row.Rpm : Mathf.Clamp(drive.velo * 60f / (2f * Mathf.PI * 0.3f) *
                Mathf.Abs(drive.gearRatios[row.Gear] * drive.finalDriveRatio), drive.minRPM, drive.maxRPM);
            drive.rpm = rpm; drive.engineAngularVelo = rpm * (2f * Mathf.PI / 60f);
        }
        internal void Release()
        {
            if (body != null)
            {
                body.isKinematic = wasKinematic;
                body.interpolation = interpolation;
                if (!wasKinematic) { body.velocity = lastVelocity; body.angularVelocity = lastAngularVelocity; }
            }
            body = null; commanded = false;
        }
    }
}
