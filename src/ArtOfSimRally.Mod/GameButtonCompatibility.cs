using System;
using System.IO;
using System.Security.Cryptography;

namespace ArtOfSimRally.Mod
{
    internal static class GameButtonCompatibility
    {
        // Offline-inspected game 1.5.8b / Steam build 17584229. New fixed action
        // IDs and transmission fields must not silently target a changed game.
        internal const string ExpectedAssembly = "7807A1D674C8214FE1FBD15B8EDC6E3E5E47700AB6E6C2943CF5BF234551C5E0";
        private static bool? _allowed;
        internal static bool Allowed
        {
            get
            {
                if (_allowed.HasValue) return _allowed.Value;
                _allowed = false;
                try
                {
                    using (var stream = File.OpenRead(typeof(PadManager).Assembly.Location))
                    using (var sha = SHA256.Create())
                        _allowed = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") == ExpectedAssembly;
                }
                catch (Exception e) { ModLog.Warning("Profile action compatibility could not be checked: " + e.Message); }
                ModLog.Info(_allowed.Value ? "Profile camera/menu/transmission: game build 17584229 matches the inspected action map (runtime input check separate)."
                    : "Profile camera/menu/transmission unavailable: game assembly differs from the inspected build. Native controls remain available.");
                return _allowed.Value;
            }
        }
    }
}
