using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Serialization;
using ArtOfSimRally.Mod;
using UnityEngine;
using Dbce.Wheel.Ffb;

static class Program
{
    static int assertions;
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static float F(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    static void Same(float actual, float expected, string message)
        => Check(float.IsFinite(actual) && Math.Abs(actual - expected) <= 0.000001f, message + $": {actual:R} != {expected:R}");

    static void Curve()
    {
        StandardReference();
        // The curve's outputs are pinned by docs/force-curve-vector.csv (Test-Rc's vector step).
        Check(ForceCurve.Normalised(40000, 0, 8.5f, 3, 11500, 2, false) == 0, "fade start");
        Check(ForceCurve.Normalised(40000, 0, 8.5f, 12, 11500, 2, false) == 1, "saturation");
        Same(ForceCurve.Normalised(1, 0, 0, 12, 0, 1, true), -1, "reference floor/inversion");
    }

    static void StandardReference()
    {
        var cfg = new Settings();
        Check(cfg.Strength == 50 && cfg.GainFromStrength == 1, "standard steering slider midpoint changed");
        Check(cfg.CrashStrength == 50 && cfg.LandingStrength == 5 && cfg.ShiftStrength == 5, "steering calibration altered independent effect defaults");
        // High forces must retain full range: the reference is calibrated before the output is clipped.
        Same(ForceCurve.Normalised(100000, 0, 8.5f, 60, cfg.FyReference, cfg.GainFromStrength, false), 1,
            "reference calibrated after output clipping");
    }

    static void WheelIdentity()
    {
        foreach (string invalid in new[] { "wheel|0|axis:8|0|65535", "wheel|0|button:128|0|1", "wheel|x|axis:0|0|1", "wheel|0|unknown:0|0|1", "wheel|0|axis:x|0|1", "wheel|0|axis:0|-2147483648|2147483647" })
            Check(WheelInput.Binding.Parse(invalid)==null,"invalid binding could index device memory");
        string legacy="wheel|0|axis:0|32767|65535";
        Check(WheelInput.Binding.Parse(legacy)?.ToString()==legacy,"legacy input binding changed");
        var first=Guid.NewGuid(); var second=Guid.NewGuid();
        var snapshot=new[] {
            new WheelFfbNative.DeviceInfo { Index=0,Name="same wheel",ForceFeedback=true,InstanceGuid=second },
            new WheelFfbNative.DeviceInfo { Index=1,Name="same wheel",ForceFeedback=true,InstanceGuid=first }
        };
        typeof(FfbNative).GetField("_devices",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.SetValue(null,snapshot);
        var settings=new Settings { PreferredDeviceIndex=0,PreferredDevice="same wheel",PreferredDeviceGuid=first.ToString() };
        Check(FfbNative.SelectedPosition(settings)==1,"GUID followed stale enumeration index");
        Check(FfbNative.DeviceGuid(1)==first.ToString(),"picker saved wrong GUID");
        settings.PreferredDeviceGuid=Guid.NewGuid().ToString();
        Check(FfbNative.SelectedPosition(settings)==-1,"missing wheel fell back in picker");
        settings.PreferredDeviceGuid="invalid";
        Check(FfbNative.SelectedPosition(settings)==-1,"invalid GUID fell back in picker");
        settings.PreferredDeviceGuid="";
        Check(FfbNative.SelectedPosition(settings)==-1,"legacy index shown as a verified device");
    }

    static void Saves()
    {
        var save = new DeferredSave(); int writes = 0;
        Func<bool> ok = () => { writes++; return true; };
        Check(!save.Flush(0, false, false, ok) && writes == 0, "clean save wrote");
        save.MarkDirty();
        Check(!save.Flush(0, true, false, ok) && writes == 0 && save.Pending, "driving save wrote");
        Check(!save.Flush(0, false, false, () => { writes++; return false; }) && save.Pending, "failure lost dirty state");
        save.MarkDirty(); // another calibration extension must not bypass backoff
        Check(!save.Flush(4.99, false, false, ok) && writes == 1, "failed save retried too soon");
        Check(save.Flush(5, false, false, ok) && !save.Pending && writes == 2, "idle retry failed");
        save.MarkDirty();
        Check(!save.Flush(10, false, false, () => throw new IOException()) && save.Pending, "exception lost dirty state");
        Check(save.Flush(10.1, false, true, ok) && !save.Pending, "shutdown retry skipped");
        Check(!save.Flush(20, false, true, ok) && writes == 3, "duplicate shutdown saved twice");
        // Real disk failure through the production writer, with the actual
        // Settings type and UMM-compatible XML, not a successful-save mock.
        string directory=Path.GetFullPath(Path.Combine("results","settings-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"Settings.xml");
        var original=new Settings { Strength=15, Smoothing=.5f, BonnetHeight=1.234f };
        SettingsPersistence.Write(original,path);
        string before=File.ReadAllText(path);
        Check(new Settings().LandingEffectsEnabled && new Settings().LandingStrength==5f,"new settings use the default landing vibration");
        Check(!new Settings().CrashEffectsEnabled && new Settings().CrashStrength==50f,"experimental crash effect must default off at strength50");
        Check(!new Settings().ShiftEffectsEnabled && new Settings().ShiftStrength==5f,"optional shift cue must default off at strength5");
        var serializer=new XmlSerializer(typeof(Settings));
        using(var legacyXml=new StringReader("<Settings><Strength>15</Strength></Settings>"))
        {
            var legacy=(Settings)serializer.Deserialize(legacyXml)!;
            Check(legacy.LandingEffectsEnabled && legacy.LandingStrength==5f && legacy.Strength==15,"pre-feature settings inherit landing defaults without altering steering");
            Check(!legacy.CrashEffectsEnabled && legacy.CrashStrength==50f,"upgrading silently enabled experimental crashes or lost new default");
            Check(!legacy.ShiftEffectsEnabled && legacy.ShiftStrength==5f,"upgrading silently enabled shift force");
        }
        using(var optedOutXml=new StringReader("<Settings><LandingEffectsEnabled>false</LandingEffectsEnabled><LandingStrength>3.5</LandingStrength></Settings>"))
        {
            var optedOut=(Settings)serializer.Deserialize(optedOutXml)!;
            Check(!optedOut.LandingEffectsEnabled && optedOut.LandingStrength==3.5f,"saved opt-out and strength survive upgrade");
        }
        foreach(string savedStrength in new[]{"0","19.52381","40","100"})
        using(var savedCrash=new StringReader("<Settings><CrashEffectsEnabled>true</CrashEffectsEnabled><CrashStrength>"+savedStrength+"</CrashStrength></Settings>"))
        {
            var saved=(Settings)serializer.Deserialize(savedCrash)!;
            Check(saved.CrashEffectsEnabled && saved.CrashStrength==float.Parse(savedStrength,System.Globalization.CultureInfo.InvariantCulture),"new default overwrote a saved crash tune");
        }
        var changed=new Settings { Strength=26, Smoothing=.2f, BonnetHeight=2.345f, LandingEffectsEnabled=true, LandingStrength=40f, CrashEffectsEnabled=true, CrashStrength=100f, ShiftEffectsEnabled=true, ShiftStrength=13f };
        var pending=new DeferredSave(); pending.MarkDirty();
        using(var locked=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Check(!pending.Flush(0,false,false,()=>{ SettingsPersistence.Write(changed,path); return true; }) && pending.Pending,"locked file reported success");
        Check(File.ReadAllText(path)==before,"failed save damaged prior settings");
        Check(Directory.GetFiles(directory,"*.tmp").Length==0,"failed save left temporary file");
        Check(pending.Flush(5,false,false,()=>{ SettingsPersistence.Write(changed,path); return true; }) && !pending.Pending,"disk retry did not recover");
        using(var input=File.OpenRead(path))
        {
            var restored=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(input)!;
            Check(restored.Strength==26 && restored.Smoothing==.2f && restored.BonnetHeight==2.345f,"UMM-compatible settings roundtrip changed values");
            Check(restored.LandingEffectsEnabled && restored.LandingStrength==40f,"extended landing settings did not persist");
            Check(restored.CrashEffectsEnabled && restored.CrashStrength==100f,"extended crash settings did not persist");
            Check(restored.ShiftEffectsEnabled && restored.ShiftStrength==13f,"shift choice did not persist");
        }
    }

    static void Native(string path)
    {
        string source = Path.GetFullPath(path);
        string directory = Path.GetFullPath(Path.Combine("results", "native-alias-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        string alias = Path.Combine(directory, "UnityForceFeedback.dll");
        File.Copy(source, alias);
        Check(NativeDiagnostics.Describe("UnityForceFeedback.dll").Contains("not loaded"), "inspection loaded native DLL");
        Check(WheelFfbNative.Load(directory, "UnityForceFeedback.dll"), WheelFfbNative.LastError);
        Check(!WheelFfbNative.Ready, "binding unexpectedly acquired a device");
        // Reviewed native-only 0.9.1 candidate retains every v0.15.0 wrapper export.
        // Load and inspect only: this regression must never acquire a device.
        Check(WheelFfbNative.Version == 901, "native component changed; review candidate ABI");
        Check(WheelFfbNative.SupportsPeriodicBursts, "finite periodic burst API is unavailable");
        Check(WheelFfbNative.SupportsShapedPeriodicBursts, "shaped finite burst API is unavailable");
        Check(WheelFfbNative.SupportsConstantBursts, "finite constant burst API is unavailable");
        string description = NativeDiagnostics.Describe("UnityForceFeedback.dll");
        Check(description.Contains("0.9.1"), "native version decoding");
        Check(description.Contains(NativeDiagnostics.FileHash(source)), "mapped DLL hash");
        Check(description.Contains(alias), "mapped DLL path");
        Check(NativeDiagnostics.Describe("kernel32.dll").Contains("export missing"), "missing-export fallback");
        IntPtr module = NativeLibrary.Load(alias); // Only a module reference; never InitDirectInput.
        try
        {
            var fields = typeof(WheelFfbNative).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType)).ToArray();
            Check(fields.Length >= 38, "native binding scan is empty/incomplete");
            foreach (var field in fields)
            {
                string export = field.Name switch {
                    "_createBurst" => "CreatePeriodicBurst", "_playBurst" => "PlayPeriodicBurst",
                    "_playShapedBurst" => "PlayShapedPeriodicBurst", "_stopBurst" => "StopPeriodicBurst",
                    "_createConstantBurst" => "CreateConstantBurst", "_playConstantBurst" => "PlayConstantBurst",
                    "_stopConstantBurst" => "StopConstantBurst", "_releaseConstantBursts" => "ReleaseConstantBursts",
                    _ => field.Name };
                Check(field.GetValue(null)!=null && NativeLibrary.TryGetExport(module, export, out _), "Missing binding/export " + export);
            }
        }
        finally { NativeLibrary.Free(module); } // Toolkit keeps its own successful reference.
    }

    static object ToolkitComparison()
    {
        // Exercise the PINNED artifact, not whatever the upstream task is editing.
        // Legacy SimLite remains different; adoption uses explicitly versioned AxleForceCurve.
        var profile = ForceProfile.SimLite();
        profile.Shaper.Invert = true; // Toolkit lateral-force sign is opposite ours.
        var model = new ForceModel(profile.Model);
        var inputs = new ForceInputs { HasFrontLateralForce=true, HasSlip=true,
            FrontLateralForce=23000, FrontSlipAngleDeg=0, IdealSlipAngleDeg=8.5f, SpeedMps=40/3.6f };
        float current = ForceCurve.Smooth(0, ForceCurve.Normalised(23000,0,8.5f,40,11500,1,false),.2f);
        float toolkit = profile.Shaper.Shape(model.Compute(inputs,.02f),40,.02f);
        Same(current,.8f,"current clamp before EMA");
        Same(toolkit,1f,"pinned toolkit clamp after EMA changed; revisit adoption audit");
        inputs.FrontLateralForce=0;
        float nextCurrent=ForceCurve.Smooth(current,0,.2f);
        float nextToolkit=profile.Shaper.Shape(model.Compute(inputs,.02f),40,.02f);
        Same(nextCurrent,.16f,"current release tail"); Same(nextToolkit,.32f,"toolkit release tail");
        return new { pipeline="AxleForceCurve@" + AxleForceCurve.CompatibilityVersion, scenario="23000 N then zero, 40 km/h, strength 50, smoothing 0.2", current, toolkit, nextCurrent, nextToolkit,
            conclusion="adopted compatibility pipeline preserves the baseline; simlite@2 remains a different tune" };
    }

    static int Main(string[] args)
    {
        try
        {
            object? detail = null;
            if (args.Length == 2 && args[0] == "--native") { Curve(); WheelIdentity(); Saves(); Native(args[1]); detail=ToolkitComparison(); }
            else { Curve(); WheelIdentity(); Saves(); detail=ToolkitComparison(); }
            Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions, detail })); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
