using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class — Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    /// <summary>
    /// A new kind of dish the pot can produce, in all three of the qualities it comes in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE DISH IS ONE PICTURE, NOT ONE PICTURE PER PAIR. Core Keeper draws a single sprite for a
    /// dish and recolours it from whichever two ingredients went in, so fifteen dishes cover every
    /// combination the game has. A dish authored here is one more of those: draw the picture once,
    /// in the eight colours the framework hands you, and every pair that leads to it comes out
    /// looking like what went in.
    /// </para>
    /// <para>
    /// THE EIGHT COLOURS ARE NOT DECORATION. Four of them are the region that takes the leading
    /// ingredient's colours and four are the region that takes the second's. A pixel painted in any
    /// other colour is left exactly as drawn, which is right for a plate or a garnish and wrong for
    /// the food itself. The dish page checks the picture against the palette and names the colours
    /// it did not recognise.
    /// </para>
    /// <para>
    /// THREE QUALITIES, ONE ASSET. The game keeps ordinary, rare and epic as three separate objects
    /// cross-linked to each other, and a player reaches the better two through cooking skill and
    /// through golden ingredients. Authoring them as three separate things would mean typing the
    /// same dish out three times and getting the cross-links right by hand, so this is one asset and
    /// the framework emits the three.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Dish")]
    public sealed class DimensionDishAsset : ScriptableObject
    {
        [SerializeField] private string dishId = "mod:dish";

        [Tooltip("What the player sees at the end of the dish's name, after the two ingredients.")]
        [SerializeField] private string displayName = "Dish";

        [Tooltip("The line under the name in the tooltip.")]
        [SerializeField] private string description = string.Empty;

        [Header("The picture")]
        [Tooltip("The dish drawn in the eight template colours. 16x16 PNG, Pixels Per Unit 16, Point (no) filter.")]
        [SerializeField] private Sprite baseSprite;

        [Tooltip("The rare version's picture. Leave empty to reuse the ordinary one.")]
        [SerializeField] private Sprite rareSprite;

        [Tooltip("The epic version's picture. Leave empty to reuse the ordinary one.")]
        [SerializeField] private Sprite epicSprite;

        [Header("How filling it is")]
        [Tooltip("Hunger the ordinary dish restores. The game's own dishes sit between 5 and 20.")]
        [Min(0)]
        [SerializeField] private int hunger = 15;

        [Tooltip("Hunger the rare version restores.")]
        [Min(0)]
        [SerializeField] private int rareHunger = 15;

        [Tooltip("Hunger the epic version restores.")]
        [Min(0)]
        [SerializeField] private int epicHunger = 15;

        [Header("What the better versions add")]
        [Tooltip("Effects the rare version gives on top of what its ingredients give. Leave empty for none.")]
        [SerializeField] private DimensionItemEffect[] extraOnRare = new DimensionItemEffect[0];

        [Tooltip("Effects the epic version gives on top of what its ingredients give. Leave empty for none.")]
        [SerializeField] private DimensionItemEffect[] extraOnEpic = new DimensionItemEffect[0];

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        /// <summary>The id of the ordinary version, which is also this dish's own id.</summary>
        public string DishId
        {
            get { return dishId ?? string.Empty; }
        }

        /// <summary>The item id the rare version is generated under.</summary>
        public string RareItemId
        {
            get { return RareItemIdFor(DishId); }
        }

        /// <summary>The item id the epic version is generated under.</summary>
        public string EpicItemId
        {
            get { return EpicItemIdFor(DishId); }
        }

        /// <summary>
        /// The rare version's item id: the dish's own id with "Rare" on the end.
        /// </summary>
        /// <remarks>
        /// THE SUFFIX IS LOAD-BEARING, not a naming habit. When the game builds a dish's name it
        /// strips a trailing "Rare" or "Epic" off the object's name before looking up
        /// <c>Items/&lt;name&gt;</c>, which is how all three qualities of CookedSoup share one term.
        /// A tier id spelled any other way sends each quality to a term of its own, and the two
        /// better ones come out showing a raw id in place of the dish's name.
        /// </remarks>
        public static string RareItemIdFor(string dishId)
        {
            return string.IsNullOrEmpty(dishId) ? string.Empty : dishId + "Rare";
        }

        /// <summary>The epic version's item id, suffixed for the same reason.</summary>
        public static string EpicItemIdFor(string dishId)
        {
            return string.IsNullOrEmpty(dishId) ? string.Empty : dishId + "Epic";
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public Sprite BaseSprite
        {
            get { return baseSprite; }
        }

        /// <summary>The rare version's picture, falling back to the ordinary one.</summary>
        public Sprite RareSprite
        {
            get { return rareSprite != null ? rareSprite : baseSprite; }
        }

        /// <summary>The epic version's picture, falling back to the ordinary one.</summary>
        public Sprite EpicSprite
        {
            get { return epicSprite != null ? epicSprite : baseSprite; }
        }

        public int Hunger
        {
            get { return hunger < 0 ? 0 : hunger; }
        }

        public int RareHunger
        {
            get { return rareHunger < 0 ? 0 : rareHunger; }
        }

        public int EpicHunger
        {
            get { return epicHunger < 0 ? 0 : epicHunger; }
        }

        /// <summary>Extra effects on the rare version, with the blank entries dropped.</summary>
        public DimensionItemEffect[] ExtraOnRare
        {
            get { return Compact(extraOnRare); }
        }

        /// <summary>Extra effects on the epic version, with the blank entries dropped.</summary>
        public DimensionItemEffect[] ExtraOnEpic
        {
            get { return Compact(extraOnEpic); }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        private static DimensionItemEffect[] Compact(DimensionItemEffect[] effects)
        {
            if (effects == null)
            {
                return new DimensionItemEffect[0];
            }

            System.Collections.Generic.List<DimensionItemEffect> kept =
                new System.Collections.Generic.List<DimensionItemEffect>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null && effects[i].NamesAnEffect)
                {
                    kept.Add(effects[i]);
                }
            }

            return kept.ToArray();
        }
    }
}
