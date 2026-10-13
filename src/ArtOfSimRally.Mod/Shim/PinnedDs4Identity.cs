using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace ArtOfSimRally.Mod.Shim
{
    // One Rewired joystick's identity for the exact path/product join: the
    // extension object the lease ties it to, its interface path and product.
    internal struct RewiredIdentity
    {
        internal readonly object Token;
        internal readonly string Path, Product;
        internal RewiredIdentity(object token, string path, string product) { Token = token; Path = path; Product = product; }
    }

    // Rewired reads a DualShock 4 through its own HID driver, which has no
    // RawInput extension, so the public API exposes no interface path for it.
    // For the reviewed Rewired_Core/Rewired_Windows pair only, this reads the
    // path the driver opened through the exact private chain observed at
    // runtime (diag-03): fields only, no getter, method or handle. The path
    // must still equal an owned reader's path exactly in ProbeIdentityMapper;
    // the product, from the public HID vendor/product getters, is the second
    // check. Any other hash, member, type or an empty path refuses by name.
    // No other controller family is read here.
    internal sealed class PinnedDs4Identity
    {
        internal const string CoreSha256 = "2DD147A1E7EA3A8ED7FECE35EA6554784ED3F039F5E291F3F0B6F877379B0655";
        internal const string WindowsSha256 = "4389BCA4D00D68B600972FCD2A0D5A21208517DF7893A6A4602E740D6691417C";
        // Types[i] declares Fields[i], whose value must be exactly Types[i + 1];
        // the last type declares PathField, a String. Core: the first two.
        internal static readonly (bool Core, string Type)[] Types =
        {
            (true, "Rewired.ControllerExtensions.DualShock4Extension"),
            (true, "Rewired.ControllerExtensions.DualShock4Extension+bCAGPoVsjuhfnJyRuyREpdPDdAUT"),
            (false, "Rewired.HID.Drivers.DualShock4Driver"),
            (false, "aTbFSaxgzEWBvObtdpuQSYzCiioT+hGVykObfUvbkhQHpTnWBQJESEppf"),
            (false, "eOjwogCoQuQOjGajnUgPmfeWSfsn"),
        };
        internal static readonly string[] Fields = { "sUHnBwREVEYFPxdqqIRaVAMCiTIW", "QYKkhsALSllcmVrwQkNAjMiAhLuHA", "oNZzuYgyerFmIKdVWnZydeacAQSD", "oNZzuYgyerFmIKdVWnZydeacAQSD" };
        internal const string PathField = "dCANkCDaitOZGMJDNGKkRJJgljXi";

        private readonly Assembly core, windows;
        private readonly Func<Assembly, string> hash;
        private readonly (bool Core, string Type)[] types;
        private readonly string[] fields;
        private readonly object gate = new object();
        private bool validated;
        private string refusal;
        private Type[] resolved;
        private FieldInfo[] links;
        private FieldInfo path;

        // One instance per loaded assembly pair: the files are hashed once, on
        // the first DualShock 4 read, never per frame.
        internal PinnedDs4Identity(Assembly core, Assembly windows) : this(core, windows, FileSha256, Types, Fields) { }
        internal PinnedDs4Identity(Assembly core, Assembly windows, Func<Assembly, string> hash, (bool Core, string Type)[] types, string[] fields)
        {
            this.core = core; this.windows = windows; this.hash = hash ?? throw new ArgumentNullException(nameof(hash));
            this.types = types; this.fields = fields;
            if (types == null || fields == null || types.Length != fields.Length + 1) throw new ArgumentException("chain shape");
        }
        internal int Validations { get; private set; }

        private static string FileSha256(Assembly assembly)
        {
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
        private static string Short(string value) => value == null || value.Length < 8 ? value : value.Substring(0, 8);

        private string Validate()
        {
            if (core == null || windows == null) return "Rewired_Core or Rewired_Windows not loaded";
            Validations++;
            string c, w;
            try { c = hash(core); w = hash(windows); }
            catch (Exception ex) { return "Rewired assembly hash unavailable: " + ex.GetType().Name; }
            if (!String.Equals(c, CoreSha256, StringComparison.OrdinalIgnoreCase)) return "Rewired_Core " + Short(c) + " is not the reviewed " + Short(CoreSha256);
            if (!String.Equals(w, WindowsSha256, StringComparison.OrdinalIgnoreCase)) return "Rewired_Windows " + Short(w) + " is not the reviewed " + Short(WindowsSha256);
            var found = new Type[types.Length];
            for (int i = 0; i < types.Length; i++)
                if ((found[i] = (types[i].Core ? core : windows).GetType(types[i].Type, false)) == null) return "type " + types[i].Type + " absent";
            var held = new FieldInfo[fields.Length];
            const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (int i = 0; i < fields.Length; i++)
                if ((held[i] = found[i].GetField(fields[i], Own)) == null) return "member " + types[i].Type + "." + fields[i] + " absent";
            var last = found[found.Length - 1].GetField(PathField, Own);
            if (last == null || last.FieldType != typeof(string)) return "member " + types[types.Length - 1].Type + "." + PathField + " absent or not a String";
            resolved = found; links = held; path = last;
            return null;
        }

        // vendorId/productId are the extension's public IHIDControllerExtension
        // getters, read only after the chain held. Throws with the refusal.
        internal RewiredIdentity Read(object extension, Func<ushort> vendorId, Func<ushort> productId)
        {
            if (extension == null) throw new ArgumentNullException(nameof(extension));
            lock (gate)
            {
                if (!validated) { refusal = Validate(); validated = true; }
                if (refusal != null) throw Refused(refusal);
            }
            object current = extension;
            if (current.GetType() != resolved[0]) throw Refused("extension " + current.GetType().FullName + " is not " + types[0].Type);
            for (int i = 0; i < links.Length; i++)
            {
                object next = links[i].GetValue(current);
                if (next == null) throw Refused(types[i].Type + "." + fields[i] + " is null");
                if (next.GetType() != resolved[i + 1]) throw Refused(fields[i] + " holds " + next.GetType().FullName + ", not the reviewed " + types[i + 1].Type);
                current = next;
            }
            var value = (string)path.GetValue(current);
            if (String.IsNullOrEmpty(value)) throw Refused("interface path empty");
            ushort vendor = vendorId(), product = productId();
            // DirectInput's product layout, as RawInput's productGuid and the
            // owned readers report it. Both zero means Rewired has none now.
            var id = vendor == 0 && product == 0 ? Guid.Empty :
                new Guid((int)((uint)product << 16 | vendor), 0, 0, 0, 0, 0x50, 0x49, 0x44, 0x56, 0x49, 0x44);
            return new RewiredIdentity(extension, value, id.ToString("D"));
        }
        private static InvalidOperationException Refused(string reason) => new InvalidOperationException("DualShock 4 identity refused: " + reason);
    }
}
