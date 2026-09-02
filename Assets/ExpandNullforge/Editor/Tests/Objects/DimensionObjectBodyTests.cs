using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using ExpandNullforge.Objects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the five uses that could not be dressed until now — a crafting bench, an animal, a
    /// character, a sign and a shop — and the wiring that makes any of the six do anything at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FAILURE THESE PIN IS INVISIBLE OFFLINE, which is why it survived a whole wave. Core
    /// Keeper pools graphical objects by component TYPE
    /// (<c>MemoryManager.CreateModdedPrefabPool</c> ends in
    /// <c>_poolFromComponentType.TryAdd(type, prefab)</c>) and <c>CreateGraphicalObjectSystem</c>
    /// asks the pool for a body rather than instantiating the prefab it was given. A generated
    /// object carrying a vanilla behaviour therefore bakes perfectly and is then drawn as somebody
    /// else's object in the running game. Every check below is a check that the type on the prefab
    /// is one only this framework can have claimed.
    /// </para>
    /// <para>
    /// Measured, not assumed: across the ripped corpus exactly one asset carries <c>Chest</c>, one
    /// carries <c>SignText</c> and one carries <c>VendingMachine</c> — all three listed in
    /// <c>Resources/PooledGraphicalObjectBank.asset</c>, which the game pools before any mod — while
    /// <c>CraftingBuilding</c>, <c>Cattle</c> and <c>NPC</c> appear on no asset at all, because the
    /// game only ships subclasses of those three. The first case loses to the game; the second loses
    /// to whichever generated object loaded first. The fix is the same type-of-our-own either way.
    /// </para>
    /// </remarks>
    public sealed class DimensionObjectBodyTests
    {
        private const string TestRoot = "Assets/NullforgeBodyTests";

        private readonly List<Object> temporaries = new List<Object>();

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeBodyTests");
            }
        }

        [TearDown]
        public void Cleanup()
        {
            for (int i = 0; i < temporaries.Count; i++)
            {
                if (temporaries[i] != null)
                {
                    Object.DestroyImmediate(temporaries[i]);
                }
            }

            temporaries.Clear();

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        // ---- every use now has a body of the framework's own ----

        [Test]
        public void EveryUseIsDrawnWithAFrameworkTypeAndNotTheGames()
        {
            AssertDressed(DimensionUseBehaviour.OpensLikeAChest,
                typeof(ExpandNullforge.Containers.DimensionContainerView));
            AssertDressed(DimensionUseBehaviour.OpensACraftingBench,
                typeof(DimensionCraftingBenchView));
            AssertDressed(DimensionUseBehaviour.TendedLikeAnAnimal, typeof(DimensionCattleView));
            AssertDressed(DimensionUseBehaviour.TalkedToLikeAnNpc, typeof(DimensionNpcView));
            AssertDressed(DimensionUseBehaviour.ReadLikeASign, typeof(DimensionSignView));
            AssertDressed(DimensionUseBehaviour.SellsLikeAShop, typeof(DimensionShopView));
        }

        /// <summary>
        /// Generates one world object with the given use and a picture, and checks that the picture
        /// reaches a renderer the framework's own view holds.
        /// </summary>
        private void AssertDressed(DimensionUseBehaviour use, System.Type expected)
        {
            string id = "body" + (int)use;
            Sprite art = MakeSprite("art" + (int)use);

            DimensionWorldObjectGenerationReport report;
            GameObject prefab = Build(id, art, use, out report);

            GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
            Assert.IsNotNull(visual, id + " has nothing for a player to walk up to");

            Component view = visual.GetComponent(expected);
            Assert.IsNotNull(
                view,
                id + " must carry " + expected.Name + ", or the game hands it somebody else's body");

            IDimensionAuthoredBody dressable = view as IDimensionAuthoredBody;
            Assert.IsNotNull(dressable, expected.Name + " cannot be dressed by the generator");
            Assert.IsNotNull(dressable.Body, expected.Name + " has no renderer to draw the picture");
            Assert.AreSame(
                art,
                dressable.Body.sprite,
                "the authored picture never reached the renderer");
        }

        [Test]
        public void TheWorldPictureTravelsOnTheObjectsOwnRecordForEveryUse()
        {
            // The renderer's baked sprite is only what the prefab looks like in the inspector: the
            // pooled instance is shared, so the view re-reads additionalSprites[0] per entity. If
            // that list were empty the object would draw as nothing however good the prefab looked.
            Sprite art = MakeSprite("record");

            DimensionWorldObjectGenerationReport report;
            GameObject prefab = Build(
                "recordsign", art, DimensionUseBehaviour.ReadLikeASign, out report);

            List<Sprite> extra = prefab.GetComponent<ObjectAuthoring>().additionalSprites;
            Assert.IsNotNull(extra);
            Assert.AreEqual(1, extra.Count);
            Assert.AreSame(art, extra[0]);
        }

        // ---- the two fields the game dereferences without a null check ----

        [Test]
        public void TheBodyHangsUnderTheScalerTheGameWritesTo()
        {
            // EntityMonoBehaviour.SetOrientation ends in XScaler.localScale with no guard, and
            // UpdateGraphicalObjectSystem reaches it every frame. currentFacingVector is a field on
            // the SHARED instance, so even a use that never turns inherits a facing from whatever
            // borrowed the view last. Every view gets the child, and the art hangs under it so the
            // flip actually flips the art.
            Sprite art = MakeSprite("scaled");

            DimensionWorldObjectGenerationReport report;
            GameObject prefab = Build(
                "scaledshop", art, DimensionUseBehaviour.SellsLikeAShop, out report);

            GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
            EntityMonoBehaviour view = visual.GetComponent<EntityMonoBehaviour>();

            Assert.IsNotNull(view.XScaler, "without this the first turn throws every frame");
            SpriteRenderer renderer = visual.GetComponentInChildren<SpriteRenderer>(true);
            Assert.AreSame(
                view.XScaler,
                renderer.transform.parent,
                "art outside the scaler never turns with the object");
        }

        [Test]
        public void TheViewPointsAtItsOwnInteractable()
        {
            // CreateGraphicalObjectSystem fills InteractableObjectReferenceCD from this field alone,
            // and LocalInteractionSystem gives up when that reference is null. Without the line this
            // pins, every generated object baked its use wiring correctly and then did nothing
            // whatsoever when a player pressed use on it.
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = Build(
                "wiredchest",
                MakeSprite("wired"),
                DimensionUseBehaviour.OpensLikeAChest,
                out report);

            GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
            EntityMonoBehaviour view = visual.GetComponent<EntityMonoBehaviour>();

            Assert.IsNotNull(view.interactable, "no use and no outline without this");
            Assert.AreSame(
                visual.GetComponentInChildren<InteractableObject>(true),
                view.interactable,
                "the view must point at its own interactable, not at nothing");
        }

        // ---- a sign needs somewhere to keep its words ----

        [Test]
        public void ASignIsGivenSomewhereToKeepTheWordsWrittenOnIt()
        {
            // The text is a DescriptionBuffer on the entity, and DescriptionConverter only makes one
            // when it sees DescriptionAuthoring. Without it WorldLabel.GetName returns null and the
            // writing window is a box a player types into that saves nothing.
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = Build(
                "wordsign", MakeSprite("words"), DimensionUseBehaviour.ReadLikeASign, out report);

            Assert.IsNotNull(
                prefab.GetComponent<DescriptionAuthoring>(),
                "a sign with nowhere to keep its words cannot be written on");
        }

        [Test]
        public void AnObjectThatStopsShowingWordsLosesTheEmptyWordStore()
        {
            // Regenerated as a SHOP, not as a chest. A chest is a WorldLabel too and keeps the
            // store, because Chest.Use refuses to make a chest nameable without it — that was the
            // correction the floating-text work made, and pinning the old expectation here would
            // put the dead name box back in every generated container's window.
            Sprite art = MakeSprite("exsign");

            DimensionWorldObjectGenerationReport first;
            Build("exsign", art, DimensionUseBehaviour.ReadLikeASign, out first);

            DimensionWorldObjectGenerationReport second;
            GameObject prefab = Build("exsign", art, DimensionUseBehaviour.SellsLikeAShop, out second);

            Assert.IsNull(
                prefab.GetComponent<DescriptionAuthoring>(),
                "generation is authoritative: a shop must not stay nameable because it was once a " +
                "sign");
        }

        [Test]
        public void AnObjectThatBecomesAChestStaysNameable()
        {
            Sprite art = MakeSprite("tochest");

            DimensionWorldObjectGenerationReport first;
            Build("tochest", art, DimensionUseBehaviour.SellsLikeAShop, out first);

            DimensionWorldObjectGenerationReport second;
            GameObject prefab = Build("tochest", art, DimensionUseBehaviour.OpensLikeAChest, out second);

            Assert.IsNotNull(
                prefab.GetComponent<DescriptionAuthoring>(),
                "Chest : WorldLabel, and Chest.Use checks for the buffer before letting a player " +
                "name it, so a chest without one has a name box that saves nothing");
        }

        // ---- the one thing that still cannot be made real ----

        [Test]
        public void ABenchWithNoRecipesSaysItWouldNeverAppear()
        {
            // CraftingHandler's constructor reads CraftingCD with EntityUtility.GetComponentData,
            // which throws when the component is absent, and CraftingBuilding.OnOccupied builds that
            // handler as the object is drawn. So this is not an empty window — it is no object.
            DimensionWorldObjectGenerationReport report;
            Build(
                "benchless",
                MakeSprite("benchless"),
                DimensionUseBehaviour.OpensACraftingBench,
                out report);

            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("no recipes of its own")),
                "an object that cannot appear at all must say so before the build");
        }

        [Test]
        public void ANormalUseWarnsAboutNothing()
        {
            // The old warning fired for five of the six uses. If it had simply been sharpened rather
            // than removed where it no longer applies, this would catch it.
            DimensionWorldObjectGenerationReport report;
            Build("quietshop", MakeSprite("quiet"), DimensionUseBehaviour.SellsLikeAShop, out report);

            Assert.IsFalse(
                report.Warnings.Exists(w => w.Contains("shared body")),
                "the undressable warning must be gone for a use that now works: " +
                string.Join("; ", report.Warnings.ToArray()));
        }

        // ---- a station is something a player looks at too ----

        [Test]
        public void AWorkbenchIsDrawnWhereItStandsAndCarriesAnIcon()
        {
            // Before this the workbench generator passed no picture at all, so a station opened its
            // recipe window from an empty square of ground and had no inventory icon either.
            Sprite art = MakeSprite("station");

            DimensionWorkbenchAsset bench = ScriptableObject.CreateInstance<DimensionWorkbenchAsset>();
            temporaries.Add(bench);

            SerializedObject serialized = new SerializedObject(bench);
            serialized.FindProperty("workbenchId").stringValue = "artbench";
            serialized.FindProperty("displayName").stringValue = "Art Bench";
            serialized.FindProperty("sprite").objectReferenceValue = art;
            serialized.FindProperty("interaction").FindPropertyRelative("whatUsingItDoes").intValue =
                (int)DimensionUseBehaviour.OpensACraftingBench;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionWorkbenchGenerator.Generate(
                new List<DimensionWorkbenchAsset> { bench },
                TestRoot,
                default(DimensionNamingContext));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/artbench.prefab");
            Assert.IsNotNull(prefab, "no station prefab was generated");

            Assert.AreSame(
                art,
                prefab.GetComponent<InventoryItemAuthoring>().icon,
                "a station with no icon draws as an empty square in every slot");

            GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
            DimensionCraftingBenchView view = visual.GetComponent<DimensionCraftingBenchView>();
            Assert.IsNotNull(view, "a station must carry the framework's own bench view");
            Assert.IsNotNull(view.body, "a station with no renderer is invisible where it stands");
            Assert.AreSame(art, view.body.sprite);
        }

        // ---- harness ----

        private GameObject Build(
            string id,
            Sprite art,
            DimensionUseBehaviour use,
            out DimensionWorldObjectGenerationReport report)
        {
            DimensionWorldObjectAsset asset =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                serialized.FindProperty("objectIdentifier").stringValue = id;
                serialized.FindProperty("displayName").stringValue = id;
                serialized.FindProperty("sprite").objectReferenceValue = art;
                // intValue rather than enumValueIndex: the enum has a gap-free range here, but the
                // container tests already found that enumValueIndex writes the ORDINAL POSITION and
                // not the value, which quietly picks the wrong use.
                serialized
                    .FindProperty("interaction")
                    .FindPropertyRelative("whatUsingItDoes")
                    .intValue = (int)use;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                report = DimensionWorldObjectGenerator.Generate(
                    new List<DimensionWorldObjectAsset> { asset },
                    TestRoot,
                    default(DimensionNamingContext));

                return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/" + id + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        /// <summary>
        /// A real sprite asset on disk, because a prefab can only serialize a reference to one.
        /// </summary>
        private static Sprite MakeSprite(string name)
        {
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            texture.name = name + "Texture";
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, TestRoot + "/" + name + "Texture.asset");

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f),
                16f,
                1u,
                SpriteMeshType.FullRect);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, TestRoot + "/" + name + ".asset");
            AssetDatabase.ImportAsset(
                TestRoot + "/" + name + ".asset",
                ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Sprite>(TestRoot + "/" + name + ".asset");
        }
    }
}
