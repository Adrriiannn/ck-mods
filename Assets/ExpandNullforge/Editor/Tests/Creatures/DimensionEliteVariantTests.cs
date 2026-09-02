using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Holds the elite's stat block to what it claims to be: the same creature, harder.
    /// </summary>
    /// <remarks>
    /// The bug these exist for was silent and backwards. <c>ScaledForElite</c> turned every elite
    /// into a fixed-number creature, resolving its parent's health as though the parent were level
    /// one and multiplying that. On a creature using the area-level curve the result was an elite
    /// that was stronger in the starting biome and WEAKER everywhere after it, and that had lost
    /// its <c>AreaLevelAuthoring</c> — so its attack damage stopped scaling as well. Nothing about
    /// the prefab looked wrong; it only showed up if someone fought one at depth.
    /// </remarks>
    public sealed class DimensionEliteVariantTests
    {
        private static DimensionCreatureStatsTemplate StatsWith(
            DimensionCreatureStatSource source,
            int maxHealth)
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            SerializedProperty stats = holder.FindProperty("creatureStats");
            stats.FindPropertyRelative("statSource").enumValueIndex = (int)source;
            stats.FindPropertyRelative("maxHealth").intValue = maxHealth;
            holder.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureStatsTemplate built = host.CreatureStats;
            Object.DestroyImmediate(host);
            return built;
        }

        private static DimensionEliteVariantTemplate Elite(float healthMultiplier, int extraLevels)
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            SerializedProperty elite = holder.FindProperty("eliteVariant");
            elite.FindPropertyRelative("enabled").boolValue = true;
            elite.FindPropertyRelative("healthMultiplier").floatValue = healthMultiplier;
            elite.FindPropertyRelative("extraLevels").intValue = extraLevels;
            holder.ApplyModifiedPropertiesWithoutUndo();
            DimensionEliteVariantTemplate built = host.EliteVariant;
            Object.DestroyImmediate(host);
            return built;
        }

        [Test]
        public void AnEliteOfATypedCreatureMultipliesTheTypedNumbers()
        {
            DimensionCreatureStatsTemplate parent =
                StatsWith(DimensionCreatureStatSource.Authored, 200);

            DimensionCreatureStatsTemplate scaled =
                parent.ScaledForElite(Elite(3f, 2), 1, true, false);

            Assert.AreEqual(DimensionCreatureStatSource.Authored, scaled.StatSource);
            Assert.AreEqual(600, scaled.MaxHealth);
        }

        [Test]
        public void AnEliteOfALevelScaledCreatureKeepsScaling()
        {
            DimensionCreatureStatsTemplate parent =
                StatsWith(DimensionCreatureStatSource.AreaLevelCurve, 200);

            DimensionCreatureStatsTemplate scaled =
                parent.ScaledForElite(Elite(3f, 2), 1, true, false);

            Assert.AreEqual(
                DimensionCreatureStatSource.AreaLevelCurve,
                scaled.StatSource,
                "freezing the curve resolved the parent at level one, which made the elite of a " +
                "deep-biome creature weaker than the creature itself");
            Assert.IsFalse(
                scaled.UsesAuthoredNumbers,
                "an elite that keeps the curve is the only one that also keeps AreaLevelAuthoring, " +
                "which is what scales its attack damage");
        }

        [Test]
        public void TheLevelBumpStaysInsideWhatTheGameCanExpress()
        {
            Assert.AreEqual(
                4,
                Elite(1f, 9).ExtraLevels,
                "the level is areaLevel + (int)rarity and Legendary is 4; anything past that is " +
                "a rarity the game has no value for");
            Assert.AreEqual(0, Elite(1f, -3).ExtraLevels);
        }
    }
}
