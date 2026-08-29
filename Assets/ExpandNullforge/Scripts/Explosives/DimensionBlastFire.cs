using Pug.Conversion;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Explosives
{
    /// <summary>Marks a blast as one that leaves a patch of the game's fire behind it.</summary>
    /// <remarks>
    /// <para>
    /// THIS EXISTS BECAUSE THE PREFAB FIELD DOES NOT. <c>ExplosionCD</c> has
    /// <c>spawnNapalmObjectID</c>, but <c>ExplosionAuthoring</c> has only three fields — damage,
    /// tile damage, radius (<c>ck-db\Pug.ECS.Authoring\ExplosionAuthoring.cs:9-15</c>) — so no
    /// vanilla prefab authors fire at all. In vanilla the field is written at runtime and only by a
    /// dice roll against the player's gear: <c>ExplosiveSystem.cs:181</c> sets it to
    /// <c>Napalm</c> or <c>None</c> depending on <c>ChanceToSpawnNapalmFromExplosives</c>, and it
    /// does so on the way out of <c>CreateExplosion</c>, overwriting whatever the prefab held.
    /// </para>
    /// <para>
    /// So a bomb that always leaves fire cannot be baked; it has to be written onto the blast after
    /// the game has finished writing it and before the blast acts. That is one small system, and
    /// this component is what tells it which blasts to write to.
    /// </para>
    /// </remarks>
    public sealed class DimensionBlastFireAuthoring : MonoBehaviour
    {
        [Tooltip("Which patch of fire: 0 is the long-lasting one, 1 the short one.")]
        public int napalmVariation;
    }

    /// <summary>A blast that should leave fire, and which of the two patches.</summary>
    public struct DimensionBlastFireCD : IComponentData
    {
        public int NapalmVariation;
    }

    [Preserve]
    public sealed class DimensionBlastFireConverter :
        SingleAuthoringComponentConverter<DimensionBlastFireAuthoring>
    {
        protected override void Convert(DimensionBlastFireAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            AddComponentData(new DimensionBlastFireCD
            {
                NapalmVariation = authoring.napalmVariation < 0 ? 0 : authoring.napalmVariation
            });
        }
    }
}
