using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Loot;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The generate-time answer to "which loot table is this name?", covering the mod's own
    /// tables as well as the game's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A custom table's id is minted from its name by pure arithmetic
    /// (<see cref="DimensionLootTableRegistry.ComputeLootTableId"/>), so the editor can stamp
    /// the exact id the runtime table will carry without the two ever meeting. What the editor
    /// must NOT do is mint for a name the template never authored — a typo would then bake an id
    /// no table matches, and the chest or drop would sit silently empty. Hence the seeded set:
    /// mint only for names the current generate actually knows.
    /// </para>
    /// <para>
    /// Seeded once at the top of a generate and cleared at its end, the
    /// <c>DimensionConditionScope</c> pattern in miniature.
    /// </para>
    /// </remarks>
    internal static class DimensionEditorLootTables
    {
        private static readonly HashSet<string> Known = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Collects every loot table the template authors, wherever it hangs.</summary>
        public static void Seed(DimensionTemplateAsset template)
        {
            Known.Clear();
            if (template == null)
            {
                return;
            }

            Add(template.GlobalLootTables);

            DimensionMobAsset[] mobs = template.GlobalMobs;
            if (mobs != null)
            {
                for (int i = 0; i < mobs.Length; i++)
                {
                    if (mobs[i] == null)
                    {
                        continue;
                    }

                    Add(mobs[i].LootTable);
                    DimensionEliteVariantTemplate elite = mobs[i].EliteVariant;
                    if (elite != null)
                    {
                        Add(elite.LootTable);
                    }
                }
            }

            DimensionBossAsset[] bosses = template.GlobalBosses;
            if (bosses != null)
            {
                for (int i = 0; i < bosses.Length; i++)
                {
                    if (bosses[i] != null)
                    {
                        Add(bosses[i].LootTable);
                    }
                }
            }

            DimensionAnimalAsset[] animals = template.GlobalAnimals;
            if (animals != null)
            {
                for (int i = 0; i < animals.Length; i++)
                {
                    if (animals[i] != null)
                    {
                        Add(animals[i].LootTable);
                    }
                }
            }
        }

        public static void Clear()
        {
            Known.Clear();
        }

        /// <summary>
        /// A vanilla <c>LootTableID</c> name, or a table this template authors (minted id).
        /// </summary>
        public static bool TryResolve(string tableName, out LootTableID lootTableId)
        {
            lootTableId = LootTableID.Empty;
            if (string.IsNullOrEmpty(tableName))
            {
                return false;
            }

            if (Enum.TryParse(tableName, false, out lootTableId))
            {
                return true;
            }

            if (Known.Contains(tableName))
            {
                lootTableId = (LootTableID)DimensionLootTableRegistry.ComputeLootTableId(tableName);
                return true;
            }

            lootTableId = LootTableID.Empty;
            return false;
        }

        private static void Add(DimensionLootTableAsset[] tables)
        {
            if (tables == null)
            {
                return;
            }

            for (int i = 0; i < tables.Length; i++)
            {
                Add(tables[i]);
            }
        }

        private static void Add(DimensionLootTableAsset table)
        {
            if (table != null && table.Enabled && !string.IsNullOrEmpty(table.LootTableId) &&
                table.EnabledEntryCount > 0)
            {
                Known.Add(table.LootTableId);
            }
        }
    }
}
