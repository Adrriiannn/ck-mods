using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class — Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Recipe")]
    public sealed class DimensionRecipeAsset : ScriptableObject
    {
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
