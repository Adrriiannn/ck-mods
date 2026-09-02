using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The child objects an interactable is made of: words, scaler, use behaviour.
    /// </summary>
    internal static partial class DimensionInteractionVisualUtility
    {
        /// <summary>
        /// Gives anything with words floating over it somewhere to keep them, and takes an empty
        /// store away again from anything that no longer shows any.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE WORDS ARE NOT A FIELD ON THE OBJECT. They are a <c>DescriptionBuffer</c> on the
        /// entity, put there by <c>DescriptionConverter</c> the moment it sees
        /// <c>DescriptionAuthoring</c> — with or without any starting text. Without the buffer
        /// <c>WorldLabel.GetName</c> returns null, the floating text never renders, and the writing
        /// window is a box a player types into that saves nothing.
        /// </para>
        /// <para>
        /// A CHEST NEEDS THIS AS MUCH AS A SIGN DOES, which is the correction here.
        /// <c>Chest : WorldLabel</c>, and <c>Chest.Use</c> refuses to make the chest the active
        /// world label unless the entity has the buffer, so a generated container without one has a
        /// dead name field in its window. Stripping the component off everything that is not a sign
        /// takes it back off every container the spine has just given one to.
        /// </para>
        /// <para>
        /// NOTHING IS REMOVED HERE AT ALL, and a narrowed removal is no better than a broad one.
        /// The narrow rule — take the component off whenever words do not float above
        /// the object and <c>initialText</c> is empty — rests on
        /// <c>DimensionObjectSpine.ApplyFacingAndText</c> having already written the author's own
        /// text, and the container and workbench generators never call <c>ApplyFacingAndText</c> at
        /// all. Their store comes from <c>DimensionObjectSpine.ApplyPlacedObject</c>, which gives
        /// one to every placed object because that is what vanilla chests carry, and which runs
        /// before this. So a container whose interaction is left at the default would have the store
        /// stripped one pass after it was given, and — per this file's own reading of
        /// <c>Chest.Use</c> — its rename box would save nothing.
        /// </para>
        /// <para>
        /// Every generator that reaches this method puts its object through
        /// <c>ApplyPlacedObject</c> first (containers, workbenches, world objects), so a removal
        /// here could only ever undo that pass. An empty store costs a player nothing: it is an
        /// empty <c>DescriptionBuffer</c>, which is exactly what a vanilla placed object has.
        /// </para>
        /// </remarks>
        private static void ApplyTheStoreForFloatingWords(GameObject root)
        {
            if (root.GetComponent<DescriptionAuthoring>() == null)
            {
                root.AddComponent<DescriptionAuthoring>();
            }
        }

        /// <summary>
        /// Lets an animal be given a name, because the window that tends it always offers to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NOT COSMETIC, AND NOT OPTIONAL. <c>CattleUI.SetName</c> sends the name straight to the
        /// server with no check that the animal can hold one, and the server answers with
        /// <c>EntityUtility.GetComponentData&lt;NameCD&gt;</c>
        /// (<c>PlayerCommand/ServerSystem.cs:482</c>), which THROWS when the component is absent. So
        /// a generated animal without <c>NameAuthoring</c> is not an animal with a greyed-out name
        /// box; it is an animal that throws on the server the first time somebody names it.
        /// </para>
        /// <para>
        /// <c>NameAuthoring</c> is an empty marker class whose converter does nothing but
        /// <c>EnsureHasComponent&lt;NameCD&gt;</c>, so adding it costs one component and no
        /// decisions. The name tag above the animal reads the same <c>NameCD</c>, which is why this
        /// belongs beside the tag rather than in the object-roles panel: the roles panel's
        /// "can be named" tick is a choice, and this is a requirement of the use.
        /// </para>
        /// </remarks>
        private static void ApplyBeingNameable(
            GameObject root,
            DimensionInteractionTemplate interaction)
        {
            if (!interaction.ItCarriesANameTag)
            {
                return;
            }

            if (root.GetComponent<NameAuthoring>() == null)
            {
                root.AddComponent<NameAuthoring>();
            }
        }

        /// <summary>
        /// Says so when an object is set to open a crafting window it has no recipes for.
        /// </summary>
        /// <remarks>
        /// This is the one warning in this file that survives the framework views, and it is not
        /// about art. <c>CraftingHandler</c>'s constructor reads <c>CraftingCD</c> off the entity
        /// with <c>EntityUtility.GetComponentData</c>, which throws when the component is absent, and
        /// <c>CraftingBuilding.OnOccupied</c> builds that handler the moment the object is drawn. So
        /// an object that answers "opens a crafting bench" without carrying <c>CraftingAuthoring</c>
        /// does not open an empty window — it never appears at all. Only the Workbench asset writes
        /// that component, which is why the fix names it.
        /// </remarks>
        private static void WarnAboutABenchWithNothingToCraft(
            GameObject root,
            DimensionInteractionTemplate interaction,
            System.Action<string> report)
        {
            if (report == null ||
                interaction.WhatUsingItDoes != DimensionUseBehaviour.OpensACraftingBench ||
                root.GetComponent<CraftingAuthoring>() != null)
            {
                return;
            }

            report(
                "opens a crafting bench but has no recipes of its own, and the game reads a " +
                "recipe list before it draws the object, so it would never appear where it was " +
                "placed. Make it a Workbench instead, which carries the recipes, or set what " +
                "using it does to something else.");
        }

        /// <summary>
        /// Says so when an animal's name tag would be drawn inside the animal.
        /// </summary>
        /// <remarks>
        /// The height that floats a chest's label nicely — the default, and the game's own chest
        /// value — is half a tile, and half a tile above an animal's feet is its middle. This is the
        /// one case where the shared default is wrong rather than merely unadventurous, so it is
        /// worth a sentence; anything above half a tile is left alone, because how high a name
        /// belongs over a creature is the author's judgement, not the framework's.
        /// </remarks>
        private static void WarnAboutANameTagInsideTheAnimal(
            DimensionInteractionTemplate interaction,
            System.Action<string> report)
        {
            if (report == null ||
                !interaction.ItCarriesANameTag ||
                interaction.HowHighTheWordsFloat > 0.5f)
            {
                return;
            }

            report(
                "is tended like an animal, and its name would float " +
                interaction.HowHighTheWordsFloat.ToString("0.###") +
                " tiles up, which is inside most animals rather than above them. Raise how high " +
                "the words float — the game's own camel uses 2.");
        }

        /// <summary>
        /// Hangs the words that float above the object, for the uses that have any.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THREE OF THE SEVEN USES SHOW WORDS IN THE AIR, and each wants a different component
        /// pointed at a different field. A chest and a sign are both <c>WorldLabel</c>s and want
        /// <c>worldLabel</c>; an animal is a <c>Cattle</c> and wants <c>nameTag</c>. The other four
        /// get nothing, because the game's own bench, character and vending machine have nothing.
        /// </para>
        /// <para>
        /// THIS IS NOT DECORATION FOR A CHEST. <c>Chest.Use</c> only makes the chest the player's
        /// active world label when <c>worldLabel != null</c> (<c>Chest.cs:34-35</c>), and the naming
        /// box inside the chest window writes to the active world label and nowhere else
        /// (<c>ChestInventoryUI.cs:70-75</c>). Until this line existed, every generated container
        /// opened a window whose name field could be typed into and saved nothing.
        /// </para>
        /// <para>
        /// NOR FOR AN ANIMAL. Without the tag, <c>Cattle.UpdateName</c>, <c>OnShow</c> and
        /// <c>OnHide</c> all dereference null, which is why the framework's animal view used to
        /// override its whole per-frame pass away and lose the leash rope with it.
        /// </para>
        /// </remarks>
        private static void AddFloatingWords(
            GameObject root,
            MonoBehaviour behaviour,
            DimensionInteractionTemplate interaction)
        {
            if (behaviour == null)
            {
                return;
            }

            if (interaction.WordsFloatAboveIt)
            {
                WorldLabel label = behaviour as WorldLabel;
                if (label != null)
                {
                    label.worldLabel = DimensionFloatingTextUtility.AddWordsThatFloatAboveIt(
                        root, interaction.HowHighTheWordsFloat);
                }

                return;
            }

            if (interaction.ItCarriesANameTag)
            {
                Cattle cattle = behaviour as Cattle;
                if (cattle != null)
                {
                    cattle.nameTag = DimensionFloatingTextUtility.AddNameTagAboveIt(
                        root, interaction.HowHighTheWordsFloat);
                }
            }
        }

        /// <summary>
        /// Gives the body the child that flips it left and right, the way every vanilla graphical
        /// prefab has one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NOT DECORATION — IT IS A NULL CHECK THE GAME NEVER DOES.
        /// <c>EntityMonoBehaviour.SetOrientation</c> ends in <c>this.XScaler.localScale = ...</c>
        /// with no guard, and it is reached from <c>UpdateAnimatorSpeedAndOrientation</c>, which
        /// <c>UpdateGraphicalObjectSystem</c> calls for every entity every frame. The animal and the
        /// character both answer <c>true</c> to <c>updateAnimOrientation</c>, so they reach it as
        /// soon as their facing direction is non-zero.
        /// </para>
        /// <para>
        /// AND THE POOL MAKES IT WORSE THAN "ONLY FOR THOSE TWO". <c>currentFacingVector</c> is a
        /// field on the shared instance, so a view that drew a facing object a moment ago carries
        /// that direction into the next object it is handed to, whatever its behaviour. Every view
        /// gets the child.
        /// </para>
        /// </remarks>
        private static Transform AddScaler(GameObject root, MonoBehaviour behaviour)
        {
            EntityMonoBehaviour view = behaviour as EntityMonoBehaviour;
            if (view == null)
            {
                return null;
            }

            GameObject scaler = new GameObject(ScalerChildName);
            scaler.transform.SetParent(root.transform, false);
            view.XScaler = scaler.transform;
            return scaler.transform;
        }

        /// <summary>Puts the component that actually does the thing on the visual prefab.</summary>
        /// <remarks>
        /// EVERY ONE OF THESE IS A FRAMEWORK SUBCLASS, NEVER THE GAME'S OWN COMPONENT, and that is
        /// not a preference. Core Keeper pools graphical objects by component TYPE
        /// (<c>MemoryManager.CreateModdedPrefabPool</c> registers the type with <c>TryAdd</c>) and
        /// <c>CreateGraphicalObjectSystem</c> asks the pool for a body rather than instantiating the
        /// prefab, so the first prefab to claim a type is the one every later object of that type is
        /// drawn as. For <c>Chest</c>, <c>SignText</c> and <c>VendingMachine</c> the winner is one of
        /// the game's own prefabs — the ripped corpus has exactly one asset carrying each, all three
        /// listed in <c>Resources/PooledGraphicalObjectBank.asset</c>. For <c>CraftingBuilding</c>,
        /// <c>Cattle</c> and <c>NPC</c> no vanilla prefab carries the base type at all, so the winner
        /// is whichever generated object loaded first — every later station wearing the first
        /// station's picture. Both failures have the same fix.
        /// </remarks>
        private static MonoBehaviour AddUseBehaviour(
            GameObject root,
            DimensionInteractionTemplate interaction,
            System.Action<string> report)
        {
            switch (interaction.WhatUsingItDoes)
            {
                case DimensionUseBehaviour.OpensLikeAChest:
                {
                    Chest chest =
                        root.AddComponent<ExpandNullforge.Containers.DimensionContainerView>();
                    chest.showSortAndQuickStackButtons = interaction.ShowsSortAndQuickStackButtons;
                    return chest;
                }

                case DimensionUseBehaviour.OpensACraftingBench:
                    return root.AddComponent<ExpandNullforge.Objects.DimensionCraftingBenchView>();

                case DimensionUseBehaviour.TendedLikeAnAnimal:
                    return root.AddComponent<ExpandNullforge.Objects.DimensionCattleView>();

                case DimensionUseBehaviour.TalkedToLikeAnNpc:
                    return root.AddComponent<ExpandNullforge.Objects.DimensionNpcView>();

                case DimensionUseBehaviour.ReadLikeASign:
                    // Deliberately NOT a SignText subclass: that class dereferences two atlas-backed
                    // SpriteObject fields every frame from a private method. See DimensionSignView.
                    return root.AddComponent<ExpandNullforge.Objects.DimensionSignView>();

                case DimensionUseBehaviour.SellsLikeAShop:
                    // The Forlorn Metropolis machines' own behaviour: Interact opens the buy window
                    // over the entity's baked item buffer. Borrowed by subclassing it.
                    return root.AddComponent<ExpandNullforge.Objects.DimensionShopView>();

                default:
                    if (report != null)
                    {
                        report(
                            "is marked as usable in a way the framework does not know how to build, " +
                            "so using it would do nothing.");
                    }

                    return null;
            }
        }

        /// <summary>
        /// Wires the two calls — using it, and walking away from it — into the prefab itself.
        /// </summary>
        /// <remarks>
        /// Both lists are replaced rather than appended to, so regenerating never leaves a listener
        /// from a previous answer sitting behind the new one.
        /// </remarks>
        private static void WireUse(
            InteractableObject interactable,
            MonoBehaviour behaviour,
            DimensionUseBehaviour what)
        {
            UnityEvent onUse = new UnityEvent();
            UnityEvent onLeave = new UnityEvent();

            switch (what)
            {
                case DimensionUseBehaviour.OpensLikeAChest:
                {
                    Chest chest = (Chest)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, chest.Use);
                    UnityEventTools.AddPersistentListener(onLeave, chest.OnPlayerLeftChest);
                    break;
                }

                case DimensionUseBehaviour.OpensACraftingBench:
                {
                    CraftingBuilding bench = (CraftingBuilding)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, bench.Use);
                    UnityEventTools.AddPersistentListener(onLeave, bench.OnPlayerLeftBuilding);
                    break;
                }

                case DimensionUseBehaviour.TendedLikeAnAnimal:
                {
                    Cattle cattle = (Cattle)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, cattle.Interact);
                    UnityEventTools.AddPersistentListener(onLeave, cattle.OnPlayerLeft);
                    break;
                }

                case DimensionUseBehaviour.TalkedToLikeAnNpc:
                {
                    NPC npc = (NPC)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, npc.Interact);
                    UnityEventTools.AddPersistentListener(onLeave, npc.OnPlayerLeft);
                    break;
                }

                case DimensionUseBehaviour.ReadLikeASign:
                {
                    // Typed as the framework view rather than SignText because the sign is the one
                    // use that does NOT derive from the game's component — see DimensionSignView for
                    // the every-frame null dereference that rules it out. The two method names are
                    // the same, so the wiring the converter reads is unchanged.
                    ExpandNullforge.Objects.DimensionSignView sign =
                        (ExpandNullforge.Objects.DimensionSignView)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, sign.Interact);
                    UnityEventTools.AddPersistentListener(onLeave, sign.OnPlayerLeft);
                    break;
                }

                case DimensionUseBehaviour.SellsLikeAShop:
                {
                    VendingMachine shop = (VendingMachine)behaviour;
                    UnityEventTools.AddPersistentListener(onUse, shop.Interact);
                    UnityEventTools.AddPersistentListener(onLeave, shop.OnPlayerLeft);
                    break;
                }
            }

            interactable.onUseActions = new List<UnityEvent> { onUse };
            interactable.onTriggerExitActions = new List<UnityEvent> { onLeave };
        }
    }
}
