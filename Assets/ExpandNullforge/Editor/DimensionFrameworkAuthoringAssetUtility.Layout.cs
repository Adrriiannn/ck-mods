using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Making a layout, adding a region or a ring to it, and publishing a version of it.
    /// </summary>
    internal static partial class DimensionFrameworkAuthoringAssetUtility
    {
        public static DimensionFrameworkAuthoringAssetActionResult CreateLayoutTemplate(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before creating a layout.");
            }

            DimensionLayoutTemplateAsset layout =
                CreateAsset<DimensionLayoutTemplateAsset>(
                    template,
                    "Layout",
                    "Layout");
            string biomeId = ResolvePrimaryBiomeId(template);
            layout.ApplySingleBiomeSquarePreset(
                biomeId,
                biomeId,
                64);
            SetObjectReference(template, "layoutTemplate", layout);
            SaveAndSelect(layout);
            return Success(layout, "Created and assigned a layout template.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult AddLayoutRegion(
            DimensionLayoutTemplateAsset layout,
            BiomeTemplateAsset biome)
        {
            if (layout == null)
            {
                return Failure("Assign or create a layout template before adding regions.");
            }

            SerializedObject serialized = new SerializedObject(layout);
            SerializedProperty regions = serialized.FindProperty("regions");
            if (regions == null || !regions.isArray)
            {
                return Failure("The layout template does not expose editable manual regions.");
            }

            int index = regions.arraySize;
            regions.InsertArrayElementAtIndex(index);
            SerializedProperty element = regions.GetArrayElementAtIndex(index);
            string biomeId = biome == null ? string.Empty : biome.BiomeId;
            string regionId = "region-" + (index + 1).ToString();
            SetRelativeString(element, "regionId", regionId);
            SetRelativeString(element, "biomeId", biomeId);
            SetRelativeString(element, "zoneId", biomeId);
            SetRelativeString(element, "displayName", string.IsNullOrEmpty(biomeId) ? "Region" : biome.DisplayName + " Region");
            SetRelativeVector2Int(element, "localMin", new Vector2Int(-64 + index * 128, -64));
            SetRelativeVector2Int(element, "localMaxExclusive", new Vector2Int(64 + index * 128, 64));
            SetRelativeInt(element, "priority", index);
            SetRelativeBool(element, "enabled", true);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            SaveAndSelect(layout);
            return Success(layout, "Added a layout region for " + (string.IsNullOrEmpty(biomeId) ? "the selected layout" : biomeId) + ".");
        }

        /// <summary>
        /// Adds a radial ring, placed just outside whatever the layout already reaches.
        /// </summary>
        /// <remarks>
        /// Appended beyond the current outermost ring rather than at a fixed radius, because a new ring
        /// dropped on top of an existing one is invisible on the map — the author sees nothing happen
        /// and clicks again. Starting outside means the ring you just added is the ring you can see.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult AddLayoutRing(
            DimensionLayoutTemplateAsset layout,
            BiomeTemplateAsset biome)
        {
            if (layout == null)
            {
                return Failure("Assign or create a layout template before adding rings.");
            }

            SerializedObject serialized = new SerializedObject(layout);
            SerializedProperty rings = serialized.FindProperty("radialRings");
            if (rings == null || !rings.isArray)
            {
                return Failure("The layout template does not expose editable radial rings.");
            }

            int outermost = 0;
            DimensionLayoutRadialRingDefinition[] existing = layout.RadialRings;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    outermost = Mathf.Max(outermost, existing[i].MaxRadiusTiles);
                }
            }

            int index = rings.arraySize;
            rings.InsertArrayElementAtIndex(index);
            SerializedProperty element = rings.GetArrayElementAtIndex(index);
            string biomeId = biome == null ? string.Empty : biome.BiomeId;

            SetRelativeString(element, "ringId", "ring-" + (index + 1).ToString());
            SetRelativeString(element, "biomeId", biomeId);
            SetRelativeString(element, "zoneId", biomeId);
            SetRelativeString(
                element,
                "displayName",
                string.IsNullOrEmpty(biomeId) ? "Ring" : biome.DisplayName + " Ring");
            SetRelativeInt(element, "minRadiusTiles", outermost);
            SetRelativeInt(element, "maxRadiusTiles", outermost + 128);
            SetRelativeBool(element, "limitToAngleRange", false);
            SetRelativeFloat(element, "startAngleDegrees", 0f);
            SetRelativeFloat(element, "endAngleDegrees", 360f);
            SetRelativeInt(element, "priority", index);
            SetRelativeBool(element, "enabled", true);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            SaveAndSelect(layout);
            return Success(
                layout,
                "Added a ring from " + outermost + " to " + (outermost + 128) + " tiles.");
        }

        /// <summary>
        /// Deletes one ring or rectangle, addressed the way the Studio reported it.
        /// </summary>
        /// <remarks>
        /// The id carries which array it came from ("ring:2", "region:0") rather than a bare index,
        /// because both lists are on screen at once in Hybrid mode and an index alone would happily
        /// delete the wrong one.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult RemoveLayoutEntry(
            DimensionLayoutTemplateAsset layout,
            string entryId)
        {
            if (layout == null || string.IsNullOrEmpty(entryId))
            {
                return Failure("Nothing to remove.");
            }

            int separator = entryId.IndexOf(':');
            if (separator <= 0 || separator >= entryId.Length - 1)
            {
                return Failure("Could not work out which layout entry to remove.");
            }

            string kind = entryId.Substring(0, separator);
            int index;
            if (!int.TryParse(entryId.Substring(separator + 1), out index) || index < 0)
            {
                return Failure("Could not work out which layout entry to remove.");
            }

            string propertyName = kind == "ring" ? "radialRings" : "regions";
            SerializedObject serialized = new SerializedObject(layout);
            SerializedProperty array = serialized.FindProperty(propertyName);
            if (array == null || !array.isArray || index >= array.arraySize)
            {
                return Failure("That layout entry no longer exists.");
            }

            array.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            SaveAndSelect(layout);
            return Success(layout, kind == "ring" ? "Removed a ring." : "Removed a rectangle.");
        }

        /// <summary>
        /// Records the layout's current shape as a new published version.
        /// </summary>
        /// <remarks>
        /// <para>
        /// What is stored is the COMPILED region list, produced here by the same compiler the build
        /// runs. That is the whole point: a save pinned to this version can be regenerated from these
        /// rectangles even after the author rebuilds the layout out of entirely different rings.
        /// </para>
        /// <para>
        /// A layout that currently compiles to nothing is refused rather than published as an empty
        /// version — a world pinned to an empty layout would generate no biomes at all, which is a far
        /// worse outcome than being told to fix the layout first.
        /// </para>
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult PublishLayoutVersion(
            DimensionTemplateAsset template,
            DimensionLayoutTemplateAsset layout)
        {
            if (template == null || layout == null)
            {
                return Failure("Select a Dimension Asset with a layout before publishing.");
            }

            DimensionCompiledGenerationPlan plan = DimensionTemplateCompiler.Compile(template);
            if (plan.BiomeRegions == null || plan.BiomeRegions.Count == 0)
            {
                return Failure(
                    "This layout does not currently produce any biome regions, so there is nothing to " +
                    "publish. Fix the problems listed under the map first.");
            }

            DimensionLayoutArchivedRegion[] archived =
                new DimensionLayoutArchivedRegion[plan.BiomeRegions.Count];
            for (int i = 0; i < plan.BiomeRegions.Count; i++)
            {
                DimensionCompiledBiomeRegion region = plan.BiomeRegions[i];
                archived[i] = new DimensionLayoutArchivedRegion(
                    region.SourceTemplateId,
                    region.BiomeId,
                    region.ZoneId,
                    region.DisplayName,
                    region.LocalBounds,
                    region.Priority);
            }

            Undo.RecordObject(layout, "Publish layout version");

            string error;
            if (!layout.TryPublishVersion(
                    archived,
                    System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    string.Empty,
                    out error))
            {
                return Failure(error);
            }

            EditorUtility.SetDirty(layout);
            SaveAndSelect(layout);
            return Success(
                layout,
                "Published layout v" + layout.LayoutVersion + " with " + archived.Length +
                " regions. Worlds generated from now on remember this version.");
        }
    }
}
