using Pug.UnityExtensions;
using PugTilemap;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// The seed sitting in the soil, before there is a plant to look at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A SEED IS ON SCREEN FOR THE FIRST TWO MINUTES OF EVERY CROP, so it is not a detail: a crop
    /// with no seed picture is a player planting something and seeing bare dirt until it sprouts.
    /// The game's own seeds are drawn by their own small renderer, which does one thing beyond
    /// showing a picture — it swaps to a damp-looking one whenever the tile underneath is watered.
    /// That is the whole of <c>Pug.Objects/Seed.cs</c>, and this is the same thing.
    /// </para>
    /// <para>
    /// A SEPARATE TYPE FROM <see cref="DimensionPlantView"/> ON PURPOSE. Core Keeper pools these by
    /// component type, so being a different class is what keeps a seed instance and a plant
    /// instance out of each other's pool — and they want different things per frame, a wetness
    /// check against a growth stage. Sharing a pool would work only because everything is
    /// re-derived anyway, and would mean every seed paying for a stage lookup it has no stages for.
    /// </para>
    /// </remarks>
    public sealed class DimensionSeedView : DimensionPlantView
    {
        /// <summary>
        /// The name of the damp version of a seed's picture.
        /// </summary>
        /// <remarks>
        /// Spelled the way the game spells it — a variant is named after its texture with the
        /// animation's own name taken off the front, so vanilla's <c>seedHeartBerry_watered</c>
        /// inside <c>seedHeartBerry</c> is called exactly this. A different spelling here would
        /// hash to something the asset has no variant for, and the seed would simply never look
        /// wet.
        /// </remarks>
        public const string WateredVariantName = "watered";

        private static readonly int WateredVariant =
            Pug.Sprite.SpriteAsset.StringToHash(WateredVariantName);

        private int shownVariant = -1;

        /// <summary>
        /// Shows the damp picture over watered soil and the dry one otherwise.
        /// </summary>
        /// <remarks>
        /// The last answer is remembered so the variant is only ever set when it changes: this runs
        /// every frame for every seed on screen, and setting a variant rebuilds the sprite's
        /// current frame data whether or not it is the one already showing.
        /// </remarks>
        protected override void UpdateLook(bool playChangeEffects)
        {
            // Nothing to swap on a sprite with no asset, and the game's own variant code reads that
            // reference without checking it.
            if (plantSprite == null || plantSprite.asset == null)
            {
                return;
            }

            int wanted = Manager.multiMap.GetTileLayerLookup()
                .HasTile(WorldPosition.RoundToInt2(), TileType.wateredGround)
                ? WateredVariant
                : 0;
            if (wanted == shownVariant)
            {
                return;
            }

            shownVariant = wanted;
            plantSprite.SetVariant(wanted);
            if (shadowSprite != null && shadowSprite.asset != null)
            {
                shadowSprite.SetVariant(wanted);
            }
        }

        public override void OnOccupied()
        {
            // Cleared before the base runs, so the pooled instance's last answer cannot make this
            // seed skip the one variant assignment it needs.
            shownVariant = -1;
            base.OnOccupied();
        }
    }
}
