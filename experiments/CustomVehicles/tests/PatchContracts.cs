using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class PatchContracts
{
    internal static void Check(string pluginPath, string managedPath)
    {
        ResolveEventHandler resolver = (sender, args) =>
        {
            string file = new AssemblyName(args.Name).Name + ".dll";
            foreach (string directory in new[] { managedPath, Path.Combine(managedPath, "UnityModManager") })
            {
                string path = Path.Combine(directory, file);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += resolver;
        try
        {
            var plugin = Assembly.LoadFrom(pluginPath); int count = 0;
            foreach (var type in plugin.GetTypes())
            {
                var attribute = type.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch");
                if (attribute == null) continue;
                var selector = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.GetCustomAttributesData().Any(a => a.AttributeType.Name == "HarmonyTargetMethods"));
                if (selector != null)
                {
                    var targets = ((IEnumerable)selector.Invoke(null, null)).Cast<MethodBase>().ToArray();
                    if (targets.Length == 0 || targets.Any(m => m == null)) throw new Exception("Missing dynamic patch targets: " + type);
                    foreach(var target in targets) CheckBindings(type,target);
                    count += targets.Length; continue;
                }
                var args = attribute.ConstructorArguments;
                Type declaring = (Type)args[0].Value;
                string methodName = (string)args[1].Value;
                var methods = declaring.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name == methodName);
                if (args.Count > 2)
                {
                    var parameters = ((IEnumerable<CustomAttributeTypedArgument>)args[2].Value).Select(a => (Type)a.Value).ToArray();
                    methods = methods.Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
                }
                if (methods.Count() != 1) throw new Exception("Missing or ambiguous patch target: " + declaring.Name + "." + methodName);
                CheckBindings(type,methods.Single());
                count++;
            }
            if (count < 38) throw new Exception("Expected camera/menu/save/physics/result hook coverage.");
            Console.WriteLine("PASS: " + count + " Harmony targets resolved against installed game metadata (no game or Unity runtime execution).");
            CheckGuards(plugin);
        }
        finally { AppDomain.CurrentDomain.AssemblyResolve -= resolver; }
    }
    private static void CheckBindings(Type patch,MethodBase original)
    {
        foreach(var method in patch.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Where(m=>m.GetCustomAttributesData().Any(a=>a.AttributeType.Name=="HarmonyPrefix" || a.AttributeType.Name=="HarmonyPostfix")))
            foreach(var p in method.GetParameters())
            {
                Type expected=null;
                if(p.Name=="__originalMethod" || p.Name=="__args") continue;
                if(p.Name=="__instance") expected=original.DeclaringType;
                else if(p.Name=="__result") expected=((MethodInfo)original).ReturnType;
                else expected=original.GetParameters().FirstOrDefault(x=>x.Name==p.Name)?.ParameterType;
                if(expected==null) throw new Exception("Unknown injected parameter "+patch.Name+"."+p.Name+" for "+original);
                if(expected.IsByRef) expected=expected.GetElementType();
                var actual=p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
                if(actual!=expected) throw new Exception("Injected parameter type mismatch: "+method+" -> "+original);
            }
    }
    private static void CheckGuards(Assembly plugin)
    {
        var main=plugin.GetType("RallyCustomVehicles.Main");
        object policy=main.GetField("Policy",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        if((bool)policy.GetType().GetProperty("RuntimeQualified").GetValue(policy)) throw new Exception("Offline candidate must keep physical writes locked.");
        var online=plugin.GetType("RallyCustomVehicles.UploadGuard"); var local=plugin.GetType("RallyCustomVehicles.LocalResultGuard");
        var targets=((IEnumerable)online.GetMethod("Targets",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null)).Cast<MethodBase>().ToArray();
        var before=online.GetMethod("Before",BindingFlags.NonPublic|BindingFlags.Static);
        var beforeLocal=local.GetMethod("Before",BindingFlags.NonPublic|BindingFlags.Static);
        int failures=0; Action<string> receive=reason=> {if(string.IsNullOrWhiteSpace(reason)) throw new Exception("Missing failure reason"); failures++;};
        var callbackType=targets[0].GetParameters().First(p=>p.ParameterType.Name=="PlatformAPIFailed").ParameterType;
        var callback=Delegate.CreateDelegate(callbackType,receive.Target,receive.Method);
        var args=new object[] {callback,callback};
        if(!(bool)before.Invoke(null,new object[] {targets[0],args}) || failures!=0 || !(bool)beforeLocal.Invoke(null,null)) throw new Exception("Stock session should pass through guards.");
        policy.GetType().GetMethod("MarkPhysicsUsed").Invoke(policy,null);
        foreach(var target in targets)
            if((bool)before.Invoke(null,new object[] {target,args})) throw new Exception("Tainted upload permitted.");
        if(failures!=targets.Length*2 || (bool)beforeLocal.Invoke(null,null)) throw new Exception("Tainted result must fail both callbacks and suppress local mutation.");
        Action<string> throwing=reason=> {throw new Exception("injected callback failure");};
        var faulty=Delegate.CreateDelegate(callbackType,throwing.Target,throwing.Method);
        if((bool)before.Invoke(null,new object[] {targets[0],new object[] {faulty,callback}})) throw new Exception("Callback failure permitted upload.");
        Console.WriteLine("PASS: actual upload/local prefixes, both failure callbacks, callback exception isolation and locked physical default (no native method invocation).");
    }
}
