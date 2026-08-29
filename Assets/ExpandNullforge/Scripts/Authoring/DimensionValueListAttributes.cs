using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Marks a text field whose legal values are a list Core Keeper holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SOME OF THESE BOXES HAVE ONLY ONE RIGHT ANSWER OUT OF FOURTEEN HUNDRED, and until a field
    /// says which list it belongs to, the editor has no way of offering that list. Marking the
    /// field is the whole mechanism: a small drawer sees the mark and puts a Browse button beside
    /// the box.
    /// </para>
    /// <para>
    /// THE BOX STAYS A BOX. None of these turn the field into a dropdown, because every one of them
    /// has a legitimate value the list cannot know — a sound a mod ships itself, an effect a mod
    /// invented. The button is an offer, never a fence.
    /// </para>
    /// <para>
    /// The marks live here, in the runtime assembly, because that is where the fields are. What
    /// they draw lives in the editor assembly, where the lists and the window are.
    /// </para>
    /// </remarks>
    public sealed class DimensionSoundNameAttribute : PropertyAttribute
    {
    }

    /// <summary>Marks a text field naming one of the game's puffs of dust and sparks.</summary>
    public sealed class DimensionPuffNameAttribute : PropertyAttribute
    {
    }

    /// <summary>Marks a text field naming one of the twelve skills.</summary>
    public sealed class DimensionSkillNameAttribute : PropertyAttribute
    {
    }

    /// <summary>Marks a text field naming a stat effect: the game's own, or one this mod invents.</summary>
    public sealed class DimensionConditionNameAttribute : PropertyAttribute
    {
    }
}
