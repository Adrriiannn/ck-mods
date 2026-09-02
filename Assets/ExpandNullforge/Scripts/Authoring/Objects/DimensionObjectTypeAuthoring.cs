using Pug.Conversion;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Carries what an object IS onto the running object, not just into the object table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS EXISTS BECAUSE ONE OF THE GAME'S TWO CONVERTERS DOES NOT DO IT.
    /// <c>EntityMonoBehaviourDataConverter</c>, which every vanilla object goes through, adds an
    /// <c>ObjectTypeCD</c> holding the object's type. <c>ObjectConverter</c>, which every object a
    /// mod builds goes through, does not — it copies the amount, the tags and the id, and stops.
    /// Everything that asks what an item is through the object table (which slot it equips to, which
    /// cooldown it shares, how much use it takes, which inventory slot will accept it) is fine
    /// either way, because that comes off <c>ObjectInfo</c> in the database blob.
    /// </para>
    /// <para>
    /// Three things ask the live object instead, and those were the ones a mod object could not
    /// answer: <c>AttackSystem</c> deciding whether a hit shows sparks, <c>EntityUtility</c>
    /// deciding whether damage treats the thing as destructible, and
    /// <c>EnvironmentalConditionsSystem</c> — which is the worst of the three, because it does not
    /// read a wrong default, it queries <c>.WithAll&lt;ObjectTypeCD&gt;()</c> and never sees the
    /// object at all. A custom creature standing in slime or on burning ground was outside that
    /// system entirely.
    /// </para>
    /// <para>
    /// Three, not four. <c>PlayerAttackRoutineSystem</c> was named here as a fourth and is not one:
    /// it reads <c>PlayerAttackCD.objectType</c>, a field filled from the database blob in
    /// <c>QueueHitSystem</c>, and holds no reference to this component at all. Mod beam weapons
    /// were never broken there.
    /// </para>
    /// <para>
    /// The marker is what keeps this scoped. Converting from <c>ObjectAuthoring</c> directly would
    /// hand <c>ObjectTypeCD</c> to every vanilla object built that way too, which would pull them
    /// into the same environmental query and change what the base game does. A component only this
    /// framework attaches, converted only where it is attached, changes nothing that was not ours.
    /// </para>
    /// <para>
    /// There is no field on it on purpose. The type lives on <c>ObjectAuthoring.objectType</c>, one
    /// field on one component, and a copy here would be a second place to forget to update.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DimensionObjectTypeAuthoring : MonoBehaviour
    {
    }

    /// <summary>
    /// Writes the object's type onto the converted entity, the way the game's own
    /// <c>EntityMonoBehaviourDataConverter</c> does.
    /// </summary>
    [Preserve]
    public sealed class DimensionObjectTypeConverter :
        SingleAuthoringComponentConverter<DimensionObjectTypeAuthoring>
    {
        protected override void Convert(DimensionObjectTypeAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            ObjectAuthoring source = authoring.GetComponent<ObjectAuthoring>();
            if (source == null)
            {
                // An EntityMonoBehaviourData object already got its ObjectTypeCD from the game's
                // own converter, and an object with neither has no type to carry.
                return;
            }

            AddComponentData(new ObjectTypeCD { Value = source.objectType });
        }
    }
}
