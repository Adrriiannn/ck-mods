using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class — Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Workbench")]
    public sealed class DimensionWorkbenchAsset : ScriptableObject
    {
        [SerializeField] private string workbenchId = "mod:workbench";

        [Tooltip("What the player sees this station called, in their inventory and once it is placed.")]
        [SerializeField] private string displayName = "Workbench";

        [Tooltip("The line under the name in its tooltip. Leave empty for no second line.")]
        [TextArea(2, 4)]
        [SerializeField] private string description = string.Empty;

        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string iconId = string.Empty;

        [Header("What it looks like")]
        [Tooltip("Its picture where it stands, once it is placed.")]
        [SerializeField] private Sprite sprite;

        [Tooltip("Its icon in inventories. Falls back to the picture when empty.")]
        [SerializeField] private Sprite icon;

        [Header("The station itself")]
        [Tooltip("What kind of station it is. A Workbench only shows a recipe list — that is 50 of the game's 85 stations.")]
        [SerializeField] private DimensionWorkbenchKind kind = DimensionWorkbenchKind.Workbench;

        [Tooltip("Generate a placeable station object for this, rather than only grouping recipes onto an object that already exists.")]
        [SerializeField] private bool generatesItsOwnObject = true;

        [Tooltip("Its rarity colour in the world and in tooltips.")]
        [SerializeField] private string rarityId = string.Empty;

        [Tooltip("How many hits to break it. Vanilla stations take a few.")]
        [Min(1)]
        [SerializeField] private int hitsToBreak = 3;

        [Tooltip("Faces the way the player was looking when it was placed. Most vanilla stations do.")]
        [SerializeField] private bool facesPlacementDirection = true;

        [Tooltip("What it does with electricity, if anything. Vanilla has powered machines — generators, drills — so a station can be one.")]
        [SerializeField] private DimensionWiringTemplate wiring = new DimensionWiringTemplate();
        [SerializeField] private DimensionRecipeAsset[] recipes =
            new DimensionRecipeAsset[0];
        [Tooltip("Where it is allowed to be put down, and how it behaves as the player lines it up.")]
        [SerializeField] private DimensionPlacementRulesTemplate placementRules = new DimensionPlacementRulesTemplate();

        [Tooltip("The small things it is, or does — one tickbox each.")]
        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits =
            new DimensionSimpleTraitsTemplate();

        [Tooltip("What happens when a player walks up to it and uses it.")]
        [SerializeField] private DimensionInteractionTemplate interaction =
            new DimensionInteractionTemplate();

        [Tooltip("It shows the looping effect on its output slot while it works.")]
        [SerializeField] private bool showsALoopingEffectWhileWorking;

        [Tooltip("A whole inventory of ingredients counts as one craft rather than many.")]
        [SerializeField] private bool wholeInventoryIsOneCraft;

        [Tooltip("For an extractor: which category tag it pulls out of things.")]
        [SerializeField] private string extractsCategoryTag = string.Empty;

        [Tooltip("Fewest and most it extracts by default.")]
        [SerializeField] private Vector2 extractedAmountRange = Vector2.zero;

        [Tooltip("Shortest and longest a default craft takes.")]
        [SerializeField] private Vector2 defaultCraftTimeRange = Vector2.zero;

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string WorkbenchId
        {
            get { return workbenchId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        /// <summary>Its picture where it stands.</summary>
        public Sprite Sprite
        {
            get { return sprite; }
        }

        /// <summary>
        /// Its icon in inventories, which is the picture again when no separate icon was given.
        /// </summary>
        /// <remarks>
        /// The fallback is not a convenience: <c>InventoryItemAuthoring.icon</c> is the only place
        /// <c>ObjectAuthoring.ObjectAuthoringToObjectInfo</c> reads an icon from, so a station with
        /// neither draws as an empty square in every slot it ever appears in.
        /// </remarks>
        public Sprite Icon
        {
            get { return icon != null ? icon : sprite; }
        }

        /// <summary>
        /// The Workbench a recipe means when it names <paramref name="stationId"/>, or null when no
        /// Workbench in <paramref name="workbenches"/> answers to that name.
        /// </summary>
        /// <remarks>
        /// A creator picks a Workbench by the id they gave it, but a Workbench may also carry an
        /// explicit object id, and either is a reasonable thing to have typed. Both the dashboard's
        /// check and the emitter that registers the recipe ask this same question, and they have to
        /// get the same answer or a recipe would validate clean and then register nowhere.
        /// </remarks>
        public static DimensionWorkbenchAsset FindByStationId(
            DimensionWorkbenchAsset[] workbenches,
            string stationId)
        {
            if (workbenches == null || string.IsNullOrEmpty(stationId))
            {
                return null;
            }

            for (int i = 0; i < workbenches.Length; i++)
            {
                DimensionWorkbenchAsset workbench = workbenches[i];
                if (workbench == null)
                {
                    continue;
                }

                if (string.Equals(workbench.WorkbenchId, stationId, System.StringComparison.Ordinal) ||
                    (!string.IsNullOrEmpty(workbench.ObjectId) &&
                     string.Equals(workbench.ObjectId, stationId, System.StringComparison.Ordinal)))
                {
                    return workbench;
                }
            }

            return null;
        }

        public string ObjectId
        {
            get { return objectId ?? string.Empty; }
        }

        public string IconId
        {
            get { return iconId ?? string.Empty; }
        }

        public DimensionWorkbenchKind Kind
        {
            get { return kind; }
        }

        /// <summary>Whether the framework makes the station object, or the author supplied one.</summary>
        /// <remarks>
        /// Grouping recipes onto a vanilla station is a real and common thing to want, so this stays
        /// optional. What it must not do is silently produce nothing: a workbench that generates no
        /// object and names no existing one has recipes with nowhere to appear.
        /// </remarks>
        public bool GeneratesItsOwnObject
        {
            get { return generatesItsOwnObject; }
        }

        public string RarityId
        {
            get { return rarityId ?? string.Empty; }
        }

        public int HitsToBreak
        {
            get { return hitsToBreak < 1 ? 1 : hitsToBreak; }
        }

        /// <summary>What it does with electricity, if anything.</summary>
        public DimensionWiringTemplate Wiring
        {
            get { return wiring ?? new DimensionWiringTemplate(); }
        }

        public bool FacesPlacementDirection
        {
            get { return facesPlacementDirection; }
        }

        /// <summary>Whether its recipes have nowhere to appear.</summary>
        public bool HasNowhereToShowRecipes
        {
            get { return !generatesItsOwnObject && string.IsNullOrEmpty(ObjectId); }
        }

        public DimensionPlacementRulesTemplate PlacementRules
        {
            get { return placementRules ?? (placementRules = new DimensionPlacementRulesTemplate()); }
        }

        /// <summary>The small things it simply is, or simply does.</summary>
        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }

        /// <summary>What happens when a player uses it.</summary>
        public DimensionInteractionTemplate Interaction
        {
            get { return interaction ?? (interaction = new DimensionInteractionTemplate()); }
        }

        public bool ShowsALoopingEffectWhileWorking { get { return showsALoopingEffectWhileWorking; } }

        public bool WholeInventoryIsOneCraft { get { return wholeInventoryIsOneCraft; } }

        public string ExtractsCategoryTag { get { return extractsCategoryTag ?? string.Empty; } }

        public Vector2 ExtractedAmountRange { get { return extractedAmountRange; } }

        public Vector2 DefaultCraftTimeRange { get { return defaultCraftTimeRange; } }

        public bool Enabled
        {
            get { return enabled; }
        }

        public DimensionRecipeAsset[] Recipes
        {
            get { return recipes ?? new DimensionRecipeAsset[0]; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                WorkbenchId,
                "object",
                DisplayName + " Object",
                DimensionAssetReferenceKind.Object,
                ObjectId,
                string.Empty,
                0,
                Enabled,
                Notes);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                WorkbenchId,
                "icon",
                DisplayName + " Icon",
                DimensionAssetReferenceKind.Icon,
                IconId,
                string.Empty,
                10,
                Enabled,
                Notes);
        }
    }
}
