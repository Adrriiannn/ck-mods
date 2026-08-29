using UnityEngine;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// A boss's body, plus the floating name only a boss carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE SHARED CLASS, SELF-CONFIGURING, AND THAT IS A CONTRACT. The pooled graphical-object
    /// system buckets view instances by component type, so every boss shares this pool — an
    /// instance that served boss A will be handed to boss B. That is only legal because every
    /// per-boss datum here is re-derived in <see cref="OnOccupied"/> from the entity's own
    /// object id; nothing is baked into the instance.
    /// </para>
    /// <para>
    /// Being a separate type from <see cref="DimensionCreatureView"/> is what keeps bosses out of
    /// the mob pool. A boss handed a mob's instance would have no nameplate to render into, and a
    /// mob handed a boss's instance would wear a name it does not have; two types are two pools,
    /// and neither can happen.
    /// </para>
    /// <para>
    /// The name renders the way vanilla bosses do: a PugText child fed a "Names/…" localization
    /// term, re-styled with the exact values ripped from the Larva Hive Boss's nameplate. A
    /// missing localization row shows the raw term — visible and debuggable, never a crash.
    /// </para>
    /// </remarks>
    public class DimensionBossView : DimensionCreatureView
    {
        [Tooltip("The nameplate child. Wired at generation.")]
        public PugText nameText;

        [Tooltip("The still picture behind the name plate, for a boss with no clips yet.")]
        public SpriteRenderer body;

        public override void OnOccupied()
        {
            base.OnOccupied();

            DimensionBossPresentationDefinition presentation =
                DimensionBossPresentationRegistry.For(objectData.objectID);
            if (presentation == null)
            {
                return;
            }

            // The still picture is the fallback for a boss with no clips, and it is switched off
            // for one that has them — otherwise a boss that draws its own animation would also be
            // wearing a frozen copy of itself, which is exactly what a pooled instance handed on
            // from a clip-less boss would leave behind.
            if (body != null)
            {
                bool animated = currentPresentation != null && currentPresentation.HasBody;
                body.sprite = animated ? null : presentation.BodySprite;
                body.enabled = !animated && presentation.BodySprite != null;
            }

            if (nameText != null && !string.IsNullOrEmpty(presentation.NameTerm))
            {
                nameText.localize = true;
                nameText.Render(presentation.NameTerm, false, true, true);
            }
        }
    }
}
