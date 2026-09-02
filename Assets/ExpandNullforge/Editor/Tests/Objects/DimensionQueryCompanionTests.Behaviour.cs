#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The prefab tests: beds, boats, karts, and what changing a kind takes off.
    /// </summary>
    internal sealed partial class DimensionQueryCompanionTests
    {
        /// <summary>
        /// The one rule for the six places the game looks an object up by its look.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Six systems call <c>PugDatabase.GetPrimaryPrefabEntity</c> with a look and then set or
        /// remove the object's collider from what came back. The lookup answers nothing for a look
        /// nobody registered, so on a framework object four of them DELETED the collider — a locked
        /// chest became unhittable the moment the key went in — and two indexed a lookup with a null
        /// entity inside a Burst job. Saying the object's look is not part of its identity turns all
        /// six into a set of the collider it already has.
        /// </para>
        /// <para>
        /// AND THE TWO EXCLUSIONS ARE PINNED HERE, because they are the reason this is not written
        /// by the sweep every generator reaches. An item's answer belongs to its author, and a crop
        /// is registered at several looks, so <c>CloseTheGaps</c> on its own must leave the field
        /// alone. If that ever changes, rare crops stop being findable and nothing else says so.
        /// </para>
        /// </remarks>
        [Test]
        public void FinishingAPlacedThingSaysItsLookIsNotItsIdentity()
        {
            GameObject finished = new GameObject("LookProbe");
            GameObject swept = new GameObject("SweptOnlyProbe");
            try
            {
                ObjectAuthoring identity = finished.AddComponent<ObjectAuthoring>();
                Assert.That(
                    identity.variationIsDynamic,
                    Is.False,
                    "The probe was supposed to start with the field unset.");

                DimensionQueryCompanions.FinishAWorldObject(finished, "probe", null);

                Assert.That(
                    identity.variationIsDynamic,
                    Is.True,
                    "Finishing a placed object has to say its look is not part of which object it " +
                    "is, or six of the game's own systems look it up under its new look, find " +
                    "nothing, and four of them take its collider away for good.");

                ObjectAuthoring sweptIdentity = swept.AddComponent<ObjectAuthoring>();
                DimensionQueryCompanions.CloseTheGaps(swept, "probe", null);

                Assert.That(
                    sweptIdentity.variationIsDynamic,
                    Is.False,
                    "The sweep on its own must NOT write that field. The item generator writes it " +
                    "from the author's own answer, and the plant generator registers a crop at " +
                    "several looks — marking the first one dynamic would hand every lookup the " +
                    "plain crop and rare crops would quietly stop existing.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(finished);
                UnityEngine.Object.DestroyImmediate(swept);
            }
        }

        /// <summary>
        /// A bed comes out as the two slabs the game's own bed has, not one box over both tiles.
        /// </summary>
        /// <remarks>
        /// <c>BedEntity</c> — the only bed in Core Keeper — carries box (1, 1, 0.2) at
        /// (0, 0.5, -0.3) and box (1, 1, 0.7) at (0, 0.5, 1.15) over a footprint of 1 by 2. The
        /// gap between them is why a player can stand in the middle of a bed. The census row that
        /// said "2 of 2 agree" had compared the layers and the collision response and nothing else,
        /// and the code it was justifying wrote one solid box across the whole footprint.
        /// </remarks>
        [Test]
        public void ABedIsTwoSlabsWithAGapBetweenThem()
        {
            GameObject probe = new GameObject("BedProbe");
            try
            {
                probe.AddComponent<ObjectAuthoring>();
                PlaceableObjectAuthoring placeable = probe.AddComponent<PlaceableObjectAuthoring>();
                placeable.prefabTileSize = new Vector2Int(1, 2);
                probe.AddComponent<BedAuthoring>();

                DimensionQueryCompanions.FinishAWorldObject(probe, "probe", null);

                List<Component> shapes = ShapesOn(probe);
                Assert.That(
                    shapes.Count,
                    Is.EqualTo(2),
                    "A bed has a headboard and a footboard. One box over the whole footprint is a " +
                    "two-tile wall where the game's bed is two thin ends.");

                AssertShape(shapes[0], new Vector3(1f, 1f, 0.2f), new Vector3(0f, 0.5f, -0.3f));
                AssertShape(shapes[1], new Vector3(1f, 1f, 0.7f), new Vector3(0f, 0.5f, 1.15f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Turning a bed into an ordinary prop leaves it with one shape, not the bed's two.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The generators regenerate onto the prefab they made last time, and the helper that
        /// writes the shapes only ever adds one when the slot is empty. While every placed sort
        /// wrote exactly one shape that could not go wrong; a bed writes two, so a bed turned into
        /// anything else kept its footboard — a solid box a tile away from the object, blocking a
        /// tile, drawn by nothing, with no control that clears it.
        /// </para>
        /// <para>
        /// The second generate here is the same object with the bed answer taken off, which is
        /// exactly what a modder does when they change their mind about what they are building.
        /// </para>
        /// </remarks>
        [Test]
        public void ChangingWhatSomethingIsTakesItsOldShapeOff()
        {
            GameObject probe = new GameObject("WasABedProbe");
            try
            {
                probe.AddComponent<ObjectAuthoring>();
                PlaceableObjectAuthoring placeable = probe.AddComponent<PlaceableObjectAuthoring>();
                placeable.prefabTileSize = new Vector2Int(1, 2);
                BedAuthoring bed = probe.AddComponent<BedAuthoring>();

                DimensionQueryCompanions.FinishAWorldObject(probe, "probe", null);
                Assert.That(ShapesOn(probe).Count, Is.EqualTo(2), "a bed is two slabs");

                UnityEngine.Object.DestroyImmediate(bed);
                DimensionQueryCompanions.FinishAWorldObject(probe, "probe", null);

                List<Component> shapes = ShapesOn(probe);
                Assert.That(
                    shapes.Count,
                    Is.EqualTo(1),
                    "An ordinary prop is one box over its tiles. The second shape is the bed's " +
                    "footboard, left over from when this object was a bed: it blocks a tile a " +
                    "tile away from the object and nothing on the object draws anything there.");

                AssertShape(shapes[0], new Vector3(1f, 1f, 2f), new Vector3(0f, 0.5f, 0.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// A boat gets the boat's shape, not the minecart's.
        /// </summary>
        /// <remarks>
        /// The 18 vanilla vehicles are three shapes: 14 karts at (1, 1, 0.75) centred
        /// (0, 0.5, 0.125), two boats at (0.01, 1, 0.01) at the same centre, and two minecarts at
        /// the footprint box. The row that said "18 of 18 agree" had compared the layers and the
        /// response — those really are 18 of 18 — and the shape it wrote was the minecart's, which
        /// is 2 of 18. A boat given the whole tile refuses every placement on that tile and
        /// swallows melee swings across it, because Category06 is inside the refusal mask.
        /// </remarks>
        [Test]
        public void ABoatGetsTheBoatsShapeAndAKartGetsTheKarts()
        {
            GameObject boat = new GameObject("BoatProbe");
            GameObject kart = new GameObject("KartProbe");
            try
            {
                boat.AddComponent<ObjectAuthoring>();
                boat.AddComponent<PlaceableObjectAuthoring>().prefabTileSize = Vector2Int.one;
                boat.AddComponent<BoatAuthoring>();
                DimensionQueryCompanions.FinishAWorldObject(boat, "probe", null);
                AssertShape(
                    ShapesOn(boat)[0],
                    new Vector3(0.01f, 1f, 0.01f),
                    new Vector3(0f, 0.5f, 0.125f));

                kart.AddComponent<ObjectAuthoring>();
                kart.AddComponent<PlaceableObjectAuthoring>().prefabTileSize = Vector2Int.one;
                kart.AddComponent<VehicleAuthoring>();
                DimensionQueryCompanions.FinishAWorldObject(kart, "probe", null);
                AssertShape(
                    ShapesOn(kart)[0],
                    new Vector3(1f, 1f, 0.75f),
                    new Vector3(0f, 0.5f, 0.125f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boat);
                UnityEngine.Object.DestroyImmediate(kart);
            }
        }

        /// <summary>
        /// The shapes on an object, in the order they were added.
        /// </summary>
        /// <remarks>
        /// By name, because this assembly does not reference the physics package and one geometry
        /// type is not worth widening the assembly graph for — the same reason the generator
        /// writes these fields through <c>SerializedObject</c>.
        /// </remarks>
        private static List<Component> ShapesOn(GameObject root)
        {
            List<Component> shapes = new List<Component>();
            foreach (Component component in root.GetComponents<Component>())
            {
                if (component != null &&
                    component.GetType().Name == "PhysicsShapeAuthoring")
                {
                    shapes.Add(component);
                }
            }

            return shapes;
        }

        private static void AssertShape(Component shape, Vector3 size, Vector3 centre)
        {
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(shape);
            AssertVector(serialized, "m_PrimitiveSize", size);
            AssertVector(serialized, "m_PrimitiveCenter", centre);
        }

        private static void AssertVector(
            UnityEditor.SerializedObject serialized,
            string path,
            Vector3 expected)
        {
            UnityEditor.SerializedProperty property = serialized.FindProperty(path);
            Assert.That(
                property,
                Is.Not.Null,
                "The physics package no longer serializes " + path + " under that name, so the " +
                "generator has been writing into nothing.");

            Assert.That(
                property.vector3Value.x, Is.EqualTo(expected.x).Within(0.0005f), path + ".x");
            Assert.That(
                property.vector3Value.y, Is.EqualTo(expected.y).Within(0.0005f), path + ".y");
            Assert.That(
                property.vector3Value.z, Is.EqualTo(expected.z).Within(0.0005f), path + ".z");
        }

        [Test]
        public void SeeingNearbyThingsKeepsWhatAnEarlierPassAsked()
        {
            GameObject probe = new GameObject("TrackerProbe");
            try
            {
                // A shove authored at six tiles, watching two layers.
                DimensionQueryCompanions.SeesNearbyThings(probe, 6f, 4u, true);

                // Then the summoning circle's pass, which merges rather than writing its own
                // numbers outright.
                DimensionQueryCompanions.SeesNearbyThings(
                    probe,
                    DimensionQueryCompanions.VanillaNoticeRadius,
                    DimensionQueryCompanions.VanillaNoticeLayers,
                    false);

                Assert.That(
                    DimensionQueryCompanions.HowFarItSees(probe),
                    Is.EqualTo(6f),
                    "The later pass narrowed how far the object can see. Everything on the object " +
                    "that watches its surroundings shares one radius, so the widest ask has to win " +
                    "or the other feature silently stops reaching.");

                Assert.That(
                    DimensionQueryCompanions.WhatItWatches(probe),
                    Is.EqualTo(5u),
                    "The later pass dropped a layer the earlier one asked for. The masks have to " +
                    "be added together, because each feature filters the list again by its own rule.");

                Assert.That(
                    DimensionQueryCompanions.ItLooksEveryFrame(probe),
                    Is.True,
                    "The later pass turned off checking every frame after an earlier one asked for it.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void SomethingThatMovesIsAllowedToTurnAndSomethingStillIsNot()
        {
            GameObject mover = new GameObject("MoverProbe");
            GameObject still = new GameObject("StillProbe");
            try
            {
                mover.AddComponent<AnimationAuthoring>();
                mover.AddComponent<ChaseStateAuthoring>();
                List<string> saidAboutTheMover = new List<string>();
                DimensionQueryCompanions.TurnsIfSomethingOnItNeedsTo(
                    mover, "mover", saidAboutTheMover.Add);

                Assert.That(
                    mover.GetComponent<AnimationAuthoring>().orientationSupport,
                    Is.Not.EqualTo(AnimationAuthoring.OrientationSupport.None),
                    "A creature that chases was left unable to turn. Every answer in " +
                    "DimensionQueryCompanions.AnswersThatOnlyWorkOnSomethingThatCanTurn is read " +
                    "by a system that names the facing component, the chase included, so it " +
                    "would chase nothing.");

                Assert.That(
                    saidAboutTheMover,
                    Is.Not.Empty,
                    "The facing answer was overruled without telling anybody.");

                still.AddComponent<AnimationAuthoring>();
                DimensionQueryCompanions.TurnsIfSomethingOnItNeedsTo(still, "still", null);

                Assert.That(
                    still.GetComponent<AnimationAuthoring>().orientationSupport,
                    Is.EqualTo(AnimationAuthoring.OrientationSupport.None),
                    "Something that does nothing but stand there was made to turn. 'Does not turn' " +
                    "is the right answer for scenery and a turret, and it is the author's to make.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mover);
                UnityEngine.Object.DestroyImmediate(still);
            }
        }

        /// <summary>
        /// The bare name a type argument ends in, with every legal decoration stripped.
        /// </summary>
        /// <remarks>
        /// <c>global::</c> and the verbatim <c>@</c> are both legal C# and both defeated the
        /// naive character class a scan is written with, so <c>Ensure&lt;global::Pug.X&gt;</c>
        /// and <c>Ensure&lt;@X&gt;</c> name nothing any list has to answer for. The tree already
        /// aliases a type in eight files to dodge a name clash, so a qualified name showing up is
        /// a matter of the next clash, not of anyone being clever.
        /// </remarks>
        private static string LastPartOf(string typeName)
        {
            string name = typeName.Trim();

            int qualifier = name.LastIndexOf("::", StringComparison.Ordinal);
            if (qualifier >= 0)
            {
                name = name.Substring(qualifier + 2);
            }

            int dot = name.LastIndexOf('.');
            if (dot >= 0)
            {
                name = name.Substring(dot + 1);
            }

            return name.StartsWith("@", StringComparison.Ordinal) ? name.Substring(1) : name;
        }

        private static Type FindAuthoringType(string name)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException loaded)
                {
                    types = loaded.Types;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    if (types[i] != null &&
                        types[i].Name == name &&
                        typeof(Component).IsAssignableFrom(types[i]))
                    {
                        return types[i];
                    }
                }
            }

            return null;
        }
    }
}
#endif
