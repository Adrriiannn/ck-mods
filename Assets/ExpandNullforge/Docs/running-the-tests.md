# Running the tests

Written 2026-08-29, alongside Phase 0.5. **The suite has never been run.** Nothing in this file
reports a result; it reports how to get one, and what the result will and will not mean.

---

## How to run them

The tests are one Unity assembly, `ExpandNullforge.Editor.Tests`, at
`Assets/ExpandNullforge/Editor/Tests`. Its asmdef carries `"defineConstraints":
["UNITY_INCLUDE_TESTS"]` and `"optionalUnityReferences": ["TestAssemblies"]`, so it exists only
inside the editor and only while Unity is building test assemblies. There is nothing to install and
no runner to configure.

**In the editor.** Open the project in the Unity instance that is already running (do not launch
`Unity.exe` — that opens a newer editor with no project and a terms dialog). Then:

1. `Window ▸ General ▸ Test Runner`.
2. The **EditMode** tab. Every test in this project is an edit-mode test; the PlayMode tab is empty
   and is meant to be.
3. `Run All`, or select `ExpandNullforge.Editor.Tests` and `Run Selected`.

Expect roughly 1,260 tests across 114 files. The tileset ones read and recompose whole sprite
sheets, so a first run takes noticeably longer than the rest put together.

**From a terminal**, with the editor closed (Unity will not open a project twice):

```
Unity.exe -runTests -batchmode -projectPath "E:\ck mods\CoreKeeperModSDK" ^
          -testPlatform EditMode -testResults "E:\ck mods\test-results.xml"
```

Use the same editor version the project is open in. The exit code is 0 only when every test passed;
the NUnit XML at `-testResults` is the record worth keeping, because the console output is
interleaved with the framework's own generate-time warnings.

**`dotnet build` is not a test run.** Building `ExpandNullforge.API`, `ExpandNullforge`,
`ExpandNullforge.Editor` and `ExpandNullforge.Editor.Tests` from `E:\ck mods\CoreKeeperModSDK`
proves every test file compiles and nothing more. No `[Test]` method executes.

---

## What a green bar means

It means: **the generator produced the shape the test expected.**

That is worth having and it is not much. Specifically, green says nothing about:

- **Whether the game accepts that shape.** The framework's own capability registry
  (`API/Scripts/DimensionCapabilityRegistry.cs`) records 2 of 60 capabilities as evidenced in a
  running game. A green suite does not move that number by one.
- **Whether a generated component is ever read.** Wave C found 101 features that generated cleanly
  and were dead because a prefab did not satisfy a vanilla `EntityQuery`. The suite was green
  throughout.
- **Whether a Harmony patch that binds also runs.** Two of those have been found by reading.
- **Whether a system that is created is also scheduled.**
- **Anything about the UI.** Two test files of 114 touch it at all.

The one class of failure the suite genuinely does catch before the game does is the sandbox: a
denied reference in shipped source or in a built assembly fails
`DimensionSandboxGuardTests` / `DimensionSandboxAssemblyGuardTests` here, and in game it would fail
the whole mod with nothing said but "Compilation failed".

---

## What was checked without Unity, and what it showed

Three things can be established offline, and were:

**Everything compiles.** All four projects build with 0 errors. The two warnings-carrying projects
report the same three pre-existing `MSB3277` reference conflicts they reported before this work.

**Nothing is missing from the build.** `ExpandNullforge.Editor.Tests.csproj` has 114
`<Compile Include>` entries and the folder has 114 `.cs` files, and the two sets are equal in both
directions — no test file is excluded from the build, and no include points at a file that is gone.
The same check on `ExpandNullforge.Editor.csproj` gives 158 = 158.

**No test is vacuous by construction.** A sweep over all 1,260 `[Test]` methods
(`Assert.Pass` / `Assert.Ignore` / no assertion at all / every assertion inside a loop) returned:

| Shape | Count | Verdict |
|---|---|---|
| No `Assert.` in the method body | 10 | All ten call a helper in the same file that asserts. Not vacuous. |
| Uses `Assert.Pass` | 1 | `RuntimeSourcesUseNothingTheSandboxDenies`, on the empty-findings path. Its subject is guarded separately by `TheShipSetIsBigEnoughToHaveCheckedAnything`. |
| Uses `Assert.Ignore` | 1 | See the fixture list below. |
| Every assertion inside a loop | 42 | 4 assert a non-empty count first; 33 loop over an enum, a literal array or a counted range and cannot be empty. |
| …of those, over a computed set with no non-empty guard in the method | 5 | Genuinely pass on an empty subject — but in each case a sibling in the same fixture fails loudly on the same emptiness (see below). |

