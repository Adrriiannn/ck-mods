using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The sweep a generator runs over a finished object, and the three shorthands for it.
    /// </summary>
    internal static partial class DimensionQueryCompanions
    {
        /// <summary>
        /// Adds every component the game's own systems need beside what this generate already wrote,
        /// and says plainly where a gap can only be closed by the author.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call this LAST, after every pass has written what it owns. The rows only ever add, so
        /// they cannot take back an answer an earlier pass made; and they only act on objects that
        /// already carry the feature, so an object without the feature is untouched.
        /// </para>
        /// <para>
        /// IT DOES NOT SAY THE OBJECT'S LOOK IS DYNAMIC. That is
        /// <see cref="ItsLookIsNotPartOfItsIdentity"/>, and it belongs to the three Finish helpers
        /// rather than here, because the two generators that reach this method without going
        /// through one of them are exactly the two it would be wrong for: the item generator, where
        /// the author answers the question themselves, and the plant generator, which registers
        /// several looks of one crop and needs the game to tell them apart.
        /// </para>
        /// </remarks>
        public static void CloseTheGaps(GameObject root, string displayName, Action<string> say)
        {
            if (root == null)
            {
                return;
            }

            Companion[] all = Rows;
            bool[] alreadySaid = new bool[all.Length];

            // RUN UNTIL NOTHING NEW APPEARS, not once through the rows. A component this sweep
            // supplies can itself have a row — "the thing we added needs another thing" is the same
            // bug class one level down — and a single pass in array order would only ever reach
            // that when the second row happened to sit later in the array. The stop condition is
            // the object itself: when a pass adds no component, there is nothing left to cascade
            // from. The bound is there so a pair of rows that each ask for the other cannot spin.
            for (int pass = 0; pass <= all.Length; pass++)
            {
                int before = root.GetComponents<Component>().Length;

                for (int i = 0; i < all.Length; i++)
                {
                    Companion row = all[i];
                    if (root.GetComponent(row.Present) == null)
                    {
                        continue;
                    }

                    if (HasNamed(root, row.AlsoNeeds))
                    {
                        continue;
                    }

                    // A ROW WITH NOTHING BUT WORDS STILL HAS TO LOOK BEFORE IT SPEAKS. Handing a
                    // pure read in as a row's "fill" — is this blast big enough, does
                    // this creature belong to somebody — makes the row claim to supply something
                    // it never supplies. The read belongs here, where it is for the rows
                    // that do fill, so a blast that really does reach and a minion that really does
                    // have an owner stay silent.
                    if (row.FillIn == null && TheGapIsReallyClosed(root, row))
                    {
                        continue;
                    }

                    if (row.FillIn != null && row.FillIn(root) && TheGapIsReallyClosed(root, row))
                    {
                        continue;
                    }

                    // Once per row, however many passes it takes. Saying the same sentence four
                    // times over would read as four separate problems.
                    if (!alreadySaid[i] && say != null && !string.IsNullOrEmpty(row.SayInstead))
                    {
                        alreadySaid[i] = true;
                        say("'" + displayName + "' " + row.SayInstead);
                    }
                }

                if (root.GetComponents<Component>().Length == before)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// The last thing done to a generated creature, critter, boss or animal.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Three things every one of Core Keeper's own creature prefabs carries and none of ours
        /// did: it is sent to players, it has a body, and it can turn. Without the first the client
        /// never even builds it and the chase skips it; without the second nothing can hit it, it
        /// cannot be noticed by anything else's overlap, and every movement state in the game skips
        /// it; without the third the chase, the attacks and the wander all skip it.
        /// </para>
        /// <para>
        /// It runs LAST because the facing question can only be answered once every other pass has
        /// finished writing what the creature does.
        /// </para>
        /// </remarks>
        public static void FinishACreature(GameObject root, string displayName, Action<string> say)
        {
            FinishACreature(root, displayName, 1f, 1f, say);
        }

        /// <summary>
        /// The same, sized from the creature rather than from a constant.
        /// </summary>
        /// <remarks>
        /// The generator wrote the body earlier in its run, before the chase and the wander read
        /// it, and this restates it at the end the way every other value here is restated. Both
        /// calls have to agree on the size or the second would undo the first, which is why the
        /// numbers come in here as well.
        /// </remarks>
        public static void FinishACreature(
            GameObject root,
            string displayName,
            float bodyWidthInTiles,
            float widthOthersNoticeItAt,
            Action<string> say)
        {
            IsSentToPlayers(root);
            ItsLookIsNotPartOfItsIdentity(root);
            HasABodyThingsCanTouch(
                root,
                bodyWidthInTiles,
                1f,
                widthOthersNoticeItAt,
                Prefixed(displayName, say));
            TurnsIfSomethingOnItNeedsTo(root, displayName, say);
            CloseTheGaps(root, displayName, say);
        }

        /// <summary>
        /// The last thing done to a generated critter.
        /// </summary>
        /// <remarks>
        /// The same three things a creature gets, with the critter's own body: Core Keeper's
        /// critters are one small sphere on the critter layer rather than a creature-sized sphere
        /// on the enemy layer, and the layer is what keeps a moth from being treated as a monster
        /// by everything that watches for one.
        /// </remarks>
        /// <summary>
        /// Carries what the object IS onto the running object, not only into the object table.
        /// </summary>
        /// <remarks>
        /// The one system this matters most for is the one that applies burning ground, acid,
        /// mould, oil and slippery slime, and it does not read a wrong default — it names the
        /// type component in its query and never sees an object without one. Every object Core
        /// Keeper builds has it; nothing this framework built outside the creature and item paths
        /// did, so a mod chest, plant, workbench, vehicle or critter could stand in lava and be
        /// touched by none of it. The component reads the type off the object's own identity, so
        /// there is nothing to set.
        /// </remarks>
        public static void CarriesWhatItIsOntoTheRunningObject(GameObject root)
        {
            if (root == null || root.GetComponent<ObjectAuthoring>() == null)
            {
                return;
            }

            Ensure<ExpandNullforge.Authoring.DimensionObjectTypeAuthoring>(root);
        }

        public static void FinishACritter(GameObject root, string displayName, Action<string> say)
        {
            CarriesWhatItIsOntoTheRunningObject(root);
            IsSentToPlayers(root);
            ItsLookIsNotPartOfItsIdentity(root);
            HasTheBodyACritterHas(root, Prefixed(displayName, say));
            TurnsIfSomethingOnItNeedsTo(root, displayName, say);
            CloseTheGaps(root, displayName, say);
        }

        /// <summary>
        /// The last thing done to a generated object that is placed in the world.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It gives the object a body, and that is new. Nothing the world-object, container,
        /// workbench or vehicle generators built had one, which meant nothing in the game could
        /// hit it, mine it, dig it, walk into it or find it with any of the casts every attack and
        /// every explosion runs — while the answers that describe being hit were all written and
        /// all silent. Core Keeper's own objects carry one on essentially everything that is
        /// placed. See <see cref="HasTheBodyAPlacedThingHas"/> for the shape and the layers.
        /// </para>
        /// <para>
        /// Everything it needs is already answered elsewhere on the object rather than asked
        /// again: the footprint and its corner offset are the ones the author gave for placement,
        /// and what SORT of thing it is — see <see cref="WhatSortOfPlacedThingItIs"/> — is read off
        /// the ground-cover tick, the world object's kind, the riding answer, the crafting answer
        /// and the automation answers. So there is no new control, and none of those controls can
        /// be contradicted by this pass.
        /// </para>
        /// </remarks>
        public static void FinishAWorldObject(GameObject root, string displayName, Action<string> say)
        {
            FinishAWorldObject(
                root,
                displayName,
                default(WhatTheAuthorSaidAboutItsBody),
                say);
        }

        /// <summary>
        /// The same, for a generator whose author answered which way the doorway runs or whether a
        /// player walks through it.
        /// </summary>
        public static void FinishAWorldObject(
            GameObject root,
            string displayName,
            WhatTheAuthorSaidAboutItsBody said,
            Action<string> say)
        {
            CarriesWhatItIsOntoTheRunningObject(root);

            // The same thing a creature and a critter already got here, and the largest remaining
            // difference between a generated placed object and Core Keeper's own: 2,074 of the
            // game's 4,180 entity prefabs carry it, ChestEntity, CopperWorkBenchEntity,
            // WoodDoorEntity, TorchEntity, EerieRugEntity and MinecartEntity among them, and
            // GhostPostConverter returns immediately without it so the entity never gets
            // GhostInstance at all.
            IsSentToPlayers(root);
            ItsLookIsNotPartOfItsIdentity(root);

            GiveItTheBodyItsFootprintAsksFor(
                root,
                displayName,
                PlacedThingKind.Decoration,
                said,
                say);
            TurnsIfSomethingOnItNeedsTo(root, displayName, say);
            CloseTheGaps(root, displayName, say);
        }

        /// <summary>
        /// Sizes a placed object's body from the footprint and the sort of thing it already is.
        /// </summary>
        public static void GiveItTheBodyItsFootprintAsksFor(
            GameObject root,
            string displayName,
            Action<string> say)
        {
            GiveItTheBodyItsFootprintAsksFor(root, displayName, PlacedThingKind.Decoration, say);
        }

        /// <summary>
        /// The same, for a generator that already knows what it is building.
        /// </summary>
        /// <remarks>
        /// The plant generator builds one prefab for a crop and one for its seed through the same
        /// method, so it hands in which of the two this one is rather than leaving the answer to be
        /// guessed off components a later pass might rename.
        /// </remarks>
        public static void GiveItTheBodyItsFootprintAsksFor(
            GameObject root,
            string displayName,
            PlacedThingKind whenNothingElseSaysSo,
            Action<string> say)
        {
            GiveItTheBodyItsFootprintAsksFor(
                root,
                displayName,
                whenNothingElseSaysSo,
                default(WhatTheAuthorSaidAboutItsBody),
                say);
        }

        /// <summary>
        /// The same, carrying the two answers only the author can give.
        /// </summary>
        public static void GiveItTheBodyItsFootprintAsksFor(
            GameObject root,
            string displayName,
            PlacedThingKind whenNothingElseSaysSo,
            WhatTheAuthorSaidAboutItsBody said,
            Action<string> say)
        {
            if (root == null)
            {
                return;
            }

            int wide = 1;
            int tall = 1;
            Vector2 cornerOffset = Vector2.zero;

            PlaceableObjectAuthoring placeable = root.GetComponent<PlaceableObjectAuthoring>();
            if (placeable != null)
            {
                if (placeable.prefabTileSize.x > 0)
                {
                    wide = placeable.prefabTileSize.x;
                }

                if (placeable.prefabTileSize.y > 0)
                {
                    tall = placeable.prefabTileSize.y;
                }

                cornerOffset = new Vector2(
                    placeable.prefabCornerOffset.x,
                    placeable.prefabCornerOffset.y);
            }

            HasTheBodyAPlacedThingHas(
                root,
                wide,
                tall,
                WhatSortOfPlacedThingItIs(root, whenNothingElseSaysSo, said),
                cornerOffset,
                Prefixed(displayName, say));
        }

        /// <summary>
        /// Reads what sort of placed thing this is off what the author already answered.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY BRANCH IS AN EXISTING CONTROL. Ground cover is the "it is ground cover" tick; a
        /// door, a bed and a trophy are the world object's own kind; a vehicle is the riding
        /// answer; a workbench is "it crafts"; a chest is "it holds items"; a machine is one of the
        /// automation answers; a crop and a seed are which of the two the plant generator is
        /// building. Nothing here asks the author anything new, and nothing here can overrule
        /// something they set.
        /// </para>
        /// <para>
        /// The order matters where an object is two things at once. A boss statue holds an item and
        /// is not a chest; a workbench that holds items is a workbench. Ground cover wins outright,
        /// because "a player walks over it" is the strongest thing anybody said about it.
        /// </para>
        /// </remarks>
        public static PlacedThingKind WhatSortOfPlacedThingItIs(
            GameObject root,
            PlacedThingKind whenNothingElseSaysSo)
        {
            return WhatSortOfPlacedThingItIs(
                root,
                whenNothingElseSaysSo,
                default(WhatTheAuthorSaidAboutItsBody));
        }

        /// <summary>
        /// The two answers about a body that nothing already on the object can supply.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERYTHING ELSE IN <see cref="WhatSortOfPlacedThingItIs"/> IS READ OFF THE OBJECT, and
        /// these two cannot be. Every vanilla door prefab is one tile by one tile whichever way it
        /// faces, so the footprint says nothing about which way the doorway runs; and a torch and a
        /// statue carry exactly the same components, so nothing on the object says whether a player
        /// walks through it. Both are questions only the author can answer, both were being
        /// answered wrongly by a guess, and both now come from a control with those words on it.
        /// </para>
        /// <para>
        /// A generator that has neither question — a container, a crafting station, a vehicle, a
        /// crop — hands in nothing and gets what it got before.
        /// </para>
        /// </remarks>
        public struct WhatTheAuthorSaidAboutItsBody
        {
            /// <summary>
            /// The doorway runs along the wall — north to south — rather than across it.
            /// </summary>
            public bool ADoorwayThatRunsAlongTheWall;

            /// <summary>
            /// A player and a creature walk straight through it, the way they walk through a torch
            /// or a candle, while it still blocks building on its tile and still takes a hit.
            /// </summary>
            public bool PlayersWalkThroughIt;
        }

        /// <summary>
        /// The same, with the two answers only the author can give.
        /// </summary>
        public static PlacedThingKind WhatSortOfPlacedThingItIs(
            GameObject root,
            PlacedThingKind whenNothingElseSaysSo,
            WhatTheAuthorSaidAboutItsBody said)
        {
            if (root == null)
            {
                return whenNothingElseSaysSo;
            }

            if (HasNamed(root, "GroundDecorationAuthoring"))
            {
                return ItIsWired(root)
                    ? PlacedThingKind.PoweredGroundCover
                    : PlacedThingKind.GroundCover;
            }

            if (HasNamed(root, "SeedAuthoring") ||
                HasNamed(root, "DimensionSeedAuthoring") ||
                HasNamed(root, "AutomatedPlantableSeedAuthoring"))
            {
                return PlacedThingKind.Seed;
            }

            if (HasNamed(root, "PlantAuthoring") ||
                HasNamed(root, "RootPlantAuthoring") ||
                HasNamed(root, "GrowingPlantAuthoring") ||
                HasNamed(root, "AutomatedHarvestablePlantAuthoring") ||
                HasNamed(root, "DimensionCropTierPlantAuthoring"))
            {
                return PlacedThingKind.Plant;
            }

            // THE THREE VEHICLES ARE THREE SHAPES, and the framework already writes exactly one of
            // these three components per vehicle from the kind the author picked, so the answer is
            // read off that rather than asked again. BoatAuthoring is on both vanilla boats,
            // MinecartAuthoring on both minecarts and VehicleAuthoring on all 14 karts.
            if (HasNamed(root, "BoatAuthoring"))
            {
                return PlacedThingKind.Boat;
            }

            if (HasNamed(root, "MinecartAuthoring"))
            {
                return PlacedThingKind.Minecart;
            }

            if (HasNamed(root, "VehicleAuthoring"))
            {
                return PlacedThingKind.Vehicle;
            }

            if (HasNamed(root, "DoorAuthoring") || HasNamed(root, "FenceGateAuthoring"))
            {
                return said.ADoorwayThatRunsAlongTheWall
                    ? PlacedThingKind.DoorAlongTheWall
                    : PlacedThingKind.Door;
            }

            if (HasNamed(root, "BedAuthoring"))
            {
                return PlacedThingKind.Bed;
            }

            if (HasNamed(root, "TrophyAuthoring"))
            {
                return PlacedThingKind.Trophy;
            }

            if (ItIsWired(root))
            {
                // The two machine shapes are read off which automation answer is already on the
                // object: an arm, a collector or a circuit watches 20 and a drill, a sprinkler or
                // a lamp also watches the critter layer. No new question.
                return ItMovesItemsAround(root)
                    ? PlacedThingKind.MachineThatMovesItems
                    : PlacedThingKind.Machine;
            }

            if (HasNamed(root, "CraftingAuthoring"))
            {
                return PlacedThingKind.Workbench;
            }

            if (HasNamed(root, "InventoryAuthoring"))
            {
                return PlacedThingKind.Chest;
            }

            // LAST, so that nothing that is a door, a chest, a crafting station or a machine can
            // be turned into a prop by ticking it. It only ever decides between the two answers an
            // ordinary prop can have.
            if (said.PlayersWalkThroughIt &&
                (whenNothingElseSaysSo == PlacedThingKind.Decoration ||
                 whenNothingElseSaysSo == PlacedThingKind.WalkThroughProp))
            {
                return PlacedThingKind.WalkThroughProp;
            }

            return whenNothingElseSaysSo;
        }
    }
}
