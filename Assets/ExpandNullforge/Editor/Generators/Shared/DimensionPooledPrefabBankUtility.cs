using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Keeps a mod's pooled-prefab bank listing every body the framework generated, so shipping a
    /// bank cannot take a generated object's pool away from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE HAZARD, VERIFIED IN THE DECOMPILE. <c>MemoryManager.PoolModdedPrefabs</c>
    /// (<c>ck-db/Pug.Other/MemoryManager.cs:94-136</c>) walks each loaded mod and looks through its
    /// assets for a <c>PooledModGraphicalObjectBank</c>. If it finds one, it pools <b>only</b> what
    /// that bank lists and skips the mod's other prefabs entirely; if it finds none, it pools every
    /// prefab in the mod that carries an <c>IPoolable</c>. So adding a bank for one prefab silently
    /// removes the pool from every other prefab in the same mod.
    /// </para>
    /// <para>
    /// AND A MISSING POOL IS NOT A MISSING PICTURE, IT IS A CRASH.
    /// <c>MemoryManager.GetPrefabPool</c> (<c>:243</c>) is
    /// <c>_pools[_poolFromComponentType[componentType]]</c> — two raw indexer reads with no
    /// fallback — reached from <c>CreateGraphicalObjectSystem</c> for every object drawn. An
    /// unpooled generated body is a <c>KeyNotFoundException</c> the first time one comes on screen,
    /// thrown from inside the game's own draw loop where nothing of ours can catch it.
    /// </para>
    /// <para>
    /// WHY THE FIX IS TO FILL A BANK RATHER THAN TO CREATE ONE. The absence of a bank is the safe
    /// state: it is what makes the game pool everything. Creating one on an author's behalf would
    /// move their mod from "everything is pooled" to "only this list is pooled", which is a
    /// strictly more fragile arrangement they never asked for. So a mod with no bank is left alone,
    /// and a mod that HAS one is reconciled with what the generators just wrote.
    /// </para>
    /// <para>
    /// EVERY POOLABLE PREFAB IN THE MOD IS LISTED, not only the framework's. The bank's effect is
    /// mod-wide, so a hand-authored body sitting beside the generated ones is exposed to exactly
    /// the same crash, and leaving it out would fix the framework's prefabs by name while breaking
    /// the author's.
    /// </para>
    /// </remarks>
    internal static class DimensionPooledPrefabBankUtility
    {
        /// <summary>What one reconciliation did, for the generation summary.</summary>
        internal sealed class Result
        {
            /// <summary>Whether the mod ships a bank at all. False means nothing needed doing.</summary>
            public bool BankExists;

            /// <summary>Where the bank is, when there is one.</summary>
            public string BankPath = string.Empty;

            /// <summary>Prefabs added to the bank by this run.</summary>
            public readonly List<string> Added = new List<string>();

            /// <summary>Prefabs the bank already listed.</summary>
            public int AlreadyListed;

            /// <summary>A line for the generation dialog, or empty when there is nothing to say.</summary>
            public string Summarize()
            {
                if (!BankExists || Added.Count == 0)
                {
                    return string.Empty;
                }

                return "\nPooled prefabs: " + Added.Count + " added to " + BankPath + ".";
            }
        }

        /// <summary>
        /// Lists every poolable prefab in the mod in the mod's bank, if it has one.
        /// </summary>
        /// <param name="modRootFolder">The mod folder, the same one the generators write into.</param>
        /// <param name="warn">Told about anything an author has to decide themselves.</param>
        public static Result EnsureGeneratedPrefabsArePooled(string modRootFolder, Action<string> warn)
        {
            Result result = new Result();
            if (string.IsNullOrEmpty(modRootFolder) || !AssetDatabase.IsValidFolder(modRootFolder))
            {
                return result;
            }

            string[] bankGuids = AssetDatabase.FindAssets(
                "t:PooledModGraphicalObjectBank", new[] { modRootFolder });
            if (bankGuids.Length == 0)
            {
                return result;
            }

            string bankPath = AssetDatabase.GUIDToAssetPath(bankGuids[0]);
            PooledModGraphicalObjectBank bank =
                AssetDatabase.LoadAssetAtPath<PooledModGraphicalObjectBank>(bankPath);
            if (bank == null)
            {
                return result;
            }

            result.BankExists = true;
            result.BankPath = bankPath;

            if (bankGuids.Length > 1 && warn != null)
            {
                // The game takes the FIRST bank it finds while walking the mod's assets, in an
                // order no mod controls, so two banks means the pooled set is decided by chance.
                warn(
                    "This mod has " + bankGuids.Length + " pooled-prefab banks. The game uses " +
                    "whichever one it happens to find first, so which prefabs get pooled is left " +
                    "to chance. Keep one bank and delete the rest; the one being kept up to date " +
                    "here is " + bankPath + ".");
            }

            if (bank.modPoolInitializers == null)
            {
                bank.modPoolInitializers = new List<PoolablePrefabBank.PoolablePrefab>();
            }

            HashSet<int> alreadyListed = new HashSet<int>();
            bool changed = false;
            for (int i = bank.modPoolInitializers.Count - 1; i >= 0; i--)
            {
                PoolablePrefabBank.PoolablePrefab entry = bank.modPoolInitializers[i];
                if (entry == null || entry.prefab == null)
                {
                    // An entry pointing at a deleted prefab makes the game log a warning per load
                    // and pools nothing, so it is worth nobody's while to keep.
                    bank.modPoolInitializers.RemoveAt(i);
                    changed = true;
                    continue;
                }

                alreadyListed.Add(entry.prefab.GetInstanceID());
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { modRootFolder });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent(typeof(IPoolable)) == null)
                {
                    continue;
                }

                if (alreadyListed.Contains(prefab.GetInstanceID()))
                {
                    result.AlreadyListed++;
                    continue;
                }

                // The same numbers the game gives an unbanked mod prefab
                // (MemoryManager.TryCreateModdedPrefabPool), so listing a prefab here changes
                // nothing about how it behaves — only whether it is pooled at all.
                bank.modPoolInitializers.Add(new PoolablePrefabBank.PoolablePrefab
                {
                    prefab = prefab,
                    initialSize = 16,
                    maxSize = 1024,
                    maxFreeSize = 1024
                });
                result.Added.Add(path);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(bank);
                AssetDatabase.SaveAssets();
            }

            return result;
        }
    }
}
