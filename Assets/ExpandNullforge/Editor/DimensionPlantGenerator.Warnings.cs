using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Plants;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What the generator says when a crop is authored in a way that cannot work.
    /// </summary>
    internal static partial class DimensionPlantGenerator
    {
        private static void Warn(List<string> warnings, string message)
        {
            if (warnings != null)
            {
                warnings.Add(message);
            }
        }

        /// <summary>
        /// Names every crop version that is turned off, because the first one anybody adds is
        /// turned off without their ever having said so.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE TRAP. <c>DimensionCropVersionTemplate.enabled</c> starts as <c>true</c> in C#, but
        /// Unity does not run field initialisers for a row added to an EMPTY list — it fills the
        /// new row with zeroes. So the very first version anybody adds arrives turned off, and
        /// <c>EnabledVersions</c> drops it: the author writes a golden variant, draws it, gives it
        /// a chance, generates, and nothing about it reaches the game. It is the same zero-fill
        /// that made the first version's colour wash a multiply-by-black.
        /// </para>
        /// <para>
        /// A WARNING RATHER THAN A QUIET REPAIR, deliberately. Turning a version off is a real
        /// thing to want — it is how you set one aside without deleting it — and no rule can tell
        /// a row that was zero-filled from one that was switched off on purpose once the author has
        /// begun filling it in. The honest fix for the field itself is to store "turned off"
        /// instead of "turned on", so a zero-filled row means the same as an untouched one; that is
        /// a change to what is already saved in every project, so it is written up rather than made
        /// here. Until then, nothing is silent.
        /// </para>
        /// </remarks>
        private static void WarnAboutVersionsThatAreTurnedOff(
            DimensionPlantAsset plant,
            DimensionPlantGenerationReport report)
        {
            DimensionCropVersionTemplate[] all = plant.Versions;
            if (all == null)
            {
                return;
            }

            for (int i = 0; i < all.Length; i++)
            {
                DimensionCropVersionTemplate version = all[i];
                if (version == null || version.Enabled)
                {
                    continue;
                }

                string name = string.IsNullOrEmpty(version.VersionName)
                    ? "version " + (i + 1)
                    : "'" + version.VersionName + "'";
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' has a crop version, " + name + ", that is turned " +
                    "off, so nothing about it reaches the game. If you did not turn it off, tick " +
                    "its Enabled box: a version added to an empty list starts unticked whatever " +
                    "the field says it defaults to.");
            }
        }

        private static void WarnAboutPlant(
            DimensionPlantAsset plant,
            DimensionCropVersionTemplate[] versions,
            DimensionPlantGenerationReport report)
        {
            if (plant.HarvestGivesNothing)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' has nothing to harvest, so it will grow and then " +
                    "give the player nothing when picked. Name an item under Produce item.");
            }

            if (plant.SpreadsNowhere)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' spreads on its own but names no ground to spread " +
                    "onto, so it will never spread anywhere. Add a tileset it may spread across.");
            }

            if (plant.IsReadyImmediately && plant.IsPlanted)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' takes no time to grow, so it is harvestable the " +
                    "instant it is planted. Give it some minutes to grow.");
            }

            if (!plant.IsPlanted && plant.Versions.Length > 0)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' spreads on its own, so nothing ever plants it and " +
                    "its better versions can never come up. Set it to be planted, or remove them.");
            }

            WarnAboutVersionsThatAreTurnedOff(plant, report);

            float running = 0f;
            for (int i = 0; i < versions.Length; i++)
            {
                DimensionCropVersionTemplate version = versions[i];
                if (i > 0 && version.UsesTheGamesGoldenChance)
                {
                    report.Warnings.Add(
                        "'" + plant.DisplayName + "' asks the game to roll '" + version.VersionName +
                        "', but the game only has one golden roll and the first version already has " +
                        "it. Give '" + version.VersionName + "' a chance of its own instead.");
                }

                if (version.NeverComesUp)
                {
                    report.Warnings.Add(
                        "'" + plant.DisplayName + "' has a version called '" + version.VersionName +
                        "' with no chance of coming up. Give it a chance above zero, or turn it off.");
                }

                bool ours = !(i == 0 && version.UsesTheGamesGoldenChance);
                if (!ours || version.ChancePercent <= 0f)
                {
                    continue;
                }

                if (running >= 100f)
                {
                    report.Warnings.Add(
                        "'" + plant.DisplayName + "' has versions above it adding up to a hundred " +
                        "already, so '" + version.VersionName + "' can never come up. Lower the " +
                        "chances of the versions listed before it.");
                }

                running += version.ChancePercent;
            }

            if (running > 100f)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' has better versions adding up to " +
                    running.ToString("0.#") + " in a hundred, which is more plantings than there " +
                    "are. Lower them until they add up to a hundred or less.");
            }
        }

        /// <summary>
        /// Everything about a crop's pictures that will fail quietly at runtime.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE COUNT IS THE ONE THAT COSTS PEOPLE AN EVENING. Core Keeper counts a plant's stages
        /// from zero up to and including its top stage, so a crop with two growth stages shows
        /// three pictures. This is the same off-by-one that <c>ObjectInfo.additionalSprites</c> has
        /// to satisfy on every vanilla plant prefab — one entry per stage, ripe included — and a
        /// list one short leaves a ripe crop showing its half-grown picture with no error anywhere.
        /// </para>
        /// <para>
        /// WHAT IS DELIBERATELY NOT WARNED ABOUT: having more than three growth stages. Core
        /// Keeper's own plant renderer keeps a fixed array of four animations and plays number
        /// <c>stage + 1</c>, so a fourth look reads off the end of it — but that renderer is not
        /// the one a generated crop uses. <see cref="DimensionPlantView"/> indexes a list that is
        /// exactly as long as the crop has stages, so the limit is genuinely not there and a
        /// warning about it would be a warning about somebody else's code.
        /// </para>
        /// <para>
        /// A ROW THAT DOES NOT DIVIDE is the other silent one. The game slices a row by width
        /// alone, so a strip of three pictures 50 pixels wide plays sixteen-and-two-thirds pixels
        /// of each and drifts sideways as it goes.
        /// </para>
        /// </remarks>
        private static void WarnAboutArt(
            DimensionPlantAsset plant,
            DimensionCropVersionTemplate[] versions,
            DimensionPlantGenerationReport report)
        {
            DimensionPlantArtTemplate art = plant.Art;
            string who = "'" + plant.DisplayName + "' ";
            int needed = plant.PicturesNeeded;

            if (!art.HasPlantPictures)
            {
                report.Warnings.Add(
                    who + "has nothing drawn for it, so it will be invisible in the ground the " +
                    "whole time it grows. Give it " + needed + " pictures under Look, one for each " +
                    "stage from just sprouted to ripe.");
            }
            else
            {
                WarnAboutPictureList(
                    who, "grows through " + plant.GrowthStages + " stages",
                    art.GrowthPictures, needed, art, report);
            }

            if (art.ShinePicture != null &&
                art.ShinePicture.width % art.PicturesInTheShineRow != 0)
            {
                report.Warnings.Add(
                    who + "has a twinkle " + art.ShinePicture.width + " pixels wide, which does " +
                    "not divide into " + art.PicturesInTheShineRow + " pictures. Every picture in " +
                    "a row has to be the same width, so trim it or change the count.");
            }

            if (plant.IsPlanted && !art.HasSeedPicture)
            {
                report.Warnings.Add(
                    who + "has no picture for its seed, so a player will plant it and see bare " +
                    "soil until it sprouts. Draw the seed in the ground under Look.");
            }

            if (art.SeedInWetGroundPicture != null && !art.HasSeedPicture)
            {
                report.Warnings.Add(
                    who + "has a watered seed picture but no dry one. The dry picture is the one " +
                    "the game asks for by default, so nothing will be drawn either way.");
            }

            if (!string.IsNullOrEmpty(art.RipePuffId))
            {
                PuffID puff;
                if (!Enum.TryParse(art.RipePuffId, false, out puff))
                {
                    report.Warnings.Add(
                        who + "throws up '" + art.RipePuffId + "' when it ripens, which is not " +
                        "one of the game's own bursts. It will throw up leaves instead.");
                }
            }

            // The puff had this check and the sound beside it did not, so a mistyped ripening
            // sound generated cleanly and ripened in silence.
            DimensionSoundNames.WarnIfUnknown(
                art.RipeSoundId,
                "'" + plant.DisplayName + "'",
                message => report.Warnings.Add(message));

            for (int i = 0; i < versions.Length; i++)
            {
                DimensionCropVersionTemplate version = versions[i];
                DimensionCropVersionLookTemplate look = version.Look;
                string versionWho = who + "version '" + version.VersionName + "' ";
                if (!look.LooksDifferent)
                {
                    report.Warnings.Add(
                        versionWho + "draws nothing of its own, so it will look exactly like the " +
                        "ordinary crop and nobody will be able to tell they found one. Give it " +
                        "its own pictures, or at least a colour wash.");
                    continue;
                }

                if (look.HasPlantPictures)
                {
                    WarnAboutPictureList(
                        versionWho, "has to match the ordinary crop", look.GrowthPictures, needed,
                        art, report);
                }
            }
        }

        /// <summary>The checks a list of stage pictures has to pass, wherever it came from.</summary>
        private static void WarnAboutPictureList(
            string who,
            string why,
            Texture2D[] pictures,
            int needed,
            DimensionPlantArtTemplate art,
            DimensionPlantGenerationReport report)
        {
            if (pictures.Length != needed)
            {
                report.Warnings.Add(
                    who + why + ", so it needs " + needed + " pictures — one for each stage, and " +
                    "the last one is the ripe plant. It has " + pictures.Length + ". " +
                    (pictures.Length < needed
                        ? "The stages past the end will show nothing."
                        : "The extra ones will never be shown."));
            }

            for (int i = 0; i < pictures.Length && i < needed; i++)
            {
                if (pictures[i] == null)
                {
                    report.Warnings.Add(
                        who + "has no picture for stage " + (i + 1) + " of " + needed +
                        ", so the plant will be invisible for that whole stage.");
                    continue;
                }

                int count = art.PicturesInRow(i);
                if (pictures[i].width % count != 0)
                {
                    report.Warnings.Add(
                        who + "has a stage " + (i + 1) + " picture " + pictures[i].width +
                        " pixels wide, which does not divide into " + count + " pictures. Every " +
                        "picture in a row has to be the same width, so trim it or change the count.");
                }
            }
        }

        /// <summary>
        /// Says so when a reference is neither a game item nor spelled like a mod item.
        /// </summary>
        /// <remarks>
        /// The one mistake that can still be caught from here. A mod's own items carry the mod's name
        /// in front of them, so a bare word that is not one of the game's own ObjectIDs resolves to
        /// nothing when the world loads — silently, since there is no id to be wrong about.
        /// </remarks>
        private static void WarnAboutReference(
            DimensionPlantAsset plant,
            string itemId,
            string what,
            DimensionPlantGenerationReport report)
        {
            if (string.IsNullOrEmpty(itemId) || itemId.IndexOf(':') >= 0)
            {
                return;
            }

            if (DimensionObjectBinder.Vanilla(itemId) != ObjectID.None)
            {
                return;
            }

            report.Warnings.Add(
                "'" + plant.DisplayName + "' " + what + " '" + itemId + "', which is not one of the " +
                "game's own items. If it is one of yours, write it with your mod in front of it, " +
                "like 'MyMod:" + itemId + "'. As it is, it will give nothing.");
        }
    }
}
