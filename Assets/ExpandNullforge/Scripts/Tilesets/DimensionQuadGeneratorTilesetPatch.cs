using System.Collections.Generic;
using HarmonyLib;
using PugTilemap;
using PugTilemap.Quads;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Stops a borrowed layer rule from silently refusing to draw for a custom tileset id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT BREAKS WITHOUT THIS. Before a map layer builds anything it asks the layer's quad
    /// generator whether it applies to this tileset, and returns early on false. There are FIVE such
    /// gates across BOTH map-layer implementations — <c>PugMapLayer2.cs:123</c> (an early-out in
    /// <c>Init2</c>), <c>:605</c>, <c>:617</c>, and the older <c>PugTilemap/PugMapLayer.cs:129</c>,
    /// <c>:150</c>. Patching the method rather than its callers covers all of them, including the
    /// second implementation, which is easy to miss when reading only <c>PugMapLayer2</c>.
    /// The generator answers from an allow/deny list of <c>Tileset</c> enum values:
    /// </para>
    /// <code>
    /// public bool HasTileset(int tileset)                    // QuadGenerator.cs:78
    /// {
    ///     List&lt;Tileset&gt; list = excludeFromTilesetInstead ? dontDrawForTilesets
    ///                                                     : onlyDrawForTilesets;
    ///     if (list.Count == 0) return true;
    ///     for (int i = 0; i &lt; list.Count; i++)
    ///         if (list[i] == (Tileset)tileset) return !excludeFromTilesetInstead;
    ///     return excludeFromTilesetInstead;
    /// }
    /// </code>
    /// <para>
    /// Deny mode needs nothing from us: an id nobody listed is drawn, and a custom id can never
    /// appear in vanilla's exclusions. Allow mode is the problem — a non-empty
    /// <c>onlyDrawForTilesets</c> means "only these tilesets", and vanilla's lists are authored
    /// entirely from vanilla enum members, so a custom id can never match. That layer then never
    /// draws, with no error and no log: geometry simply missing.
    /// </para>
    /// <para>
    /// WHY IT BITES EVERY CUSTOM TILESET. A tileset with no <c>LayersTemplate</c> of its own is
    /// served vanilla Dirt's rule set (<see cref="DimensionTilesetRegistry.ResolveFallbackLayers"/>),
    /// so it inherits whatever restrictions Dirt's generators carry. The bank ships only inside the
    /// game, so those lists cannot be inspected from the SDK — which is exactly why this is a patch
    /// and not an audit. Rather than depend on unseen authored data staying a particular way, make
    /// the outcome true by construction and report what was overridden.
    /// </para>
    /// <para>
    /// THE RULE, AND WHY IT IS NOT A BLANKET OVERRIDE. A list naming only vanilla ids cannot have
    /// meant anything about a custom tileset, so it is treated as "not about us" and the layer
    /// draws. A list that names <b>any</b> custom id is author-curated — somebody deliberately
    /// enumerated custom tilesets — so it is honoured exactly, including the case where it lists
    /// other custom ids and not ours. That keeps a modder's own template authoritative over their
    /// own layers while stopping vanilla's Dirt-shaped restrictions from erasing ours.
    /// </para>
    /// <para>
    /// Drawing the layer is also the coherent outcome: the texture accessors are patched alongside
    /// this, so the layer samples the custom sheet at the same UVs. The framework already requires
    /// custom sheets to follow the Dirt layout, so a borrowed rule lands on the right art.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(QuadGenerator), nameof(QuadGenerator.HasTileset))]
    internal static class DimensionQuadGeneratorTilesetPatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        /// <summary>
        /// Layers already reported. Keyed by layer rather than by generator instance: the question
        /// worth answering is "which layers restrict themselves", and every custom tileset shares
        /// one borrowed rule set, so per-instance keying would say the same thing many times.
        /// </summary>
        private static readonly HashSet<LayerName> ReportedLayers = new HashSet<LayerName>();

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static bool Before(QuadGenerator __instance, int tileset, ref bool __result)
        {
            Fired++;

            // One integer compare rejects every vanilla tileset, which is nearly every call. This
            // method runs per layer per map-layer build, so the early out matters.
            if (!DimensionTilesetRegistry.IsCustomTilesetId(tileset) || __instance == null)
            {
                return true;
            }

            // Deny mode already resolves to "draw" for an unlisted id, and an explicit exclusion is
            // a decision. Either way the original is right.
            if (__instance.excludeFromTilesetInstead)
            {
                return true;
            }

            List<Tileset> allowed = __instance.onlyDrawForTilesets;
            if (allowed == null || allowed.Count == 0)
            {
                // Unrestricted — the original returns true on its own.
                return true;
            }

            bool namesACustomId = false;
            for (int i = 0; i < allowed.Count; i++)
            {
                int id = (int)allowed[i];
                if (id == tileset)
                {
                    // Explicitly allowed; the original agrees.
                    return true;
                }

                if (DimensionTilesetRegistry.IsCustomTilesetId(id))
                {
                    namesACustomId = true;
                }
            }

            if (namesACustomId)
            {
                // Author-curated for custom tilesets and ours is not on it. Honour that.
                return true;
            }

            ReportOverrideOnce(__instance, tileset, allowed);
            __result = true;
            return false;
        }

        /// <summary>
        /// Names the layer whose restriction was lifted. This is the only visibility into which
        /// vanilla layers are allow-listed, since the tileset bank ships only with the game — so it
        /// stays a plain log rather than a verbose-gated one, and fires once per layer.
        /// </summary>
        private static void ReportOverrideOnce(QuadGenerator generator, int tileset, List<Tileset> allowed)
        {
            if (!ReportedLayers.Add(generator.layerName))
            {
                return;
            }

            string list = string.Empty;
            for (int i = 0; i < allowed.Count; i++)
            {
                list += (list.Length > 0 ? ", " : string.Empty) + allowed[i] + "(" + (int)allowed[i] + ")";
            }

            DimensionTilesetRegistry.TryGet(tileset, out DimensionCustomTileset custom);
            DimensionLog.Trace(DimensionLogChannels.Tileset, null, 
                "Layer '" + generator.layerName + "' is restricted to [" + list +
                "] and would not draw for custom tilesets. Allowing it — the list names no custom " +
                "id, so it cannot have been about them. First seen for '" +
                (custom != null ? custom.Name : "id " + tileset) + "'.");
        }
    }
}
