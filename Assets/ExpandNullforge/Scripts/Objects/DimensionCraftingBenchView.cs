using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// The body of a generated crafting station: the picture a player walks up to, and the recipe
    /// window that opens when they use it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A TYPE OF OUR OWN RATHER THAN THE GAME'S <c>CraftingBuilding</c>. Core Keeper pools
    /// graphical objects by component TYPE and the mapping is first come, first served:
    /// <c>MemoryManager.CreateModdedPrefabPool</c> ends in
    /// <c>_poolFromComponentType.TryAdd(type, prefab)</c>, and <c>CreateGraphicalObjectSystem</c>
    /// then asks for a free component of the prefab's type rather than instantiating the prefab. Put
    /// the bare <c>CraftingBuilding</c> on two generated stations and the second one is drawn with
    /// the FIRST one's body for the rest of the session, because that first prefab won the type. A
    /// type of our own still shares one pool between every generated station, which is why the
    /// picture is re-read per entity below rather than baked.
    /// </para>
    /// <para>
    /// UNLIKE A CHEST, NO VANILLA PREFAB CLAIMS THIS TYPE. Reading every prefab in the ripped
    /// corpus, exactly one asset carries <c>Chest</c>, one carries <c>SignText</c> and one carries
    /// <c>VendingMachine</c> — all three in <c>Resources/PooledGraphicalObjectBank.asset</c>, built
    /// before any mod — while <c>CraftingBuilding</c>, <c>Cattle</c> and <c>NPC</c> appear on none:
    /// the game only ever ships SUBCLASSES of those three. So a station was not wearing a vanilla
    /// bench's art; it was wearing the first generated bench's art. Same fix either way.
    /// </para>
    /// <para>
    /// THREE FIELDS THE GAME DEREFERENCES WITHOUT A NULL CHECK ARE FILLED HERE.
    /// <c>TryGetBuildingSpecificSettings</c> and <c>GetCraftingCategoryWindowInfo</c> both foreach
    /// straight over their lists, and <c>CraftingUIBase</c> reads
    /// <c>GetCraftingUISettings().craftingUIBackgroundVariation</c> with no guard, so an unfilled
    /// station threw the moment its window opened. They are refilled on every occupy rather than
    /// baked once, for the pooling reason above: <c>base.OnOccupied</c> REPLACES
    /// <c>craftingCategoryWindowInfos</c> for a multi-window station, and the next station to borrow
    /// this instance would inherit those windows.
    /// </para>
    /// <para>
    /// THE WINDOW'S LOOK IS LOOKED UP PER ENTITY for the same pooling reason. It used to be
    /// hard-coded to <c>Wood</c>; it now comes from <see cref="DimensionCraftingBenchLookRegistry"/>
    /// keyed on the object being drawn, so two stations in one mod can wear two different windows.
    /// An unregistered station still gets Wood, which is the value the game itself falls back to.
    /// </para>
    /// </remarks>
    public class DimensionCraftingBenchView : CraftingBuilding, IDimensionAuthoredBody
    {
        /// <summary>
        /// The renderer that draws the station. Wired at generation; the sprite it shows is replaced
        /// on every occupy, because this instance is shared with every other generated station.
        /// </summary>
        [Tooltip("The renderer that draws this station. Wired at generation.")]
        public SpriteRenderer body;

        /// <inheritdoc/>
        public SpriteRenderer Body
        {
            get { return body; }
            set { body = value; }
        }

        /// <summary>
        /// Points the body at whatever the entity being drawn is and gives the crafting UI something
        /// to read, before the base class runs.
        /// </summary>
        /// <remarks>
        /// Ahead of <c>base.OnOccupied()</c> deliberately. The base builds the
        /// <c>CraftingHandler</c> and, for a station that includes other stations' recipes, fills
        /// <see cref="CraftingBuilding.craftingCategoryWindowInfos"/> itself — so the empty list has
        /// to be in place first, or the base's work would be thrown away.
        /// </remarks>
        public override void OnOccupied()
        {
            DimensionAuthoredBody.Point(body, objectData.objectID, objectData.variation);

            buildingSpecificUISettings = new List<CraftingUISettingsOverride>();
            craftingCategoryWindowInfos = new List<CraftingCategoryWindowInfos>();
            defaultUISettings = new CraftingUISettings(
                (UIManager.CraftingUIThemeType)
                    DimensionCraftingBenchLookRegistry.For(objectData.objectID),
                DimensionAuthoredBody.NameTerm(objectData.objectID));

            base.OnOccupied();
        }
    }
}
