using System.Linq;

namespace ArtOfSimRally.Mod
{
    internal static class ChasePoseStore
    {
        internal static bool Finite(ChaseCameraPose pose) => pose != null &&
            new[] { pose.Side, pose.Height, pose.Forward, pose.Pitch }.All(x => !float.IsNaN(x) && !float.IsInfinity(x));

        internal static ChaseCameraPose Read(Settings cfg, string car, int view, bool realistic)
        {
            var saved = cfg.ChaseCameraPoses.LastOrDefault(p => p != null && p.Car == car && p.View == view && p.Realistic == realistic);
            return Finite(saved) ? new ChaseCameraPose { Car=car, View=view, Realistic=realistic,
                Side=saved.Side, Height=saved.Height, Forward=saved.Forward, Pitch=saved.Pitch }
                : new ChaseCameraPose { Car=car, View=view, Realistic=realistic };
        }

        internal static void Write(Settings cfg, ChaseCameraPose pose)
        {
            if (string.IsNullOrWhiteSpace(pose.Car) || pose.View < 0 || pose.View > 31 || !Finite(pose)) return;
            cfg.ChaseCameraPoses.RemoveAll(p => p != null && p.Car == pose.Car && p.View == pose.View && p.Realistic == pose.Realistic);
            if (pose.Side != 0 || pose.Height != 0 || pose.Forward != 0 || pose.Pitch != 0)
                cfg.ChaseCameraPoses.Add(pose);
        }
    }
}
