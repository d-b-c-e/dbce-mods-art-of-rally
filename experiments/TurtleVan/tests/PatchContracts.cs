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
                count++;
            }
            if (count < 9) throw new Exception("Expected camera/menu/save hook coverage.");
            Console.WriteLine("PASS: " + count + " Harmony targets resolved against installed game metadata (no game or Unity runtime execution).");
        }
        finally { AppDomain.CurrentDomain.AssemblyResolve -= resolver; }
    }
}
