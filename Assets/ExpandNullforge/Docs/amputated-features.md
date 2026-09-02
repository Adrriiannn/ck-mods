# The features that were removed, and what they left behind

`CoreKeeperModSDK/DimensionsAPI.csproj` was the project file of an assembly this framework used to
be called. Unity stopped regenerating it when the assembly was renamed, so it froze: 165 `Compile`
rows describing the tree as it stood on the day of the rename. Thirty of those rows name files that
are no longer on disk.

That makes the stale list the only written record of which features were taken out. This page copies
it down before the file is deleted, because a deleted `.csproj` takes the map with it.

**What this page is not.** It is not a plan to restore anything, and none of these files exist to be
restored from — only their names and their folders survived. Treat it as the answer to "was there
ever a thing that did X, and what is still wired for it".

## The four amputated registries

Thirteen of the thirty files are one story: four content registries were built end to end and then
the acting half was deleted, leaving the authoring half in place.

| Registry | Files removed |
|---|---|
| Resource nodes | `Scripts/Authoring/ResourceNodeTemplateAsset.cs`, `Scripts/Foundation/NullforgeDimensionService.ResourceNodes.cs`, `…ResourceNodeInternals.cs` |
| Environment profiles | `Scripts/Authoring/EnvironmentProfileTemplateAsset.cs`, `Scripts/Foundation/NullforgeDimensionService.EnvironmentProfiles.cs`, `…ZoneEnvironmentInternals.cs` |
| Generation tables | `Scripts/Authoring/GenerationTableTemplateAsset.cs`, `Scripts/Authoring/GenerationTableTemplatePreviewUtility.cs`, `Scripts/Foundation/NullforgeDimensionService.GenerationTables.cs`, `…GenerationTableInternals.cs` |
| Spawn rules | `Scripts/Authoring/SpawnRuleTemplateAsset.cs`, `Scripts/Foundation/NullforgeDimensionService.SpawnRules.cs`, `…SpawnRuleInternals.cs` |

The pattern is the same each time: one `*TemplateAsset` the modder filled in, and two service
partials — the public methods and their internals — that turned it into something the game read.
Both halves of the service went; the asset went with them.

**What is still standing.** None of the four `*TemplateAsset` types are referenced anywhere in the
tree any more. What survived is thinner than it looks from a distance and is worth naming exactly:

- `DimensionAuthoringContentSummaryKind.ResourceNode` / `.EnvironmentProfile` and
  `DimensionAuthoringPreviewLayerKind.ResourceNode` — counted in the authoring summary, drawn as a
  preview layer, and tested for spatial conflicts by `DimensionAuthoringSpatialConflict`.
- `DimensionBiomeGenerationBudget.ResourceNodeCount` / `.TotalResourceNodeCount` — a budget for a
  thing nothing places.
- `BiomeTemplateAsset` still authors environment-profile ids; they are carried into
  `DimensionRuntimeManifestSnapshot`, filtered in `NullforgeDimensionService.BiomeInternals`, and
  persisted by `DimensionWorldRegistry`.
- `NullforgeDimensionService.RuntimeRecords.cs` keeps two of the four stub dictionaries —
  `ResourceNodeIds` and `EnvironmentProfileIds` — in the tracked-record class.

Generation tables and spawn rules left no id surface at all: `GenerationTableId` and `SpawnRuleId`
occur nowhere.

So the honest statement of the gap is: **a modder can still author environment-profile ids on a
biome and see resource nodes counted, and nothing on the other end acts on either.** That is the
decision this page exists to inform — either the service partials come back or the authoring surface
goes, and it is one decision, not four separate tidyings.

## Travel feedback

Three files, and they are the read side of a subsystem whose write side is still running:

- `Scripts/Networking/DimensionTravelFeedbackActions.cs`
- `Scripts/Networking/DimensionTravelFeedbackPresenter.cs`
- `Scripts/UI/DimensionTravelFeedbackOverlay.cs`

`Scripts/Networking/DimensionTravelFeedbackState.cs` and its API DTOs are still here and still
publish a snapshot on every travel event; the overlay that displayed them, the presenter that fed
it, and the actions it offered are gone. Nothing reads the state today. The severities, phases and
retention rules that survive in the state class are written up separately in
`travel-feedback-spec.md`.

## The rename residue

Six files named `Nullforge*` that have a `Dimension*` counterpart today, or no counterpart at all:

