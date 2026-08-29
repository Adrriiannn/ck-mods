#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Proves every method this framework patches actually exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test exists because of a real bug that shipped: a hook named
    /// <c>GameMusicHandler.Awake</c>, a method the game does not have. Naming a method that is not
    /// there does not fail quietly and locally. Harmony throws while binding, the mod loader turns
    /// that into one line in a log nobody reads, and every patch it had not reached yet is silently
    /// never applied. So a single typo cost custom biome music and left every other hook in the mod
    /// at the mercy of the order the compiler happened to emit the classes in.
    /// </para>
    /// <para>
    /// The check is cheap and total: walk the framework assembly, read the target off each
    /// <c>[HarmonyPatch]</c>, and resolve it by reflection exactly as Harmony would.
    /// </para>
    /// </remarks>
    internal sealed class DimensionHarmonyPatchTargetTests
    {
        [Test]
        public void EveryPatchedMethodExists()
        {
            List<string> missing = new List<string>();
            int checkedTargets = 0;

            foreach (Type patchClass in FrameworkTypes())
            {
                foreach (PatchTarget target in PatchTargetsOf(patchClass))
                {
                    checkedTargets++;
                    if (Resolves(target.DeclaringType, target.MethodName))
                    {
                        continue;
                    }

                    missing.Add(
                        target.Label + " patches " + target.DeclaringType.Name + "." +
                        target.MethodName + ", which does not exist. Candidates: " +
                        Candidates(target.DeclaringType, target.MethodName));
                }
            }

            Assert.That(
                checkedTargets,
                Is.GreaterThan(0),
                "No patch classes were found, so this test proved nothing. The scan is broken.");
            Assert.That(
                missing,
                Is.Empty,
                "A patch names a method the game does not have. Harmony throws while binding and " +
                "the loader swallows it, so patches after it may never apply:\n" +
                string.Join("\n", missing));
        }

        /// <summary>
        /// Proves no patch lands on a system lifecycle method that Burst calls directly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A SECOND WAY A PATCH DIES, and the existing test cannot see it. When Unity compiles an
        /// <c>ISystem</c> marked <c>[BurstCompile]</c>, it emits a nested
        /// <c>__codegen__&lt;Method&gt;_&lt;hash&gt;$BurstDirectCall</c> helper and dispatches through a
        /// compiled function pointer. The managed method body is still there, still resolves by
        /// reflection, and is still perfectly patchable — it is simply never the thing that runs.
        /// So the patch binds without complaint and does nothing at all.
        /// </para>
        /// <para>
        /// Presence of that nested type is the signal, and it is per METHOD rather than per system:
        /// <c>SpawnDungeonAndSceneSystem</c> generates one for OnCreate, OnUpdate, OnDestroy and
        /// OnStopRunning but not for OnStartRunning, which is the only reason the framework's hook
        /// on it works.
        /// </para>
        /// <para>
        /// WHAT THIS CANNOT SEE. Burst also inlines ordinary static methods into the jobs that call
        /// them — <c>EntityUtility.AddTile</c> and <c>EquipmentSlot.UpdateEquipment</c> both live
        /// inside <c>EquipmentUpdateSystem</c>'s job that way. No nested type is generated for those,
        /// so nothing here will flag them; they are covered instead by the single de-Burst the
        /// framework allows, which <c>DimensionBurstBudgetTests</c> guards.
        /// </para>
        /// </remarks>
        [Test]
        public void PatchedSystemMethodsAreReachable()
        {
            List<string> unreachable = new List<string>();
            int checkedTargets = 0;

            foreach (Type patchClass in FrameworkTypes())
            {
                foreach (PatchTarget target in PatchTargetsOf(patchClass))
                {
                    checkedTargets++;
                    if (!HasBurstDirectCall(target.DeclaringType, target.MethodName))
                    {
                        continue;
                    }

                    if (DeBurstedOnPurpose.Contains(
                            target.DeclaringType.Name + "." + target.MethodName))
                    {
                        continue;
                    }

                    unreachable.Add(
                        target.Label + " patches " + target.DeclaringType.Name + "." +
                        target.MethodName +
                        ", which Burst calls through a compiled function pointer. The managed body it hooks never runs.");
                }
            }

            Assert.That(
                checkedTargets,
                Is.GreaterThan(0),
                "No patch targets were found, so this test proved nothing. The scan is broken.");
            Assert.That(
                unreachable,
                Is.Empty,
                "A patch targets a Burst-compiled system method, so it binds cleanly and then does " +
                "nothing. Either pick a seam Burst does not compile, replicate the behaviour in a " +
                "companion system, or — only if there is genuinely no alternative — switch Burst " +
                "off for that system and record it here and in DimensionBurstBudgetTests:\n" +
                string.Join("\n", unreachable));
        }

        /// <summary>
        /// Proves every private field a patch asks Harmony to hand it actually exists.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A patch parameter named <c>___someField</c> tells Harmony to load that private field of
        /// the patched class into the patch's prologue. The framework uses this instead of
        /// <c>AccessTools.FieldRef</c>, which Core Keeper's mod sandbox denies outright — a mod that
        /// so much as names <c>AccessTools</c> is refused whole, with the player told only that
        /// compilation failed.
        /// </para>
        /// <para>
        /// The trade is where a wrong name shows up. A field ref failed on first use, in one patch.
        /// A wrong <c>___</c> name throws while Harmony binds the class, and the loader logs that
        /// and moves on — so every patch it had not reached yet is silently never applied. That is
        /// the same failure the test above this one was written for, arriving through a different
        /// door, and this closes it: read the parameters, resolve each name against the patched
        /// class the way Harmony does.
        /// </para>
        /// <para>
        /// WHAT THIS DOES NOT COVER. Harmony's other injections — <c>__instance</c>, <c>__result</c>,
        /// <c>__state</c>, original parameters by name — are not checked here, and neither is the
        /// TYPE of the field against the type of the parameter.
        /// </para>
        /// </remarks>
        [Test]
        public void InjectedPrivateFieldsExist()
        {
            List<string> missing = new List<string>();
            int checkedFields = 0;

            foreach (Type patchClass in FrameworkTypes())
            {
                Type classType;
                string classMethod;
                TryReadPatchTarget(
                    patchClass.GetCustomAttributes(false).OfType<Attribute>(),
                    null,
                    null,
                    out classType,
                    out classMethod);

                MethodInfo[] methods = patchClass.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                for (int i = 0; i < methods.Length; i++)
                {
                    Type memberType;
                    string memberMethod;
                    TryReadPatchTarget(
                        methods[i].GetCustomAttributes(false).OfType<Attribute>(),
                        classType,
                        classMethod,
                        out memberType,
                        out memberMethod);

                    Type target = memberType ?? classType;
                    if (target == null)
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = methods[i].GetParameters();
                    for (int p = 0; p < parameters.Length; p++)
                    {
                        string name = parameters[p].Name;
                        if (name == null || !name.StartsWith("___", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        checkedFields++;
                        string fieldName = name.Substring(3);
                        if (ResolvesAsField(target, fieldName))
                        {
                            continue;
                        }

                        missing.Add(
                            patchClass.Name + "." + methods[i].Name + " asks Harmony for " +
                            target.Name + "." + fieldName + ", which does not exist. Fields: " +
                            FieldCandidates(target));
                    }
                }
            }

            Assert.That(
                checkedFields,
                Is.GreaterThan(0),
                "No injected fields were found, so this test proved nothing. The scan is broken.");
            Assert.That(
                missing,
                Is.Empty,
                "A patch asks for a private field the game does not have. Harmony throws while " +
                "binding the class and the loader swallows it, so patches after it may never " +
                "apply:\n" + string.Join("\n", missing));
        }

        /// <summary>
        /// Whether Harmony would find this field, by name or — for the <c>___0</c> form — by index
        /// among the declared ones.
        /// </summary>
        private static bool ResolvesAsField(Type declaringType, string fieldName)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            if (fieldName.Length > 0 && fieldName.All(char.IsDigit))
            {
                int index;
                return int.TryParse(fieldName, out index) &&
                       index >= 0 &&
                       index < declaringType.GetFields(flags).Length;
            }

            for (Type type = declaringType; type != null; type = type.BaseType)
            {
                if (type.GetField(fieldName, flags) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FieldCandidates(Type declaringType)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            string[] names = declaringType
                .GetFields(flags)
                .Select(f => f.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .Take(12)
                .ToArray();
            return names.Length == 0 ? "none" : string.Join(", ", names);
        }

        /// <summary>
        /// Patched methods that Burst compiles, and which run managed only because the framework
        /// deliberately switches Burst off for their system.
        /// </summary>
        private static readonly HashSet<string> DeBurstedOnPurpose =
            new HashSet<string>(StringComparer.Ordinal)
            {
                // Joins the equipment job before Burst is switched back on. Live only because
                // ExpandNullforgeModEntry de-Bursts EquipmentUpdateSystem for worlds with content.
                "EquipmentUpdateSystem.OnUpdate",
            };

        /// <summary>
        /// Whether Unity generated a Burst direct-call helper for this method.
        /// </summary>
        private static bool HasBurstDirectCall(Type declaringType, string methodName)
        {
            string prefix = "__codegen__" + methodName + "_";
            Type[] nested = declaringType.GetNestedTypes(
                BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < nested.Length; i++)
            {
                string name = nested[i].Name;
                if (name.StartsWith(prefix, StringComparison.Ordinal) &&
                    name.EndsWith("BurstDirectCall", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<Type> FrameworkTypes()
        {
            Assembly assembly = typeof(ExpandNullforge.Portals.DimensionPortal).Assembly;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException loadFailure)
            {
                types = loadFailure.Types.Where(t => t != null).ToArray();
            }

            return types;
        }

        /// <summary>One method this framework patches, and the class or method that names it.</summary>
        private struct PatchTarget
        {
            public Type DeclaringType;
            public string MethodName;
            public string Label;
        }

        /// <summary>
        /// Every method a patch class targets, whether the class names one or its methods do.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A SECOND SHAPE THAT WAS SLIPPING THROUGH. Harmony lets a class carry the declaring type
        /// once and each patch method name its own target — which is exactly how
        /// <c>DimensionTilesetTypeUtilityPatch</c> is written, with eight
        /// <c>[HarmonyPatch(nameof(TilesetTypeUtility.GetTileset))]</c>-style methods under one
        /// <c>[HarmonyPatch(typeof(TilesetTypeUtility))]</c> class. Reading only the class's
        /// attributes yielded a declaring type with no method name, so the whole class was skipped
        /// as "not a plain named method" and every one of those eight went unchecked — the tileset
        /// render path, silently outside the guard that exists to protect exactly this.
        /// </para>
        /// <para>
        /// A method-level attribute inherits the class's declaring type when it does not state one,
        /// the same way Harmony resolves it.
        /// </para>
        /// </remarks>
        private static IEnumerable<PatchTarget> PatchTargetsOf(Type patchClass)
        {
            Type classType;
            string classMethod;
            bool classNamesSomething = TryReadPatchTarget(
                patchClass.GetCustomAttributes(false).OfType<Attribute>(),
                null,
                null,
                out classType,
                out classMethod);

            if (classNamesSomething && classType != null && !string.IsNullOrEmpty(classMethod))
            {
                yield return new PatchTarget
                {
                    DeclaringType = classType,
                    MethodName = classMethod,
                    Label = patchClass.Name,
                };
            }

            MethodInfo[] methods = patchClass.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            for (int i = 0; i < methods.Length; i++)
            {
                Type memberType;
                string memberMethod;
                if (!TryReadPatchTarget(
                        methods[i].GetCustomAttributes(false).OfType<Attribute>(),
                        classType,
                        classMethod,
                        out memberType,
                        out memberMethod))
                {
                    continue;
                }

                if (string.Equals(memberMethod, classMethod, StringComparison.Ordinal) &&
                    memberType == classType)
                {
                    // The class already named this one; counting it twice would only double a
                    // failure message.
                    continue;
                }

                yield return new PatchTarget
                {
                    DeclaringType = memberType,
                    MethodName = memberMethod,
                    Label = patchClass.Name + "." + methods[i].Name,
                };
            }
        }

        /// <summary>
        /// Reads the declaring type and method name off a set of <c>[HarmonyPatch]</c> attributes.
        /// </summary>
        /// <remarks>
        /// Harmony's attribute is read through reflection rather than referenced directly, because
        /// the test assembly does not link HarmonyLib and does not need to: the attribute stores
        /// its target in public fields on a nested info object.
        /// </remarks>
        private static bool TryReadPatchTarget(
            IEnumerable<Attribute> attributes,
            Type inheritedType,
            string inheritedMethodName,
            out Type declaringType,
            out string methodName)
        {
            declaringType = inheritedType;
            methodName = inheritedMethodName;
            bool sawAPatchAttribute = false;

            foreach (Attribute attribute in attributes)
            {
                if (attribute.GetType().Name != "HarmonyPatch")
                {
                    continue;
                }

                object info = attribute.GetType()
                    .GetField("info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(attribute);
                if (info == null)
                {
                    continue;
                }

                Type infoType = info.GetType();
                Type candidateType =
                    infoType.GetField("declaringType")?.GetValue(info) as Type ?? declaringType;
                string candidateName =
                    infoType.GetField("methodName")?.GetValue(info) as string ?? methodName;

                sawAPatchAttribute = true;
                declaringType = candidateType ?? declaringType;
                methodName = candidateName ?? methodName;
            }

            // A class may carry several attributes that together name one target, and some patches
            // name a property or a constructor rather than a method. Only plain named methods are
            // checked here; the rest are left to Harmony.
            return sawAPatchAttribute &&
                declaringType != null &&
                !string.IsNullOrEmpty(methodName);
        }

        private static bool Resolves(Type declaringType, string methodName)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            for (Type type = declaringType; type != null; type = type.BaseType)
            {
                if (type.GetMethods(flags).Any(m => m.Name == methodName))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Candidates(Type declaringType, string methodName)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            string[] names = declaringType
                .GetMethods(flags)
                .Select(m => m.Name)
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal)
                .Take(12)
                .ToArray();
            return names.Length == 0 ? "none" : string.Join(", ", names);
        }
    }
}
#endif
