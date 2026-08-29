# The framework's built-in ids are not protected, in a system that reads as though they are

`NullforgeDimensionService` has five `IsProtected*Id` predicates. One has a body. Four return
`false`. Eight branches in the service consult the four, produce a `…-protected` failure that can
never fire, and read to every subsequent maintainer as a guarantee that is not there.

This is not dead code that can be swept. It is a bug wearing dead code's clothes, and it needs an
owner's decision (D2). This page is the evidence.

## The five predicates

```csharp
// NullforgeDimensionService.RelationshipCleanup.cs
private bool IsProtectedMarkerId(string markerId)   { return false; }

private bool IsProtectedMapLayerId(string layerId)
{
    return string.Equals(layerId, BuiltInOverworldMapLayerId, StringComparison.Ordinal);
}

private bool IsProtectedAnchorId(string anchorId)   { return false; }

// NullforgeDimensionService.PortalInternals.cs
private bool IsProtectedPortalId(string portalId)   { return false; }

// NullforgeDimensionService.Starters.cs
private static bool IsProtectedStarterId(string starterId) { return false; }
```

`IsProtectedMapLayerId` is the template. It compares against one constant:

```csharp
// NullforgeDimensionService.BuiltIns.cs:45
private const string BuiltInOverworldMapLayerId = "corekeeper:map-layer-overworld";
```

Its two branches work. The overworld map layer genuinely cannot be moved to another dimension and
genuinely cannot be removed. That proves the other four were meant to do the same thing, and that
somebody knew how.

## The eight branches that do nothing

| File:line | Branch | Message it would produce |
|---|---|---|
| `ContentManifests.cs:1506` | `IsProtectedMarkerId(marker.MarkerId) && !MarkerAnchorEquals(...)` | — |
| `ContentManifests.cs:1561` | `IsProtectedAnchorId(anchor.AnchorId) && !AnchorLocationEquals(...)` | — |
| `MapMarkersAnchors.cs:108` | `IsProtectedMarkerId(marker.MarkerId) && …` | — |
| `MapMarkersAnchors.cs:184` | `IsProtectedMarkerId(markerId)` | remove refused |
| `MapMarkersAnchors.cs:323` | `IsProtectedAnchorId(anchor.AnchorId) && !AnchorLocationEquals(...)` | — |
| `MapMarkersAnchors.cs:403` | `IsProtectedAnchorId(anchorId)` | remove refused |
| `Portals.cs:138` | `IsProtectedPortalId(portalId)` | `portal-protected` — *"Built-in dimension portals cannot be removed."* |
| `Starters.cs:79` | `IsProtectedStarterId(starterId)` | `starter-protected` — *"Built-in dimension starters cannot be removed."* |

Compare the two that work:

```csharp
// MapPresentation.cs:215 — reachable
if (IsProtectedMapLayerId(layerId))
{
    result = DimensionOperationResult.Failed(
        "map-layer-protected", "Built-in map layers cannot be removed.");
    return false;
}
```

```csharp
// Portals.cs:138 — identical shape, unreachable
if (IsProtectedPortalId(portalId))
{
    result = DimensionOperationResult.Failed(
        "portal-protected", "Built-in dimension portals cannot be removed.");
    return false;
}
```

Nothing distinguishes them at the call site. Only the predicate body differs.

## What that means today

**Any mod, and any bug in the framework's own code, can remove the framework's built-in portals,
markers, anchors and starters, and the service will report success.** The two mutation-guard
branches (`MarkerAnchorEquals`, `AnchorLocationEquals`) are also inert, so a built-in marker or
anchor can be silently relocated by a content manifest apply.

The failure is quiet in the way this project's worst defects are quiet: the operation succeeds, the
error string exists in the source so a search for it looks reassuring, and the consequence is a
missing portal in somebody's world three sessions later.

## Why it cannot be fixed by reading

`IsProtectedMapLayerId` had one id to compare against, and it was a named constant sitting in
`BuiltIns.cs`. **The other four have no equivalent list.** `NullforgeDimensionService.BuiltIns.cs`
(101 lines) declares:

```
BuiltInTravelRequirementAccessProviderId   "expandnullforge:travel-requirement-access"
BuiltInProgressFlagRequirementEvaluatorId  "expandnullforge:progress-flag-requirements"
BuiltInOverworldMapLayerId                 "corekeeper:map-layer-overworld"
BuiltInFrameworkContentPack                the "expandnullforge" content pack
OverworldDefinition                        DimensionIds.Overworld
```

and no built-in portal, marker, anchor or starter id at all. The four files that would have declared
them — `DimensionBuiltInPortalContent.cs`, `DimensionBuiltInTravelActions.cs`,
`DimensionPortalCraftingRegistry.cs`, `NullforgeDimensionService.BuiltInContracts.cs` — were deleted
(see `amputated-features.md`).

So the honest reading is: **the framework used to ship built-in portals and starters, the predicates
guarded them, the content went away, and the predicates were reduced to `return false` rather than
removed.** Whether the framework should ship any again is a product question.

## The two ways to close it

**Supply the lists.** Add the built-in ids to `BuiltIns.cs` as constants beside
`BuiltInOverworldMapLayerId`, and give each predicate the same one-line `string.Equals` body. This
is the right answer if the framework ships built-in content again. It is a few lines.

**Delete the four predicates and all eight branches, together, and say so.** Write into
`BuiltIns.cs` — beside the one surviving constant — that the overworld map layer is the only
protected id the framework has, and that portals, markers, anchors and starters registered by the
framework are removable like anybody else's. This is the right answer if the framework ships no
built-in content, which is what the code says today.

**What must not happen is leaving it.** A predicate named `IsProtectedPortalId` that returns `false`
is worse than no predicate: the next person to read `Portals.cs:138` will believe the built-ins are
safe, and will be wrong.

One thing to be careful of if the delete path is taken: `ContentManifests.cs:1506` and `:1561` and
`MapMarkersAnchors.cs:108` and `:323` are `&&` conditions whose *second* half is a real equality
check. Removing the branch removes that check too. Read each of the four before cutting.