| Removed | What stands in its place |
|---|---|
| `Scripts/Portals/NullforgePortal.cs` | `Scripts/Portals/DimensionPortal.cs` |
| `Scripts/Portals/NullforgePortalIds.cs` | nothing. `API/Scripts/DimensionPortalIds.cs` was the empty class the rename left behind, and it is deleted too |
| `Scripts/UI/NullforgeCoordinatePresentation.cs` | `Scripts/UI/DimensionCoordinatePresentation.cs` |
| `Scripts/Zones/NullforgeZoneProvider.cs` | nothing by that shape; zone providers are registered through the service |
| `Scripts/Generation/NullforgeGenerationRuntime.cs` | nothing by that name |
| `Scripts/Generation/NullforgeSafePlatformGenerationSystem.cs`, `…GenerationProvider.cs` | `Scripts/Generation/DimensionSafePlatformGenerationProvider.cs` — the provider came across, the system did not |

The safe-platform pair is the one worth a second look: the provider survived the rename and is live
(`ExpandNullforgeModEntry` registers it, `DimensionGenerationProviderIds` names it), and the system
beside it did not.

## Built-in content

Four files that shipped content the framework itself owned, rather than content a mod authored:

- `Scripts/Networking/DimensionBuiltInTravelActions.cs`
- `Scripts/Portals/DimensionBuiltInPortalContent.cs`
- `Scripts/Portals/DimensionPortalCraftingRegistry.cs`
- `Scripts/Foundation/NullforgeDimensionService.BuiltInContracts.cs`

None of the four names occur anywhere in the tree. The framework's built-in ids are still spoken of
in the service — that is the subject of the `IsProtected*Id` question in `protected-ids-decision.md`
— but the content that defined them is gone.

## Biome presets

Three files that turned a short authored recipe into a filled biome:

- `Scripts/Authoring/BiomeContentPresetAsset.cs`
- `Scripts/Authoring/BiomePaletteTemplateAsset.cs`
- `Scripts/Authoring/DimensionSemanticObjectTableBuilder.cs`

`API/Scripts/Authoring/DimensionBiomeAuthoringRecipe.cs` is the surviving type in that area and does part of
the same job. No reference to any of the three names remains.

## The full list, as the csproj had it

```
Scripts/Authoring/BiomeContentPresetAsset.cs
Scripts/Authoring/BiomePaletteTemplateAsset.cs
Scripts/Authoring/DimensionSemanticObjectTableBuilder.cs
Scripts/Authoring/EnvironmentProfileTemplateAsset.cs
Scripts/Authoring/GenerationTableTemplateAsset.cs
Scripts/Authoring/GenerationTableTemplatePreviewUtility.cs
Scripts/Authoring/ResourceNodeTemplateAsset.cs
Scripts/Authoring/SpawnRuleTemplateAsset.cs
Scripts/Foundation/NullforgeDimensionService.BuiltInContracts.cs
Scripts/Foundation/NullforgeDimensionService.EnvironmentProfiles.cs
Scripts/Foundation/NullforgeDimensionService.GenerationTableInternals.cs
Scripts/Foundation/NullforgeDimensionService.GenerationTables.cs
Scripts/Foundation/NullforgeDimensionService.ResourceNodeInternals.cs
Scripts/Foundation/NullforgeDimensionService.ResourceNodes.cs
Scripts/Foundation/NullforgeDimensionService.SpawnRuleInternals.cs
Scripts/Foundation/NullforgeDimensionService.SpawnRules.cs
Scripts/Foundation/NullforgeDimensionService.ZoneEnvironmentInternals.cs
Scripts/Generation/NullforgeGenerationRuntime.cs
Scripts/Generation/NullforgeSafePlatformGenerationProvider.cs
Scripts/Generation/NullforgeSafePlatformGenerationSystem.cs
Scripts/Networking/DimensionBuiltInTravelActions.cs
Scripts/Networking/DimensionTravelFeedbackActions.cs
Scripts/Networking/DimensionTravelFeedbackPresenter.cs
Scripts/Portals/DimensionBuiltInPortalContent.cs
Scripts/Portals/DimensionPortalCraftingRegistry.cs
Scripts/Portals/NullforgePortal.cs
Scripts/Portals/NullforgePortalIds.cs
Scripts/UI/DimensionTravelFeedbackOverlay.cs
Scripts/UI/NullforgeCoordinatePresentation.cs
Scripts/Zones/NullforgeZoneProvider.cs
```

The other 135 rows in that file name files that still exist and are compiled by
`ExpandNullforge.csproj` today. Nothing was lost by deleting it beyond this list.
