using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GoblinPack
{
    /// <summary>
    /// The mod compiles against publicized game assemblies (everything public), but the game's runtime
    /// enforces real visibility on both methods and fields when a method first runs, which can be
    /// hours into play. This reads GoblinPack's own IL at startup, finds every game member it
    /// references, and checks that member's visibility in the game DLLs actually loaded, so every
    /// offending reference is listed in the log immediately.
    /// </summary>
    internal static class SelfCheck
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<short, OpCode> OpCodesByValue = BuildOpCodes();

        public static void Run()
        {
            var problems = new SortedSet<string>();
            int methods = 0;
            Type[] types;
            try
            {
                types = typeof(SelfCheck).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }

            var skipped = new List<string>();
            foreach (Type type in types)
            {
                if (type == null)
                {
                    skipped.Add("?");
                    continue;
                }
                try
                {
                    foreach (MethodBase method in Methods(type))
                    {
                        methods++;
                        CheckMethod(method, problems);
                    }
                }
                catch (Exception)
                {
                    skipped.Add(type.Name);
                }
            }
            if (skipped.Count > 0)
            {
                GoblinPackPlugin.Log.LogWarning($"Self-check: couldn't inspect {skipped.Count} type(s): {string.Join(", ", skipped.ToArray())}");
            }

            if (problems.Count == 0)
            {
                GoblinPackPlugin.Log.LogInfo($"Self-check: scanned {methods} methods, every game member they use is accessible.");
                return;
            }
            foreach (string problem in problems)
            {
                GoblinPackPlugin.Log.LogError($"Self-check: {problem}");
            }
            GoblinPackPlugin.Log.LogError($"Self-check: {problems.Count} inaccessible game member(s); those features will fail on this game version. Please report the lines above.");
        }

        private static IEnumerable<MethodBase> Methods(Type type)
        {
            foreach (MethodInfo m in type.GetMethods(All))
            {
                yield return m;
            }
            foreach (ConstructorInfo c in type.GetConstructors(All))
            {
                yield return c;
            }
        }

        private static void CheckMethod(MethodBase method, SortedSet<string> problems)
        {
            byte[] il;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch
            {
                return;
            }
            if (il == null)
            {
                return;
            }

            Type[] typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
            Type[] methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;

            int pos = 0;
            while (pos < il.Length)
            {
                short value = il[pos++];
                if (value == 0xFE && pos < il.Length)
                {
                    value = (short)(0xFE00 | il[pos++]);
                }
                if (!OpCodesByValue.TryGetValue(value, out OpCode op))
                {
                    return; // Unknown opcode; stop scanning this method rather than misread it.
                }

                switch (op.OperandType)
                {
                    case OperandType.InlineField:
                    case OperandType.InlineMethod:
                    case OperandType.InlineTok:
                        int token = BitConverter.ToInt32(il, pos);
                        CheckToken(method, token, typeArgs, methodArgs, problems);
                        pos += 4;
                        break;
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        pos += 1;
                        break;
                    case OperandType.InlineVar:
                        pos += 2;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        pos += 8;
                        break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(il, pos);
                        pos += 4 + count * 4;
                        break;
                    default:
                        pos += 4;
                        break;
                }
            }
        }

        private static void CheckToken(MethodBase caller, int token, Type[] typeArgs, Type[] methodArgs, SortedSet<string> problems)
        {
            MemberInfo member;
            try
            {
                member = caller.Module.ResolveMember(token, typeArgs, methodArgs);
            }
            catch
            {
                return;
            }
            Type owner = member?.DeclaringType;
            if (owner == null || owner.Assembly == typeof(SelfCheck).Assembly || !IsGameAssembly(owner.Assembly))
            {
                return;
            }

            bool accessible;
            switch (member)
            {
                case FieldInfo f:
                    accessible = f.IsPublic || ((f.IsFamily || f.IsFamilyOrAssembly) && Derives(caller.DeclaringType, owner));
                    break;
                case MethodBase m:
                    accessible = m.IsPublic || ((m.IsFamily || m.IsFamilyOrAssembly) && Derives(caller.DeclaringType, owner));
                    break;
                default:
                    return;
            }

            if (!accessible)
            {
                string kind = member is FieldInfo ? "field" : "method";
                problems.Add($"{kind} {owner.Name}.{member.Name} is not public in the game (used by {caller.DeclaringType?.Name}.{caller.Name})");
            }
        }

        private static bool IsGameAssembly(Assembly assembly)
        {
            string name = assembly.GetName().Name;
            return name.StartsWith("assembly_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Derives(Type type, Type baseType)
        {
            for (Type t = type; t != null; t = t.DeclaringType)
            {
                if (t.IsSubclassOf(baseType))
                {
                    return true;
                }
            }
            return false;
        }

        private static Dictionary<short, OpCode> BuildOpCodes()
        {
            var map = new Dictionary<short, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is OpCode op)
                {
                    map[op.Value] = op;
                }
            }
            return map;
        }
    }
}
