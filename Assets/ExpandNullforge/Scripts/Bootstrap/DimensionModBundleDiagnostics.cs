using System.Collections.Generic;
using PugMod;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// Names the mods whose second asset bundle the game refused to load, so a half-missing mod is
    /// reported as the packaging fault it is instead of looking like broken content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT GOES WRONG. Core Keeper registers a mod's DataBlocks per bundle but keys the registration
    /// by the MOD, not the bundle (<c>Loader.LoadBundle</c>):
    /// </para>
    /// <code>
    /// ScriptableData.AddDataBlocksLoader(mod.Metadata.guid, new ScriptableDataLoader(assetBundle));
    /// </code>
    /// <para>
    /// and the registry refuses a key it already holds:
    /// </para>
    /// <code>
    /// if (s_dataBlockLoaders.ContainsKey(name))
    ///     DimensionLog.Fatal(DimensionLogChannels.Boot, null, "Data block loader already added for key " + name);   // second bundle dropped
    /// else
    ///     s_dataBlockLoaders.Add(name, loader);
    /// </code>
    /// <para>
    /// So a mod shipping two bundles keeps the first and loses every DataBlock in the second. The mod
    /// still loads. Nothing marks the content as absent — recipes point at nothing, objects resolve
    /// to nothing, and the one clue is a single engine error line that says "already added for key"
    /// and names a GUID rather than a mod.
    /// </para>
    /// <para>
    /// WHY THIS IS A RUNTIME CHECK. There is nothing to inspect at authoring time: bundles are an
    /// output of the PugMod build, not project assets, so no amount of validation in the dashboard
    /// can see them. The condition is only observable once the game has loaded the mod — which is
    /// exactly where this runs.
    /// </para>
    /// <para>
    /// It reports on EVERY loaded mod, not only ours. The failure is a property of how a mod was
    /// packaged, our users will hit it in their own mods, and a framework that can name the problem
    /// for free should.
    /// </para>
    /// </remarks>
    internal static class DimensionModBundleDiagnostics
    {
        private static bool reported;

        /// <summary>
        /// Logs one error per mod carrying more than one asset bundle. Safe to call repeatedly; only
        /// the first call with a resolvable mod list does anything.
        /// </summary>
        public static void ReportMultiBundleModsOnce()
        {
            if (reported || API.ModLoader == null)
            {
                return;
            }

            IEnumerable<LoadedMod> mods = API.ModLoader.LoadedMods;
            if (mods == null)
            {
                // The loader exists but has not published its list yet — try again next tick rather
                // than latching, or a slow load would silence the check permanently.
                return;
            }

            reported = true;

            foreach (LoadedMod mod in mods)
            {
                if (mod == null || mod.AssetBundles == null || mod.AssetBundles.Count <= 1)
                {
                    continue;
                }

                string kept = mod.AssetBundles[0] != null ? mod.AssetBundles[0].name : "(unnamed)";
                string lost = string.Empty;
                for (int i = 1; i < mod.AssetBundles.Count; i++)
                {
                    AssetBundle bundle = mod.AssetBundles[i];
                    lost += (lost.Length > 0 ? ", " : string.Empty) +
                            (bundle != null ? bundle.name : "(unnamed)");
                }

                // Metadata is a struct, so there is nothing to null-check but the name itself.
                string name = string.IsNullOrEmpty(mod.Metadata.name) ? "(unnamed mod)" : mod.Metadata.name;
                DimensionLog.Fatal(DimensionLogChannels.Boot, null, 
                    "Mod '" + name + "' ships " + mod.AssetBundles.Count +
                    " asset bundles. Core Keeper registers DataBlocks once per MOD, so only '" + kept +
                    "' contributed any; everything defined in " + lost + " is absent from this session " +
                    "even though the mod loaded. Content from those bundles will silently resolve to " +
                    "nothing. Rebuild the mod so it produces a single asset bundle.");
            }
        }

        /// <summary>Lets the check run again after a reload, so a rebuilt mod is re-examined.</summary>
        public static void Reset()
        {
            reported = false;
        }
    }
}