The five: `DimensionArrivalTileTests.NoCellIsSearchedTwice`, `.TheOriginIsNeverACandidate`,
`.CloserTilesAreAlwaysTriedFirst`, `.EveryCandidateIsWithinTheSearchRadius` — all four walk
`DimensionArrivalTile.SearchOffsets`, and `SearchCoversEveryCellInRangeExceptTheOriginItself`
asserts its exact length while `TheFirstCellTriedIsAdjacentToTheDestination` indexes `[0]`. And
`DimensionOreVeinScatterTests.NoTwoVeinCellsShareAPositionAndPaintedOreIsNeverReclaimed`, where
`VeinSizesStayInsideTheAuthoredBounds` in the same fixture asserts `cells.Count > 0`. Left as they
are: the fixture cannot go green on an empty subject, and adding a guard to each would restate what
a sibling already says.

**Some of it was actually executed.** Two pieces of shipped logic have no Unity dependency and were
compiled into a console harness and run against this project:

- `DimensionSandboxGuard` and `DimensionSandboxAssemblyGuard`, over the real tree: deny list 7
  namespaces / 16 types / 2 members; ship set 662 files; **0** findings in framework source, **0**
  in the emitter literals, **0** in the consumer mods, **0** in all four built assemblies
  (`ExpandNullforge.dll`, `ExpandNullforge.API.dll`, `MPTest.dll`, `Nullforge.dll`). The reader
  still reads: pointed at `ExpandNullforge.Editor.dll`, which uses `System.IO` freely and never
  ships, it returned 22 findings.
- `DimensionCuratedPaths.ParentsOf`, over the real catalog: 22 dotted field paths, 5 blocks
  (`cooking`, `fishing`, `player`, `talents`, `upgrading`), 0 dotted paths whose block goes
  unclaimed.

---

## Tests that need a fixture, and what happens without it

Each of these is red rather than skipped when its fixture is absent, which is deliberate — a skip
reads as green and these are the project's strongest claims.

| Test | Needs | Without it |
|---|---|---|
| `DimensionSandboxAssemblyGuardTests` (all three) | `Library/ScriptAssemblies/*.dll`, which only Unity writes | Red: "Not every shipped assembly was found". A fresh clone has an empty `Library`; open the project in Unity once. |
| `DimensionSandboxGuardTests.TheGeneratedConsumerModsUseNothingTheSandboxDenies` and the consumer half of the assembly guard | At least one generated consumer mod — a folder with `Scripts/Generated/*RuntimeBootstrap.cs` | Red, with the sentence from `ConsumerSetProblem` naming what to generate. This project has two: `Assets/Nullforge` and `Assets/MPTest`. |
| `DimensionTilesetGenerationTests` (8 tests) | The captured Dirt tables (`Scripts/Tilesets/DimensionTilesetAtlasData.cs`, written by running the mod in game once) **and** Core Keeper's shipped sheets | Red, with instructions. Both are present on this machine: the data file is there, and the sheets come from the `dev.pugstorm.corekeeper.assets` package in `Library/PackageCache`. |
| `DimensionQueryCompanionTests.EveryNameSaidToHaveAVerdictHasOneInTheCensus` | `E:\ck mods\ck-research\query-match-census.md` — an absolute path outside the Unity project | **Ignored**, not red. This is the one skip in the suite. It is present on this machine; on any other it goes yellow and the "verdict is recorded" list is backed by nobody. |

Three tests added in Phase 0 have never been run at all and are named here so a first failure is not
mistaken for a regression: `TheGeneratedConsumerModsUseNothingTheSandboxDenies`,
`AConsumerModIsFoundByItsGeneratedBootstrapAndScanned`, `NoConsumerModFoundIsAProblemAndNotAPass`
(all in `DimensionSandboxGuardTests`), and the four in `DimensionCuratedFieldTests`. The parts of
them that could be executed offline were, and are listed above; the parts that need
`Application.dataPath`, `SerializedObject` or Unity's assembly set were not.

---

## What this file is not

It is not a CI configuration, and none is proposed. Nobody asked for one, and a scheduled green bar
on a suite that cannot see the failures this project actually has would be worse than no bar: it
would make the number in the capability registry easier to ignore.
