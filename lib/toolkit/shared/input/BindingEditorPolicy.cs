// Vendored presentation policy only. No device, capture, profile, file or force calls.
#nullable enable
using System;
using System.Collections.Generic;

#if DBCE_INPUT_NET35
using DeviceRows = System.Collections.Generic.IList<Dbce.Wheel.Input.SharedBindingDeviceView>;
#else
using DeviceRows = System.Collections.Generic.IReadOnlyList<Dbce.Wheel.Input.SharedBindingDeviceView>;
#endif

namespace Dbce.Wheel.Input
{
    public enum SharedBindingSelectionMode { Saved, Explicit, Unavailable }

    /// <summary>An explicit capture target never becomes Saved or another device because it disappears.
    /// Reconcile once before drawing, using the current host and its device snapshot. Host replacement
    /// invalidates an explicit choice even when the same GUID is present. Only a user choice resolves it.</summary>
    public sealed class SharedBindingDeviceSelection
    {
        private object? host;
        public bool IsInitialized => host != null;
        public SharedBindingSelectionMode Mode { get; private set; } = SharedBindingSelectionMode.Saved;
        public string? InstanceGuid { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public bool CanBind => IsInitialized && (Mode == SharedBindingSelectionMode.Saved ||
            Mode == SharedBindingSelectionMode.Explicit && SelectedIndex >= 0);
        public string UnavailableReason => Mode == SharedBindingSelectionMode.Unavailable
            ? "The input connection changed. Choose a device again to bind controls."
            : "";

        public void Reconcile(object currentHost, DeviceRows devices)
        {
            if (currentHost == null) throw new ArgumentNullException(nameof(currentHost));
            if (devices == null) throw new ArgumentNullException(nameof(devices));
            if (host != null && !ReferenceEquals(host, currentHost) && Mode != SharedBindingSelectionMode.Saved)
                Mode = SharedBindingSelectionMode.Unavailable;
            host = currentHost;
            SelectedIndex = Mode == SharedBindingSelectionMode.Explicit ? Find(devices, InstanceGuid) : -1;
            if (Mode == SharedBindingSelectionMode.Explicit && SelectedIndex < 0)
                Mode = SharedBindingSelectionMode.Unavailable;
        }

        /// <summary>Call only for a deliberate Saved-device choice; the host retains its existing default semantics.</summary>
        public void SelectSaved()
        {
            Mode = SharedBindingSelectionMode.Saved;
            InstanceGuid = null;
            SelectedIndex = -1;
        }

        /// <summary>Call only for a deliberate choice from the current reconciled host's snapshot.</summary>
        public bool SelectDevice(string instanceGuid, DeviceRows devices)
        {
            if (devices == null) throw new ArgumentNullException(nameof(devices));
            int index = Find(devices, instanceGuid);
            if (!IsInitialized || index < 0) return false;
            InstanceGuid = devices[index].InstanceGuid;
            SelectedIndex = index;
            Mode = SharedBindingSelectionMode.Explicit;
            return true;
        }

        private static int Find(DeviceRows devices, string? instanceGuid)
        {
            if (string.IsNullOrEmpty(instanceGuid)) return -1;
            for (int i = 0; i < devices.Count; i++)
                if (string.Equals(devices[i].InstanceGuid, instanceGuid, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }

    public static class SharedBindingUiPolicy
    {
        /// <summary>The current contract's Note is an unavailable-action or profile-mode reason.
        /// Preserve it visibly; do not offer edits which this consumer cannot deliver.</summary>
        public static bool CanEditRow(SharedBindingAccess access, string? capturingAction, string? note) =>
            access == SharedBindingAccess.Editable && capturingAction == null && string.IsNullOrEmpty(note);
        public static bool CanEditRow(SharedBindingAccess access, string? capturingAction, string? note, SharedBindingApplicability applicability) =>
            applicability == SharedBindingApplicability.Supported && CanEditRow(access, capturingAction, note);
    }
}
