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
        [SerializeField] private string displayName = "Workbench";
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string iconId = string.Empty;
        [SerializeField] private DimensionRecipeAsset[] recipes =
            new DimensionRecipeAsset[0];
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

        public string ObjectId
        {
            get { return objectId ?? string.Empty; }
        }

        public string IconId
        {
            get { return iconId ?? string.Empty; }
        }

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
