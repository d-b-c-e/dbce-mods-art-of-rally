using System;
using System.Collections.Generic;

namespace ArtOfSimRally.Mod.Shim
{
    // RawInput's public identity getters intentionally return empty when the
    // joystick is disabled. Retain an explicitly admitted identity witness only
    // while its exact controller/extension/connection lifetime still holds.
    // Returned rows are for joining only; raw diagnostic observations stay raw.
    internal sealed class ProbeIdentityLease
    {
        private sealed class References : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
        private readonly Dictionary<object, JoystickObservation> before = new Dictionary<object, JoystickObservation>(new References());
        private readonly HashSet<Guid> owned;
        private readonly long epoch;
        private bool invalid;
        internal ProbeIdentityLease(JoystickObservation[] rows, Guid[] owned, long epoch)
        {
            if (rows == null || owned == null || rows.Length == 0) throw new ArgumentException("lease inventory required");
            this.owned = new HashSet<Guid>(owned); this.epoch = epoch;
            if (this.owned.Count == 0 || this.owned.Contains(Guid.Empty)) throw new ArgumentException("lease owned identities required");
            foreach (var row in rows)
            {
                Guid instance, product;
                if (row == null || row.Token == null || row.ExtensionToken == null || !row.Connected || !row.Enabled ||
                    !Guid.TryParse(row.Instance, out instance) || instance == Guid.Empty || !Guid.TryParse(row.ProductGuid, out product) || product == Guid.Empty ||
                    String.IsNullOrEmpty(row.InterfacePath) || !String.IsNullOrEmpty(row.IdentityPathError) || before.ContainsKey(row.Token))
                    throw new InvalidOperationException("lease requires unique connected objects with live identity before disabling");
                before.Add(row.Token, new JoystickObservation { Token = row.Token, ExtensionToken = row.ExtensionToken, Instance = instance.ToString("D"),
                    ProductGuid = product.ToString("D"), InterfacePath = row.InterfacePath, Enabled = row.Enabled, Connected = true });
            }
        }
        internal JoystickObservation[] Verify(JoystickObservation[] current, long epoch, Func<object, bool> ownsDisabled)
        {
            try
            {
                if (invalid || this.epoch != epoch || current == null || current.Length != before.Count || ownsDisabled == null)
                    throw new InvalidOperationException("identity lease invalidated by connection epoch or inventory change");
                var seen = new HashSet<object>(new References()); var result = new JoystickObservation[current.Length];
                for (int i = 0; i < current.Length; i++)
                {
                    var now = current[i]; JoystickObservation prior;
                    if (now == null || now.Token == null || !seen.Add(now.Token) || !before.TryGetValue(now.Token, out prior) ||
                        !ReferenceEquals(now.ExtensionToken, prior.ExtensionToken) || !now.Connected || now.Instance != prior.Instance || !String.IsNullOrEmpty(now.IdentityPathError))
                        throw new InvalidOperationException("identity lease controller, extension, GUID or connected state changed" +
                            (now == null || String.IsNullOrEmpty(now.IdentityPathError) ? "" : " (" + now.IdentityPathError + ")"));
                    Guid product;
                    if (!Guid.TryParse(now.ProductGuid, out product) || now.InterfacePath == null)
                        throw new InvalidOperationException("identity getter returned an unexpected unavailable representation");
                    bool hasProduct = product != Guid.Empty;
                    bool hasPath = !String.IsNullOrEmpty(now.InterfacePath);
                    if (hasPath != hasProduct) throw new InvalidOperationException("identity getters returned only a partial identity");
                    // Even when disabled, any exposed identity must agree. Do
                    // not hide a partial/mismatched new interface behind a cache.
                    if ((hasPath && !String.Equals(now.InterfacePath, prior.InterfacePath, StringComparison.OrdinalIgnoreCase)) ||
                        (hasProduct && product != Guid.Parse(prior.ProductGuid)))
                        throw new InvalidOperationException("identity lease live path/product changed");
                    if (now.Enabled)
                    {
                        if (!hasPath || !hasProduct) throw new InvalidOperationException("enabled joystick identity unavailable");
                        result[i] = now;
                    }
                    else
                    {
                        if (!owned.Contains(Guid.Parse(now.Instance)) || !ownsDisabled(now.Token))
                            throw new InvalidOperationException("identity hidden outside this lease's own disabled value");
                        result[i] = new JoystickObservation { Token = now.Token, ExtensionToken = now.ExtensionToken, Instance = now.Instance,
                            Enabled = false, Connected = true, InterfacePath = prior.InterfacePath, ProductGuid = prior.ProductGuid };
                    }
                }
                return result;
            }
            catch { invalid = true; throw; } // no reconnect or later frame revives a failed lease
        }
    }
}
