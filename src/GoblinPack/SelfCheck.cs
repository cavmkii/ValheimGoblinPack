using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GoblinPack
{
    /// <summary>
    /// The mod compiles against publicized game assemblies (everything public), but the game's Mono
    /// runtime still enforces method visibility when it JIT-compiles a caller. A private game method
    /// therefore only fails the first time the code path that calls it runs, which may be rare
    /// (a theft, a fight). Compiling every method up front surfaces all of those at startup.
    /// </summary>
    internal static class SelfCheck
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        public static void Run()
        {
            int checkedCount = 0;
            int failures = 0;
            foreach (Type type in typeof(SelfCheck).Assembly.GetTypes())
            {
                if (type.IsGenericTypeDefinition)
                {
                    continue;
                }
                foreach (MethodBase method in Methods(type))
                {
                    if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
                    {
                        continue;
                    }
                    checkedCount++;
                    try
                    {
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    }
                    catch (Exception e)
                    {
                        failures++;
                        Exception root = e.InnerException ?? e;
                        GoblinPackPlugin.Log.LogError($"Self-check: {type.FullName}.{method.Name} can't run on this game version: {root.GetType().Name}: {root.Message}");
                    }
                }
            }

            if (failures == 0)
            {
                GoblinPackPlugin.Log.LogInfo($"Self-check: compiled {checkedCount} methods, no access errors.");
            }
            else
            {
                GoblinPackPlugin.Log.LogError($"Self-check: {failures} of {checkedCount} methods reference game members this version doesn't allow. Please report the lines above.");
            }
        }

        private static MethodBase[] Methods(Type type)
        {
            MethodInfo[] methods = type.GetMethods(All);
            ConstructorInfo[] ctors = type.GetConstructors(All);
            var result = new MethodBase[methods.Length + ctors.Length];
            methods.CopyTo(result, 0);
            ctors.CopyTo(result, methods.Length);
            return result;
        }
    }
}
