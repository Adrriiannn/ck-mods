using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class — Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Recipe")]
    public sealed class DimensionRecipeAsset : ScriptableObject
    {
        /// <summary>
        /// How many recipes a mod may add to the by-hand list before the game runs out of room.
        /// </summary>
        /// <remarks>
        /// Core Keeper has no separate hand-crafting list: the player is itself a crafting station
        /// (<c>ObjectID.Player</c>) and already carries six recipes of its own. The crafting window
        /// draws at most three panes of six — the game's UI prefab holds exactly three
        /// <c>SimpleCraftingUI</c>s — and the by-hand list cannot be split into pages, because
        /// paging is read by <c>CraftingBuilding</c>, which is a placed object rather than the
        /// player. Six of the eighteen are already spoken for, so twelve is what a mod may add;
        /// past that the game logs "Not enough SimpleCraftingUIs" and shows nothing.
        /// </remarks>
        public const int HandCraftingRecipeLimit = 12;

        [SerializeField] private string recipeId = "mod:recipe";
        [SerializeField] private string displayName = "Recipe";
        [SerializeField] private string outputItemId = string.Empty;
        [SerializeField] private int outputAmount = 1;
        [SerializeField] private string craftingStationId = string.Empty;
        [SerializeField] private float craftTimeSeconds;
        [SerializeField] private bool enabled = true;
        [SerializeField] private DimensionRecipeIngredientTemplate[] ingredients =
            new DimensionRecipeIngredientTemplate[0];
        [SerializeField] private string notes = string.Empty;

        public string RecipeId
        {
            get { return recipeId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string OutputItemId
        {
            get { return outputItemId ?? string.Empty; }
        }

        public int OutputAmount
        {
            get { return Mathf.Max(1, outputAmount); }
        }

        public string CraftingStationId
        {
            get { return craftingStationId ?? string.Empty; }
        }

        public float CraftTimeSeconds
        {
            get { return Mathf.Max(0f, craftTimeSeconds); }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public DimensionRecipeIngredientTemplate[] Ingredients
        {
            get { return ingredients ?? new DimensionRecipeIngredientTemplate[0]; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }
    }
}
