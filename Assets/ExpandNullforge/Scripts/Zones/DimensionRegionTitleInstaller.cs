using System.Collections.Generic;
using ExpandNullforge.Foundation;
using I2.Loc;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Puts custom biomes' title cards into Core Keeper's own region-title handler, so they announce
    /// themselves exactly the way vanilla biomes do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NOTHING IS PATCHED HERE, AND THAT IS THE POINT. <c>RegionTitleHandler.regionTitles</c> is an
    /// ordinary public list that the handler searches by biome. Appending to it means custom biomes get
    /// the real thing: the fade in and out, the music ducking, the discovery sound, the flanking icons,
    /// the name on the map, the gamepad light — all of it Core Keeper's, all of it staying correct if
    /// the game changes how any of that looks.
    /// </para>
    /// <para>
    /// The one thing the handler needs from us is for the player's biome to actually BE the custom
    /// biome, which is <see cref="DimensionCurrentBiomeSystem"/>'s job.
    /// </para>
    /// </remarks>
    public static class DimensionRegionTitleInstaller
    {
        private static RegionTitleHandler installedInto;
        private static int installedCount;

        /// <summary>How many custom titles the last install added.</summary>
        public static int InstalledCount
        {
            get { return installedCount; }
        }

        /// <summary>
        /// Installs the titles into whichever handler the current scene has, once per handler.
        /// </summary>
        /// <remarks>
        /// Keyed on the handler instance rather than a "done" flag, because loading a second world
        /// builds a second handler — and a flag would leave that world with no custom titles at all,
        /// with nothing to explain why the first world had them.
        /// </remarks>
        public static void EnsureInstalled(RegionTitleHandler handler)
        {
            if (handler == null || !DimensionRegionTitleRegistry.HasAny)
            {
                return;
            }

            if (ReferenceEquals(installedInto, handler))
            {
                return;
            }

            if (handler.regionTitles == null)
            {
                handler.regionTitles = new List<RegionTitleHandler.RegionTitle>();
            }

            installedCount = 0;
            IReadOnlyList<DimensionRegionTitleDefinition> definitions = DimensionRegionTitleRegistry.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (TryInstall(handler, definitions[i]))
                {
                    installedCount++;
                }
            }

            installedInto = handler;

            if (installedCount > 0)
            {
                DimensionFrameworkLog.Info(
                    "Added " + installedCount + " biome title card(s) to the game's own handler.");
            }
        }

        private static bool TryInstall(RegionTitleHandler handler, DimensionRegionTitleDefinition definition)
        {
            Biome biome = DimensionBiomeIdentity.GetOrAssign(definition.BiomeId);
            if (biome == Biome.None)
            {
                return false;
            }

            // A second entry for the same biome would never be reached — the handler returns the first
            // match — so replacing keeps a re-install from leaving a stale title in place.
            for (int i = 0; i < handler.regionTitles.Count; i++)
            {
                if (handler.regionTitles[i] != null && handler.regionTitles[i].biome == biome)
                {
                    handler.regionTitles[i] = Build(definition, biome);
                    return true;
                }
            }

            handler.regionTitles.Add(Build(definition, biome));
            return true;
        }

        private static RegionTitleHandler.RegionTitle Build(
            DimensionRegionTitleDefinition definition,
            Biome biome)
        {
            List<Tileset> required = new List<Tileset>();
            for (int i = 0; i < definition.RequiredTilesetIds.Count; i++)
            {
                // Cast straight through: the game builds its nearby-tile counts from the raw tileset
                // number on each tile, so a custom id appears in that map under exactly this value.
                required.Add((Tileset)definition.RequiredTilesetIds[i]);
            }

            return new RegionTitleHandler.RegionTitle
            {
                biome = biome,
                requiredTilesets = required,
                title = new LocalizedString(definition.TitleTerm),
                color = definition.Color,
                icon = ResolveIcon(definition.IconObjectName)
            };
        }

        /// <summary>
        /// The sprite to flank the title with, or null to show none.
        /// </summary>
        /// <remarks>
        /// A missing icon is not worth failing over — vanilla draws the icons at whatever sprite it is
        /// given and simply shows nothing for null, so a typo costs an ornament rather than the title.
        /// </remarks>
        private static Sprite ResolveIcon(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            ObjectID objectID = PugMod.API.Authoring.GetObjectID(objectName);
            if (objectID == ObjectID.None)
            {
                DimensionFrameworkLog.Warning(
                    "Biome title icon '" + objectName +
                    "' is not a registered object, so the title will show without icons.");
                return null;
            }

            ObjectInfo info = PugDatabase.GetObjectInfo(objectID);
            return info == null ? null : info.icon;
        }

        /// <summary>Forgets which handler was filled. The runtime does not need it; tests do.</summary>
        public static void ResetForNewScene()
        {
            installedInto = null;
            installedCount = 0;
        }
    }
}
