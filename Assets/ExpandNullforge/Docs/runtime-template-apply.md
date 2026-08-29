# Applying an authored template into the running service

`NullforgeDimensionService.Authoring.cs` holds three public methods that nothing calls, and the
third of them is the only labelled home in the framework for *"take a template a modder authored and
make it real in the live service, now."*

They are kept, not deleted, for the reason written down here. This page exists so the next reader
finds a decision rather than a puzzle.

## The three methods

| Method | What it does | Callers | Kept? |
|---|---|---:|---|
| `CompileDimensionTemplate(DimensionTemplateAsset)` | Straight through to `DimensionTemplateCompiler.Compile`. | 0 external | kept — `TryApplyDimensionTemplate` calls it |
| `TryBuildDimensionTemplateManifest(template, out manifest, out plan, out result)` | Straight through to `DimensionTemplateManifestBuilder.TryBuildManifest`. | 0 | **deleted** — a pure forwarder to a public static a caller can reach directly |
| `TryApplyDimensionTemplate(template, updateExistingRecords, out plan, out result)` | The real one. 58 lines. | 0 | kept |

"0" is measured: none of the three names occurred anywhere in the tree outside their own file. The
first two were one-line forwarders to public statics a caller could reach directly, so they cost
nothing and prove nothing. The third is not a forwarder, and that is why it stays.

## What `TryApplyDimensionTemplate` actually does

It is a seven-step pipeline, each step failing the whole thing with a code and a message:

```
compile the template                       → fail with the compiler's own code/message
apply the content pack                     (skipped when the template names none)
apply the dimension definition             template.ToDimensionDefinition()
apply the biome templates
apply the compiled biome regions           from the compiled plan, not the asset
apply the generation passes                from the compiled plan
apply the scene templates
apply the compiled scene placements        from the compiled plan
```

Two things about that shape are worth keeping whatever happens to the code.

**It splits authored input from compiled output deliberately.** Biome templates and scene templates
come off the asset; regions, passes and placements come off the `DimensionCompiledGenerationPlan`.
Anything that rebuilds this and reads all seven off the asset will silently skip the compiler's
work.

**`updateExistingRecords` is threaded to every step.** It is the difference between "register this
dimension for the first time" and "the author changed something, take the new version" — and it is
one flag for the whole operation, so a partial update where three of seven record kinds refreshed
cannot happen.

The failure mode it does have: it is not transactional. Step five failing leaves steps one to four
applied. For an editor-driven "apply my template" that is acceptable; for anything automatic it is
not, and that is the first thing to fix if this is ever promoted.

## Why it is not on an interface

`NullforgeDimensionService` implements a 46-interface umbrella (`IDimensionService`). None of the
46 declares any of these three methods, so no consumer can reach them without naming the concrete
class — which is why they read as dead to every reference check.

That is also why they are safe: nothing depends on them, so promoting them breaks nothing.

## If the capability is wanted

The framework has no `IDimensionAuthoringService`. Adding one with these three members is the
smallest honest version:

```csharp
public interface IDimensionAuthoringService
{
    DimensionCompiledGenerationPlan CompileDimensionTemplate(DimensionTemplateAsset template);

    bool TryApplyDimensionTemplate(
        DimensionTemplateAsset template,
        bool updateExistingRecords,
        out DimensionCompiledGenerationPlan compiledPlan,
        out DimensionOperationResult result);
}
```

added to the `IDimensionService` umbrella the way the other 46 are. The service already implements
both members, so it is a declaration change and nothing more. (A manifest-building member would
forward to the public `DimensionTemplateManifestBuilder.TryBuildManifest`; add it only if a consumer
asks, rather than re-adding the forwarder this cleanup removed.)

**Do not do it as tidying.** Publishing an interface is a promise to keep it, and this one currently
has no consumer asking for it. The question to answer first is whether a mod should be able to
author a dimension at runtime at all, or whether authoring is an editor-time activity that ends at a
built manifest. The code says the former was once intended; nothing in the shipped tools agrees.

## Related

`DimensionTemplateCustomizerExecutionPlan` — deleted, harvested to
`authoring-staged-execution-note.md` — had `ApplyManifest` as one of its four intents, and its
`runtime-manifest-service` step said *"the execution plan contains the request; the caller must pass
it to a manifest service instance."* This method is the other end of that sentence. They were built
to meet and never did.
