# Scene authoring

## The code-first construction API that was on `SceneTemplateAsset`

`Scripts/Authoring/Assets/SceneTemplateAsset.cs` carried a complete set of methods for building a scene
template in code rather than in the inspector. Nothing ever called eight of them, and they are
deleted. The shape is written down here because it is a coherent API and somebody will want it back
the first time a mod wants to generate scene templates rather than author them by hand.

Everything on the asset is a private `[SerializeField]` with a read-only property, so **without
these methods the only way to fill a `SceneTemplateAsset` is the inspector or a `SerializedObject`
by string.** That is the gap the deletion opens.

### What was there

```csharp
// placement — mutually exclusive, each one sets the mode and the footprint together.
// ApplyAutomaticPlacement is the third of this family and is NOT deleted: it is still declared on
// SceneTemplateAsset, and DimensionFrameworkAuthoringAssetUtility.Content.cs calls it when it
// creates a scene.
void ApplyExactPlacement(Vector2Int exactLocalPosition, Vector2Int footprintSize);
void ApplyPreferredPlacement(Vector2Int preferredLocalMin,
                             Vector2Int preferredLocalMaxExclusive,
                             Vector2Int footprintSize);

// contents — each replaces the whole array with a defensive copy
void SetTriggers(IReadOnlyList<DimensionSceneTriggerTemplate> values);
void SetTiles(IReadOnlyList<DimensionSceneTileTemplate> values);
void SetSceneObjects(IReadOnlyList<DimensionSceneObjectTemplate> values);

// where it may appear in the overworld
void ConfigureOverworldSpawn(bool spawnInOverworld, string[] biomeNames,
                             int maxOccurrences, int minDistanceFromCore);

// turning the asset into what the service registers
DimensionSceneDefinition ToSceneDefinition(string dimensionId, DimensionBounds localBounds);
DimensionBounds ToRadialBounds();
```

Three siblings survive because something does call them: `ConfigureIdentity` (10 callers),
`ConfigureSpawnPolicy` and `SetAllowedBiomeIds` (1 each), `ApplyAutomaticPlacement` (1),
`ToTemplateDefinition` (2), `AddAssetReferencesTo` (8).

### The three properties worth preserving if it is rebuilt

**One call sets both the mode and the geometry.** Each `Apply*Placement` writes
`placementMode` *and* `footprintSize` in the same call, so a template cannot end up in
`ExactLocalPosition` mode with a footprint from a previous automatic layout. Any replacement that
exposes the mode and the size as separate setters loses that.

**Every value is clamped on the way in, not on the way out.** `EnsurePositiveSize` on every
footprint, `EnsureExclusiveMax` on the preferred bounds, `Mathf.Max(1, …)` on
`overworldMaxOccurrences`, `Mathf.Max(0, …)` on `minDistanceFromCore`, and `?? new string[0]` on the
biome names. The asset therefore cannot hold a degenerate value, which is why the readers do no
defending.

**The array setters copy.** `SetTriggers` / `SetTiles` / `SetSceneObjects` all go through
`CopyValues`, so the caller's list is not aliased into the asset — a template built in a loop from a
reused buffer stays correct.

### The two `To*` converters

`ToSceneDefinition(dimensionId, localBounds)` produced the placed scene: id, display name, the
dimension it lives in, the bounds, its kind, its priority, and a state of `Planned` or `Disabled`
depending on `Enabled`. `ToRadialBounds()` produced the bounds for a radial-placement template from
its min/max radius and its biome.

`ToTemplateDefinition` — the third converter, still live — is the *template* half; these two were
the *instance* half. If code-first scene building comes back, it comes back as all three.
