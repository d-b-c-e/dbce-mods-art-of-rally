using System;
using System.Reflection;

namespace ArtOfSimRally.Testing
{
    /// <summary>The route's wait for the mod's shared session (sana 45326). Every policy that waits for it (native, or
    /// shared with holdForShared=1) binds the mod's ShimInput.Consumer and reads its Started flag; no other policy binds
    /// anything. Unity-free, so the production binding path is exercised by tests/Recorder with stub types. 8989f63 passed
    /// the delegate for holdForShared while only native evidence bound the consumer: a frame-0 NullReferenceException.</summary>
    internal static class SharedReadiness
    {
        internal const string ShimInputType = "ArtOfSimRally.Mod.Shim.ShimInput";
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>The delegate the capture driver holds the route on, or null when the policy does not wait.</summary>
        internal static Func<bool> For(CapturePolicy policy, Func<string, Type> resolve)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            return policy.WaitsForShared ? Bind(resolve) : null;
        }

        internal static Func<bool> Bind(Func<string, Type> resolve)
        {
            var shim = resolve(ShimInputType) ?? throw new MissingMemberException("ShimInput");
            var field = shim.GetField("Consumer", Static) ?? throw new MissingFieldException("ShimInput.Consumer");
            var consumer = field.GetValue(null) ?? throw new InvalidOperationException("ShimInput.Consumer is null");
            var started = consumer.GetType().GetProperty("Started", Instance);
            if (started == null || started.PropertyType != typeof(bool)) throw new MissingMemberException("ShimAxisConsumer.Started");
            return () => (bool)started.GetValue(consumer, null);
        }
    }
}
