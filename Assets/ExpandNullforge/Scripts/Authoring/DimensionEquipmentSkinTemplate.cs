using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Which part of the character a piece of equipment is drawn on.
    /// </summary>
    /// <remarks>
    /// These are the only three the game reads. Measured in <c>PlayerController</c>, exactly three
    /// slots consult <c>EquipmentSkinCD</c> — helm, breast and pants — each falling back from the
    /// vanity slot to the equipment slot. A ring or a necklace has no skin because the character
    /// sprite has nowhere to put one.
    /// </remarks>
    public enum DimensionEquipmentSkinSlot
    {
        /// <summary>Not drawn on the character at all. Most items.</summary>
        NotWorn = 0,

        /// <summary>Drawn on the head.</summary>
        Head = 1,

        /// <summary>Drawn on the chest.</summary>
        Chest = 2,

        /// <summary>Drawn on the legs.</summary>
        Legs = 3
    }

    /// <summary>How much hair shows out from under a helmet.</summary>
    public enum DimensionHairUnderHelm
    {
        /// <summary>None. A full helm that covers the head.</summary>
        Hidden = 0,

        /// <summary>Some. A hat or a circlet.</summary>
        PartlyShown = 1,

        /// <summary>All of it. A headband or a pair of glasses.</summary>
        FullyShown = 2
    }

    /// <summary>
    /// How a worn item is drawn on the player character.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <c>EquipmentSkinAuthoring</c>, on 228 vanilla prefabs, and it is the difference
    /// between a helmet that exists and a helmet you can see. Without it a custom armour piece
    /// equips, grants its stats, shows in the inventory — and the character on screen is bare.
    /// Nothing errors, which is what makes it worth asking about plainly.
    /// </para>
    /// <para>
    /// SHEET SIZE. Core Keeper draws these from a <b>234 x 156</b> sheet: every vanilla armour
    /// texture measured is exactly that, and it is what <c>SkinBaseDataBlock</c> declares. A sheet
    /// of another size is not rejected by the game, it is sampled as though it were that size, so
    /// the art lands in the wrong places rather than failing loudly.
    /// </para>
    /// <para>
    /// Composable, like wiring and secondary use, because being drawn on the character is something
    /// an item HAS. A helmet is still a helmet whether or not somebody has drawn it yet.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionEquipmentSkinTemplate
    {
        /// <summary>The width Core Keeper samples an equipment sheet at.</summary>
        public const int SheetWidth = 234;

        /// <summary>The height Core Keeper samples an equipment sheet at.</summary>
        public const int SheetHeight = 156;

        [Tooltip("Where on the character this is drawn. Most items are not worn at all.")]
        [SerializeField] private DimensionEquipmentSkinSlot slot = DimensionEquipmentSkinSlot.NotWorn;

        [Tooltip("The character sheet for it. 234 x 156, the size every vanilla armour sheet is.")]
        [SerializeField] private Texture2D sheet;

        [Tooltip("The parts of it that glow in the dark. Optional.")]
        [SerializeField] private Texture2D glowingParts;

        [Header("Head only")]
        [Tooltip("How much hair shows out from under it.")]
        [SerializeField] private DimensionHairUnderHelm hairUnderIt = DimensionHairUnderHelm.Hidden;

        [Tooltip("Nudges it on the head, in pixels. For a hat that sits high or low.")]
        [SerializeField] private Vector2Int nudge = Vector2Int.zero;

        [Header("Chest only")]
        [Tooltip("It covers the shirt underneath. Untick for something worn over clothing.")]
        [SerializeField] private bool hidesTheShirt = true;

        [Header("Legs only")]
        [Tooltip("It covers the trousers underneath.")]
        [SerializeField] private bool hidesTheTrousers = true;

        public DimensionEquipmentSkinSlot Slot
        {
            get { return slot; }
        }

        public Texture2D Sheet
        {
            get { return sheet; }
        }

        public Texture2D GlowingParts
        {
            get { return glowingParts; }
        }

        /// <summary>How much hair shows, or hidden when this is not a head piece.</summary>
        public DimensionHairUnderHelm HairUnderIt
        {
            get
            {
                return slot == DimensionEquipmentSkinSlot.Head
                    ? hairUnderIt
                    : DimensionHairUnderHelm.Hidden;
            }
        }

        /// <summary>The pixel nudge, or none when this is not a head piece.</summary>
        public Vector2Int Nudge
        {
            get { return slot == DimensionEquipmentSkinSlot.Head ? nudge : Vector2Int.zero; }
        }

        public bool HidesTheShirt
        {
            get { return hidesTheShirt; }
        }

        public bool HidesTheTrousers
        {
            get { return hidesTheTrousers; }
        }

        /// <summary>Whether this item is drawn on the character at all.</summary>
        public bool IsWorn
        {
            get { return slot != DimensionEquipmentSkinSlot.NotWorn; }
        }

        /// <summary>Whether it is worn and there is art to draw.</summary>
        public bool ShowsOnTheCharacter
        {
            get { return IsWorn && sheet != null; }
        }

        /// <summary>
        /// Whether it was set to be worn but has no art, so the character wears nothing visible.
        /// </summary>
        /// <remarks>
        /// The quiet failure this whole template exists to surface. Everything else about the item
        /// works — it equips, it grants its stats, it sits in the inventory — and the player looking
        /// at their own character sees no change.
        /// </remarks>
        public bool WornButInvisible
        {
            get { return IsWorn && sheet == null; }
        }

        /// <summary>Whether the sheet is not the size Core Keeper samples equipment art at.</summary>
        /// <remarks>
        /// Worth its own check because the game does not reject a wrong size — it samples the sheet
        /// as though it were 234 x 156 regardless, so the symptom is art appearing in the wrong
        /// places rather than art failing to appear.
        /// </remarks>
        public bool SheetIsTheWrongSize
        {
            get
            {
                return sheet != null && (sheet.width != SheetWidth || sheet.height != SheetHeight);
            }
        }
    }
}
