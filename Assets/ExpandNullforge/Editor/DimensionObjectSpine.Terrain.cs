using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What an object does to the ground under it, and what the ground does back.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// Things that go off, take their ground with them, stand in for ground, or bounce.
        /// </summary>
        public static void ApplyTerrainEffects(
            GameObject root,
            DimensionTerrainEffectTemplate terrain,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || terrain == null)
            {
                return;
            }

            // WITHOUT THE FIGHTING TAGS NOTHING LOOKS AT IT. The two systems that set an object off
            // on contact both name the tags that say what it counts as and what it fights — the
            // same block a creature gets. Every one of the game's four objects that explode on
            // contact is a creature or a minion. Supplied here rather than said about, because the
            // tags are a component and this object can carry one.
            if (terrain.ExplodesOnContact)
            {
                bool alreadyKnewItsEnemies = HasNamed(root, "BehaviourTagsAuthoring");
                DimensionQueryCompanions.DecidesWhoIsAnEnemy(root);
                SayWhenTicked(
                    !alreadyKnewItsEnemies,
                    report,
                    "explodes on contact, and nothing was set about what it counts as an enemy — " +
                    "which is the first thing the game checks before it lets anything go off on " +
                    "contact. An empty answer was filled in, which reads as 'nothing here is my " +
                    "enemy'. Set what it attacks if you want it to go off on something.");
            }

            if (terrain.ExplodesOnContact)
            {
                ExplodeOnImpactAuthoring blast = EnsureComponent<ExplodeOnImpactAuthoring>(root);
                blast.distanceToExplode = terrain.ExplodesWithin;
                blast.explodeRadius = terrain.BlastRadius;
                blast.explodeDamage = terrain.BlastDamage;
                blast.explodeDamageMultiplier = terrain.BlastHitsThisHardForItsTier;
                blast.spawnTilesOnExplode = terrain.BlastLaysGround && !terrain.BlastGroundIsMissing;
                if (blast.spawnTilesOnExplode)
                {
                    int laid = ResolveTilesetName(terrain.BlastGroundTilesetId, resolveTileset);
                    if (laid < 0)
                    {
                        blast.spawnTilesOnExplode = false;
                        if (report != null)
                        {
                            report(
                                "lays '" + terrain.BlastGroundTilesetId + "' where it explodes, " +
                                "which is not a tileset, so it leaves the ground as it was.");
                        }
                    }
                    else
                    {
                        blast.tilesetToSpawn = (PugTilemap.Tileset)laid;
                        blast.tileTypeToSpawn = terrain.BlastGroundKind;
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<ExplodeOnImpactAuthoring>(root);
            }

            if (terrain.TakesItsTileWhenItDies)
            {
                RemoveTileOnDeathAuthoring takes =
                    EnsureComponent<RemoveTileOnDeathAuthoring>(root);
                takes.removeChance = terrain.RemovalChance;
                takes.tileType = terrain.RemovedTileKind;

                int removed = ResolveTilesetName(terrain.RemovedTilesetId, resolveTileset);
                if (removed >= 0)
                {
                    takes.tileset = (PugTilemap.Tileset)removed;
                }
                else if (!string.IsNullOrEmpty(terrain.RemovedTilesetId) && report != null)
                {
                    report(
                        "removes '" + terrain.RemovedTilesetId + "' beneath it when it dies, which " +
                        "is not a tileset, so it removes whatever is there instead.");
                }
            }
            else
            {
                RemoveComponentIfPresent<RemoveTileOnDeathAuthoring>(root);
            }

            if (terrain.CountsAsATile && !terrain.StandsInForNothing)
            {
                int stands = ResolveTilesetName(terrain.CountsAsTilesetId, resolveTileset);
                if (stands < 0)
                {
                    RemoveComponentIfPresent<PseudoTileAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "counts as '" + terrain.CountsAsTilesetId + "' underfoot, which is not " +
                            "a tileset, so nothing treats it as ground.");
                    }
                }
                else
                {
                    PseudoTileAuthoring pseudo = EnsureComponent<PseudoTileAuthoring>(root);
                    pseudo.tileset = (PugTilemap.Tileset)stands;
                    pseudo.tileType = terrain.CountsAsTileKind;
                }
            }
            else
            {
                RemoveComponentIfPresent<PseudoTileAuthoring>(root);
                if (terrain.StandsInForNothing && report != null)
                {
                    report(
                        "counts as ground without saying which ground, so nothing treats it as any.");
                }
            }

            if (terrain.BouncesAlongTheGround)
            {
                EnsureComponent<GroundBouncableProjectileAuthoring>(root).verticalCurve =
                    terrain.Arc;

                SayWhenTicked(
                    true,
                    report,
                    "is set to bounce along the ground. Core Keeper only bounces a thrown thing — " +
                    "the seven grenades are all of it — and the bounce needs the effect stream a " +
                    "thrown thing carries, which a placed object does not have. Build it as a " +
                    "projectile and the arc will work.");
            }
            else
            {
                RemoveComponentIfPresent<GroundBouncableProjectileAuthoring>(root);
            }

            if (terrain.BlastGroundIsMissing && report != null)
            {
                report("is told to lay ground where it explodes without naming any.");
            }
        }

        public static void ApplyLooksAtItsSurroundings(
            GameObject root,
            DimensionLooksAtItsSurroundingsTemplate adapts,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || adapts == null ||
                !adapts.ChangesWithItsSurroundings || adapts.HasNoRules)
            {
                RemoveComponentIfPresent<AdaptiveEntityBufferAuthoring>(root);
                if (adapts != null && adapts.HasNoRules && report != null)
                {
                    report(
                        "changes its look with its surroundings but has no rules saying how, so it " +
                        "always wears its first look.");
                }

                return;
            }

            AdaptiveEntityBufferAuthoring buffer =
                EnsureComponent<AdaptiveEntityBufferAuthoring>(root);
            buffer.adaptiveCondition = new System.Collections.Generic.List<AdaptiveCondition>();

            DimensionSurroundingRule[] rules = adapts.Rules;
            for (int i = 0; i < rules.Length; i++)
            {
                buffer.adaptiveCondition.Add(new AdaptiveCondition
                {
                    variation = rules[i].WearsLook,
                    matchesNeeded = rules[i].MatchesNeeded,
                    allowAnyTilesetToMatch = rules[i].AnyGroundCounts,
                    leftTile = BuildTileCondition(rules[i].ToItsLeft, rules[i].TileKind, resolveTileset, report),
                    rightTile = BuildTileCondition(rules[i].ToItsRight, rules[i].TileKind, resolveTileset, report),
                    forwardTile = BuildTileCondition(rules[i].InFront, rules[i].TileKind, resolveTileset, report),
                    backTile = BuildTileCondition(rules[i].Behind, rules[i].TileKind, resolveTileset, report)
                });
            }

            if (adapts.AFussierRuleIsHiddenByALooserOne && report != null)
            {
                report(
                    "lists a rule needing more matching neighbours BELOW one needing fewer. Rules " +
                    "are checked in order and the first match wins, so the fussier look can never " +
                    "appear. Put the fussiest rules first.");
            }
        }

        /// <summary>One neighbour expectation, resolved from a tileset name.</summary>
        /// <remarks>
        /// A blank name is a real answer — "no expectation on this side" — so it resolves to the
        /// game's own zero rather than being reported as a mistake.
        /// </remarks>
        private static TileCondition BuildTileCondition(
            string tilesetId,
            PugTilemap.TileType tileKind,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (string.IsNullOrEmpty(tilesetId))
            {
                return default(TileCondition);
            }

            int resolved = resolveTileset == null ? -1 : resolveTileset(tilesetId);
            if (resolved < 0)
            {
                PugTilemap.Tileset named;
                if (System.Enum.TryParse(tilesetId, false, out named))
                {
                    resolved = (int)named;
                }
            }

            if (resolved < 0)
            {
                if (report != null)
                {
                    report(
                        "expects '" + tilesetId + "' beside it, which is neither one of this mod's " +
                        "tilesets nor one of the game's, so that side is treated as no expectation.");
                }

                return default(TileCondition);
            }

            return new TileCondition
            {
                tileset = (PugTilemap.Tileset)resolved,
                tileType = tileKind
            };
        }

        /// <summary>
        /// Makes an object keep ground it can sit on underneath itself.
        /// </summary>
        public static void ApplyKeepsItsFloor(
            GameObject root,
            DimensionKeepsItsFloorTemplate floor,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || floor == null || !floor.KeepsItsOwnFloor)
            {
                RemoveComponentIfPresent<EnsureSameGroundTileBeneathEntityAuthoring>(root);
                return;
            }

            if (floor.HasNoFallback)
            {
                RemoveComponentIfPresent<EnsureSameGroundTileBeneathEntityAuthoring>(root);
                if (report != null)
                {
                    report(
                        "keeps its own floor without saying what to lay down, so it would insist on " +
                        "ground it supports with nothing to put there. Give it a fallback tileset.");
                }

                return;
            }

            int fallback = resolveTileset == null ? -1 : resolveTileset(floor.FallbackTilesetId);
            if (fallback < 0)
            {
                RemoveComponentIfPresent<EnsureSameGroundTileBeneathEntityAuthoring>(root);
                if (report != null)
                {
                    report(
                        "lays down '" + floor.FallbackTilesetId + "' beneath itself, which is " +
                        "neither one of this mod's tilesets nor one of the game's, so it keeps no " +
                        "floor at all.");
                }

                return;
            }

            EnsureSameGroundTileBeneathEntityAuthoring keeps =
                EnsureComponent<EnsureSameGroundTileBeneathEntityAuthoring>(root);
            keeps.tileType = floor.TileKindBeneath;
            keeps.fallbackTileset = (PugTilemap.Tileset)fallback;
            keeps.continouslyCheck = floor.KeepsChecking;

            keeps.onlySupportsTilesets = new System.Collections.Generic.List<PugTilemap.Tileset>();
            string[] happy = floor.HappyOnTilesets;
            for (int i = 0; i < happy.Length; i++)
            {
                int supported = resolveTileset == null ? -1 : resolveTileset(happy[i]);
                if (supported >= 0)
                {
                    keeps.onlySupportsTilesets.Add((PugTilemap.Tileset)supported);
                }
                else if (report != null)
                {
                    report(
                        "is happy sitting on '" + happy[i] + "', which is not a tileset, so it will " +
                        "replace that ground instead of leaving it alone.");
                }
            }

            StateID ignoreIn;
            keeps.ignoreCheckingWhileInState =
                !string.IsNullOrEmpty(floor.StopsCheckingInState) &&
                System.Enum.TryParse(floor.StopsCheckingInState, false, out ignoreIn);
            if (keeps.ignoreCheckingWhileInState)
            {
                System.Enum.TryParse(floor.StopsCheckingInState, false, out ignoreIn);
                keeps.stateToIgnore = ignoreIn;
            }
            else if (!string.IsNullOrEmpty(floor.StopsCheckingInState) && report != null)
            {
                report(
                    "stops keeping its floor in state '" + floor.StopsCheckingInState + "', which " +
                    "the game does not have, so it always keeps it.");
            }
        }

        /// <summary>
        /// What tile an object leaves behind when it is destroyed, and what it cracks into first.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two small components that belong together because they are the same idea at two moments:
        /// <c>CrackableTileAuthoring</c> is the tile it turns into when damaged but not destroyed
        /// (146 and 93 vanilla prefabs respectively), <c>SpawnTileOnDeathAuthoring</c> is what is
        /// left where it stood.
        /// </para>
        /// <para>
        /// Both name a tileset by our own id, not by the vanilla enum, because a custom block is
        /// exactly the thing an author is most likely to want left behind — that is the point of
        /// having a tileset system at all. Resolution goes through the same registry the block
        /// generator uses, so a name that resolves to nothing is reported rather than silently
        /// leaving dirt.
        /// </para>
        /// </remarks>
        public static void ApplyTileOutcomes(
            GameObject root,
            DimensionTileOutcomeTemplate outcome,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (outcome == null || !outcome.LeavesATileBehind)
            {
                RemoveComponentIfPresent<SpawnTileOnDeathAuthoring>(root);
            }
            else
            {
                int tileset = ResolveTilesetOrReport(outcome.LeavesTilesetId, resolveTileset, report);
                if (tileset < 0)
                {
                    RemoveComponentIfPresent<SpawnTileOnDeathAuthoring>(root);
                }
                else
                {
                    SpawnTileOnDeathAuthoring spawn =
                        EnsureComponent<SpawnTileOnDeathAuthoring>(root);
                    spawn.tileType = outcome.LeavesTileType;
                    spawn.tileset = (PugTilemap.Tileset)tileset;
                    spawn.spawnChance = outcome.LeavesChance;
                    spawn.clearOtherTiles = outcome.ClearsWhatWasThere;
                }
            }

            if (outcome == null || !outcome.CracksFirst)
            {
                RemoveComponentIfPresent<CrackableTileAuthoring>(root);
                return;
            }

            int crackTileset = ResolveTilesetOrReport(outcome.CracksIntoTilesetId, resolveTileset, report);
            if (crackTileset < 0)
            {
                RemoveComponentIfPresent<CrackableTileAuthoring>(root);
                return;
            }

            // ONLY ON SOMETHING THAT IS A TILE. Core Keeper's step for cracking looks for the tile
            // answer and the cracking answer on the same object and does nothing at all when
            // either is missing — all ninety-three of the game's cracking objects are tiles. A
            // world object is not a tile, so the cracking answer is written and then skipped in
            // silence. Blocks made in the Tileset studio get this properly; a placed object cannot.
            if (!HasNamed(root, "TileAuthoring"))
            {
                RemoveComponentIfPresent<CrackableTileAuthoring>(root);
                SayWhenTicked(
                    true,
                    report,
                    "is set to crack before it breaks, and that only works on ground and walls. " +
                    "Core Keeper looks for cracking on something it already knows is a tile, and " +
                    "a placed object is not one, so the crack stage was left off. Build it as a " +
                    "block in the Tileset studio if you want it to crack.");
                return;
            }

            CrackableTileAuthoring crack = EnsureComponent<CrackableTileAuthoring>(root);
            crack.crackTileType = outcome.CracksIntoTileType;
            crack.crackTileset = (PugTilemap.Tileset)crackTileset;
        }
    }
}
