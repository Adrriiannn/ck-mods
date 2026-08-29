using System;
using System.Globalization;
using System.Text;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits the half of a crop's look that only exists once the game is running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THE GENERATOR CANNOT JUST BAKE THIS INTO THE PREFAB. Core Keeper hands a pooled plant
    /// body from one crop to the next, so the body has to re-learn which pictures to draw, which
    /// colour to wash them and whether to glow every time it is given an entity. It looks all of
    /// that up by object id and variation — and object ids do not exist until the mod is loaded.
    /// The bootstrap is the only place the two can be introduced.
    /// </para>
    /// <para>
    /// The pictures are registered as an ADDRESS, derived from the crop's object name by the same
    /// arithmetic the generator used when it wrote the sprite asset. Neither step has to find the
    /// other's output; they agree because they compute the same number from the same name.
    /// </para>
    /// <para>
    /// ONE ROW COVERS EVERY VARIATION NOBODY CLAIMED. A crop's ordinary plant, its already-ripe
    /// copy and the seed an ordinary planting lands on all look the same, so they share a row; only
    /// a better version that actually draws something of its own costs an entry of its own.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>The puff a crop throws up when it ripens if its author named none.</summary>
        /// <remarks>
        /// Vanilla's default for every crop that is not coral or gleam root — read off
        /// <c>Plant.PlayRipeEffects</c>.
        /// </remarks>
        private const PuffID DefaultRipePuff = PuffID.LeafDebris;

        /// <summary>Writes one registration per crop look that has pictures behind it.</summary>
        internal static void AppendPlantPresentationRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            DimensionPlantAsset[] plants = template.GlobalPlants;
            for (int i = 0; plants != null && i < plants.Length; i++)
            {
                DimensionPlantAsset plant = plants[i];
                if (plant == null || !plant.Enabled || string.IsNullOrEmpty(plant.PlantId))
                {
                    continue;
                }

                AppendOnePlant(builder, plant, modName);
            }
        }

        private static void AppendOnePlant(
            StringBuilder builder,
            DimensionPlantAsset plant,
            string modName)
        {
            DimensionPlantArtTemplate art = plant.Art;

            // A plant that spreads on its own is never planted, so nothing ever rolls for it and it
            // has no better versions to draw. Kept in step with the generator's own rule, which is
            // what makes the variation numbers below match the prefabs on disk.
            DimensionCropVersionTemplate[] versions = plant.IsPlanted
                ? plant.EnabledVersions
                : new DimensionCropVersionTemplate[0];
            string[] keys = DimensionPlantGenerator.BuildVersionKeys(plant, versions, null);

            string plantObjectName = DimensionObjectNamespace.Qualify(
                modName, plant.PlantId + DimensionPlantGenerator.PlantSuffix);
            string seedObjectName = DimensionObjectNamespace.Qualify(
                modName, plant.PlantId + DimensionPlantGenerator.SeedSuffix);

            int[] stages = StageAnimationsFor(plant.PicturesNeeded);
            int shine = art.ShinePicture != null ? SpriteAsset.StringToHash(
                DimensionPlantSpriteAssetUtility.ShineAnimationName) : 0;
            int ripeSound = SoundNumber(art.RipeSoundId);
            int ripePuff = RipePuffNumber(art.RipePuffId);

            if (art.HasPlantPictures)
            {
                AppendPlantRow(
                    builder,
                    plantObjectName,
                    -1,
                    DimensionPlantGenerator.PlantArtSeed(plantObjectName, null),
                    stages,
                    shine,
                    art,
                    Color.white,
                    ripeSound,
                    ripePuff);
            }

            if (art.HasSeedPicture && plant.IsPlanted)
            {
                AppendSeedRow(
                    builder,
                    seedObjectName,
                    -1,
                    DimensionPlantGenerator.SeedArtSeed(seedObjectName, null),
                    art,
                    Color.white);
            }

            for (int i = 0; i < versions.Length; i++)
            {
                DimensionCropVersionLookTemplate look = versions[i].Look;
                if (!look.LooksDifferent)
                {
                    continue;
                }

                if (art.HasPlantPictures || look.HasPlantPictures)
                {
                    // A version that draws nothing of its own still gets a row when it washes the
                    // ordinary pictures a colour — which is the whole point of the wash.
                    string seed = look.HasPlantPictures
                        ? DimensionPlantGenerator.PlantArtSeed(plantObjectName, keys[i])
                        : DimensionPlantGenerator.PlantArtSeed(plantObjectName, null);
                    int versionShine =
                        look.ShinePicture != null || art.ShinePicture != null ? shine : 0;
                    AppendPlantRow(
                        builder,
                        plantObjectName,
                        DimensionPlantGenerator.PlantVariationFor(i),
                        seed,
                        stages,
                        versionShine,
                        art,
                        look.ColourWash,
                        ripeSound,
                        ripePuff);
                }

                if (!plant.IsPlanted || (!art.HasSeedPicture && !look.HasSeedPicture))
                {
                    continue;
                }

                string seedSeed = look.HasSeedPicture
                    ? DimensionPlantGenerator.SeedArtSeed(seedObjectName, keys[i])
                    : DimensionPlantGenerator.SeedArtSeed(seedObjectName, null);
                AppendSeedRow(
                    builder,
                    seedObjectName,
                    DimensionPlantGenerator.SeedVariationFor(i),
                    seedSeed,
                    art,
                    look.ColourWash);
            }
        }

        private static void AppendPlantRow(
            StringBuilder builder,
            string objectName,
            int variation,
            string addressSeed,
            int[] stages,
            int shine,
            DimensionPlantArtTemplate art,
            Color wash,
            int ripeSound,
            int ripePuff)
        {
            long low;
            long high;
            DimensionPlantSpriteAssetUtility.AddressFor(addressSeed, out low, out high);

            builder.AppendLine("    DimensionPlantPresentationRegistry.Register(");
            builder.AppendLine("        new DimensionPlantPresentationDefinition(");
            builder.Append("            ").Append(ToCSharpString(objectName)).AppendLine(",");
            builder.Append("            ").Append(Number(variation)).AppendLine(",");
            builder.Append("            ").Append(Literal(low)).AppendLine(",");
            builder.Append("            ").Append(Literal(high)).AppendLine(",");
            AppendIntArray(builder, stages);
            builder.Append("            ").Append(Number(shine)).AppendLine(",");
            builder.Append("            ").Append(art.CastsAShadow ? "true" : "false").AppendLine(",");
            AppendColour(builder, wash);
            AppendColour(builder, art.GlowColour);
            AppendColour(builder, art.GroundGlowColour);
            builder.Append("            ")
                .Append(art.GlowsOnlyWhenRipe ? "true" : "false").AppendLine(",");
            builder.Append("            ")
                .Append(art.IgnoresTorchlight ? "true" : "false").AppendLine(",");
            builder.Append("            ").Append(Number(ripeSound)).AppendLine(",");
            builder.Append("            ").Append(Number(ripePuff)).AppendLine("));");
        }

        /// <summary>
        /// A seed's row, which is the same shape with the growing half left empty.
        /// </summary>
        /// <remarks>
        /// No stages, no twinkle and no glow: a seed in the soil is one still picture with a damp
        /// version of itself, and nothing in the game asks it for anything else. The shadow setting
        /// is shared with the plant because a crop that lays no shadow should not start laying one
        /// for the two minutes before it sprouts.
        /// </remarks>
        private static void AppendSeedRow(
            StringBuilder builder,
            string objectName,
            int variation,
            string addressSeed,
            DimensionPlantArtTemplate art,
            Color wash)
        {
            long low;
            long high;
            DimensionPlantSpriteAssetUtility.AddressFor(addressSeed, out low, out high);

            builder.AppendLine("    DimensionPlantPresentationRegistry.Register(");
            builder.AppendLine("        new DimensionPlantPresentationDefinition(");
            builder.Append("            ").Append(ToCSharpString(objectName)).AppendLine(",");
            builder.Append("            ").Append(Number(variation)).AppendLine(",");
            builder.Append("            ").Append(Literal(low)).AppendLine(",");
            builder.Append("            ").Append(Literal(high)).AppendLine(",");
            builder.AppendLine("            null,");
            builder.AppendLine("            0,");
            builder.Append("            ").Append(art.CastsAShadow ? "true" : "false").AppendLine(",");
            AppendColour(builder, wash);
            AppendColour(builder, Color.black);
            AppendColour(builder, Color.black);
            builder.AppendLine("            true,");
            builder.AppendLine("            false,");
            builder.AppendLine("            0,");
            builder.AppendLine("            -1));");
        }

        private static void AppendIntArray(StringBuilder builder, int[] values)
        {
            if (values == null || values.Length == 0)
            {
                builder.AppendLine("            null,");
                return;
            }

            builder.Append("            new int[] { ");
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Number(values[i]));
            }

            builder.AppendLine(" },");
        }

        /// <summary>
        /// A colour written out in full rather than by name.
        /// </summary>
        /// <remarks>
        /// The float form is used because a glow colour is regularly above one: Core Keeper's
        /// emissive colours are high dynamic range, and vanilla's glow tulip throws light at 3.4.
        /// A byte colour would clamp that to a dim white.
        /// </remarks>
        private static void AppendColour(StringBuilder builder, Color colour)
        {
            builder.Append("            new Color(")
                .Append(Float(colour.r)).Append(", ")
                .Append(Float(colour.g)).Append(", ")
                .Append(Float(colour.b)).Append(", ")
                .Append(Float(colour.a)).AppendLine("),");
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }

        /// <summary>The animation name for each stage, as the numbers the sprite system stores.</summary>
        private static int[] StageAnimationsFor(int count)
        {
            int[] stages = new int[count < 1 ? 1 : count];
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i] = SpriteAsset.StringToHash(
                    DimensionPlantSpriteAssetUtility.StageAnimationName(i));
            }

            return stages;
        }

        /// <summary>
        /// The number of the puff a crop throws up as it ripens.
        /// </summary>
        /// <remarks>
        /// A name that is not one of the game's puffs falls back to leaves rather than to nothing:
        /// a typo should cost a modder the puff they wanted, not the one every crop has. The
        /// generator reports the typo separately.
        /// </remarks>
        private static int RipePuffNumber(string puffId)
        {
            if (string.IsNullOrEmpty(puffId))
            {
                return (int)DefaultRipePuff;
            }

            PuffID puff;
            return Enum.TryParse(puffId, false, out puff) ? (int)puff : (int)DefaultRipePuff;
        }
    }
}
