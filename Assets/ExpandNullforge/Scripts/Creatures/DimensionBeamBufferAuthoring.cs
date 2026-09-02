using Pug.Conversion;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Gives a creature with a beam attack the list of beams the game's beam system works on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE BEAM IS NOT BROKEN. THE CONVERTER IS ONE LINE SHORT. Refusing the
    /// beam outright, on the grounds that Core Keeper needs a list of beams nothing ever creates,
    /// misreads the code. <c>BeamAttackStateSystem</c> fills the list ITSELF the
    /// moment the wind-up finishes — <c>ck-db\Pug.Other\BeamAttackStateSystem.cs:133</c> adds the
    /// first beam and <c>:141</c> adds the rest of the fan — and its query at <c>:218</c> only asks
    /// that the buffer EXISTS. The gap is that <c>BeamAttackStateConverter</c> ensures
    /// <c>StateInfoCD</c> and adds <c>BeamAttackStateCD</c> and never calls
    /// <c>EnsureHasBuffer&lt;BeamBuffer&gt;()</c>. No Core Keeper prefab carries a beam, so nothing
    /// in the shipped game ever ran into the omission.
    /// </para>
    /// <para>
    /// AND THE COOLDOWN GOES WITH IT. The same query names <c>AttackCooldownTimerCD</c>, and only
    /// three converters in the game produce one — melee, ranged and jump
    /// (<c>MeleeAttackStateConverter.cs:32</c>, <c>RangeAttackStateConverter.cs:71</c>,
    /// <c>JumpAttackStateConverter.cs:25</c>). A creature whose only attack is the beam would have
    /// had none, and <c>BeamAttackStateRequest.ShouldUpdate</c> checks for it before it will even
    /// consider entering the state. Adding it pulls the creature into no other query: every system
    /// that names the cooldown also names its own attack state beside it.
    /// </para>
    /// <para>
    /// Written as a marker plus a converter of our own rather than as a patch, because that is the
    /// sanctioned shape and the framework already ships one — see
    /// <see cref="DimensionSummoningItemConverter"/>, which ensures a vanilla buffer the same way.
    /// The generator puts this on beside <c>BeamAttackStateAuthoring</c> and takes it off with it.
    /// </para>
    /// </remarks>
    public sealed class DimensionBeamBufferAuthoring : MonoBehaviour
    {
    }

    [Preserve]
    public sealed class DimensionBeamBufferConverter :
        SingleAuthoringComponentConverter<DimensionBeamBufferAuthoring>
    {
        protected override void Convert(DimensionBeamBufferAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            EnsureHasBuffer<BeamBuffer>();
            EnsureHasComponent<AttackCooldownTimerCD>();
        }
    }
}
