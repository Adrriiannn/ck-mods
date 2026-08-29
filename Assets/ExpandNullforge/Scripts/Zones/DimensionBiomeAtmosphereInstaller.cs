using System.Collections.Generic;
using ExpandNullforge.Foundation;
using ExpandNullforge.Portals;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Hands custom biomes' ambience and music to Core Keeper's own audio handlers, so a custom biome
    /// sounds like part of the game rather than like a mod playing over it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both handlers keep their contents in ordinary public lists — <c>ambientSounds</c> and
    /// <c>subBiomeMusics</c> — matched against the same nearby-tile counts everything else in this
    /// area uses. Appending to them buys the whole behaviour: distance falloff, the volume ramp as a
    /// biome fills more of the screen, the stereo direction, the mixer routing, the music cross-fade
    /// and its cooldowns. None of it is reimplemented here.
    /// </para>
    /// <para>
    /// THE ONE PIECE OF TIMING THAT MATTERS. The ambience handler compiles its tile lookup once, in
    /// <c>Awake</c>, from whatever is in the list at that moment. Appending afterwards leaves entries
    /// that exist but are never chosen — silent, with nothing logged. So ambience installs from a
    /// PREFIX on that method; music has no such table and installs after.
    /// </para>
    /// </remarks>
    public static class DimensionBiomeAtmosphereInstaller
    {
        /// <summary>
        /// The audio info objects this mod owns.
        /// </summary>
        /// <remarks>
        /// Tracked because Core Keeper streams ambience clips in and out through Addressables as the
        /// volume crosses a threshold, and ours are not Addressables assets — they are already loaded
        /// by the framework's own clip cache. <see cref="DimensionAmbienceAssetHook"/> uses this set to
        /// leave ours alone, which is the difference between working and an error every few seconds.
        /// </remarks>
        private static readonly HashSet<object> OwnedAudioInfos = new HashSet<object>();

        private static AmbientSoundsHandler installedAmbienceInto;
        private static GameMusicHandler installedMusicInto;

        /// <summary>How many ambience loops the last install added.</summary>
        public static int InstalledAmbienceCount { get; private set; }

        /// <summary>How many biome-to-music-roster bindings the last install added.</summary>
        public static int InstalledMusicCount { get; private set; }

        public static bool IsOwned(object audioInfo)
        {
            return audioInfo != null && OwnedAudioInfos.Contains(audioInfo);
        }

        /// <summary>
        /// Adds this mod's ambience loops to the handler's list, before it compiles its lookup.
        /// </summary>
        public static void EnsureAmbienceInstalled(AmbientSoundsHandler handler)
        {
            if (handler == null ||
                !DimensionBiomeAtmosphereRegistry.HasAny ||
                ReferenceEquals(installedAmbienceInto, handler))
            {
                return;
            }

            if (handler.ambientSounds == null)
            {
                handler.ambientSounds = new List<AmbientSoundsHandler.AmbientSound>();
            }

            // Every setting that makes an ambient source sound right — the mixer group, the spatial
            // blend, the rolloff, the parent transform the handler moves around — is already correct
            // on the game's own sources. Copying one is more faithful than guessing at a dozen values,
            // and it keeps working if the game retunes them.
            AudioSource template = FindTemplateSource(handler);
            if (template == null)
            {
                DimensionFrameworkLog.Warning(
                    "The game has no ambient sound to copy settings from, so custom " +
                    "biome ambience was skipped. Everything else about the biome still works.");
                return;
            }

            InstalledAmbienceCount = 0;
            IReadOnlyList<DimensionBiomeAtmosphereDefinition> definitions = DimensionBiomeAtmosphereRegistry.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (TryInstallAmbience(handler, definitions[i], template))
                {
                    InstalledAmbienceCount++;
                }
            }

            installedAmbienceInto = handler;

            if (InstalledAmbienceCount > 0)
            {
                DimensionFrameworkLog.Info(
                    "Added " + InstalledAmbienceCount +
                    " biome ambience loop(s) to the game's own mix.");
            }
        }

        /// <summary>
        /// Binds each custom biome to the music roster it asked for.
        /// </summary>
        /// <remarks>
        /// Sub-biome music is checked before the game's own biome switch and wins, which is exactly
        /// what a custom biome needs — that switch is a hardcoded list of vanilla biomes and would
        /// otherwise fall through to silence.
        /// </remarks>
        public static void EnsureMusicInstalled(GameMusicHandler handler)
        {
            if (handler == null ||
                !DimensionBiomeAtmosphereRegistry.HasAny ||
                ReferenceEquals(installedMusicInto, handler))
            {
                return;
            }

            if (handler.subBiomeMusics == null)
            {
                handler.subBiomeMusics = new List<GameMusicHandler.SubBiomeMusic>();
            }

            InstalledMusicCount = 0;
            IReadOnlyList<DimensionBiomeAtmosphereDefinition> definitions = DimensionBiomeAtmosphereRegistry.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (TryInstallMusic(handler, definitions[i]))
                {
                    InstalledMusicCount++;
                }
            }

            installedMusicInto = handler;

            if (InstalledMusicCount > 0)
            {
                DimensionFrameworkLog.Info(
                    "Bound " + InstalledMusicCount + " custom biome(s) to a music roster.");
            }
        }

        private static bool TryInstallAmbience(
            AmbientSoundsHandler handler,
            DimensionBiomeAtmosphereDefinition definition,
            AudioSource template)
        {
            if (!definition.HasAmbience || definition.TilesetIds.Count == 0)
            {
                return false;
            }

            AudioClip clip = DimensionPortalSoundRegistry.TryGetLoopClip(definition.AmbienceSoundKey);
            if (clip == null)
            {
                // The clip cache loads asynchronously, so a miss here is usually "not yet" rather than
                // "never". Ambience is installed once at scene start, so a slow clip simply means this
                // biome is quiet for this session — worth a line, not worth failing the scene over.
                DimensionFrameworkLog.Warning(
                    "Ambience '" + definition.AmbienceSoundKey + "' for biome '" +
                    definition.BiomeId + "' was not ready, so that biome has no ambience this session.");
                return false;
            }

            GameObject holder = new GameObject("NullforgeAmbience_" + definition.BiomeId);
            holder.transform.SetParent(template.transform.parent, false);

            AudioSource source = holder.AddComponent<AudioSource>();
            CopySettings(template, source);
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.playOnAwake = false;
            source.Play();

            AmbientSoundsHandler.AudioInfo audioInfo = new AmbientSoundsHandler.AudioInfo
            {
                audio = source,
                volumeMultiply = definition.AmbienceVolume
            };

            OwnedAudioInfos.Add(audioInfo);

            List<AmbientSoundsHandler.ContributingTiles> contributing =
                new List<AmbientSoundsHandler.ContributingTiles>();
            for (int i = 0; i < definition.TilesetIds.Count; i++)
            {
                Tileset tileset = (Tileset)definition.TilesetIds[i];

                // Ground and wall both contribute, the way vanilla's own entries do: standing in a
                // corridor cut through a biome should still sound like that biome.
                contributing.Add(new AmbientSoundsHandler.ContributingTiles
                {
                    tileType = TileType.ground,
                    tileset = tileset
                });
                contributing.Add(new AmbientSoundsHandler.ContributingTiles
                {
                    tileType = TileType.wall,
                    tileset = tileset
                });
            }

            handler.ambientSounds.Add(new AmbientSoundsHandler.AmbientSound
            {
                audioInfo = audioInfo,
                contributingTiles = contributing
            });

            return true;
        }

        private static bool TryInstallMusic(
            GameMusicHandler handler,
            DimensionBiomeAtmosphereDefinition definition)
        {
            if (!definition.HasMusic || definition.TilesetIds.Count == 0)
            {
                return false;
            }

            MusicRosterType roster;
            if (!System.Enum.TryParse(definition.MusicRosterName, false, out roster))
            {
                // A registered cue of the mod's own answers to its name here too, so a biome
                // can play the same custom music a boss fight registered.
                if (DimensionMusicRosterRegistry.IsCue(definition.MusicRosterName))
                {
                    roster = (MusicRosterType)DimensionMusicRosterIds.For(definition.MusicRosterName);
                }
                else
                {
                    DimensionFrameworkLog.Warning(
                        "Biome '" + definition.BiomeId + "' asked for music roster '" +
                        definition.MusicRosterName + "', which this version of the game does not have. " +
                        "That biome will play whatever music the area around it plays.");
                    return false;
                }
            }

            Biome biome = DimensionBiomeIdentity.GetOrAssign(definition.BiomeId);
            if (biome == Biome.None)
            {
                return false;
            }

            List<TileTypeAndTileset> tiles = new List<TileTypeAndTileset>();
            for (int i = 0; i < definition.TilesetIds.Count; i++)
            {
                Tileset tileset = (Tileset)definition.TilesetIds[i];
                tiles.Add(new TileTypeAndTileset(TileType.ground, tileset));
                tiles.Add(new TileTypeAndTileset(TileType.wall, tileset));
            }

            handler.subBiomeMusics.Add(new GameMusicHandler.SubBiomeMusic
            {
                biomes = new List<Biome> { biome },
                tiles = tiles,
                roster = roster
            });

            return true;
        }

        /// <summary>An existing ambient source to copy configuration from, or null if there is none.</summary>
        private static AudioSource FindTemplateSource(AmbientSoundsHandler handler)
        {
            for (int i = 0; i < handler.ambientSounds.Count; i++)
            {
                AmbientSoundsHandler.AmbientSound existing = handler.ambientSounds[i];
                if (existing != null && existing.audioInfo != null && existing.audioInfo.audio != null)
                {
                    return existing.audioInfo.audio;
                }
            }

            return null;
        }

        private static void CopySettings(AudioSource from, AudioSource to)
        {
            to.outputAudioMixerGroup = from.outputAudioMixerGroup;
            to.spatialBlend = from.spatialBlend;
            to.rolloffMode = from.rolloffMode;
            to.minDistance = from.minDistance;
            to.maxDistance = from.maxDistance;
            to.dopplerLevel = from.dopplerLevel;
            to.spread = from.spread;
            to.priority = from.priority;
            to.bypassEffects = from.bypassEffects;
            to.bypassListenerEffects = from.bypassListenerEffects;
            to.bypassReverbZones = from.bypassReverbZones;
        }

        /// <summary>Forgets which handlers were filled. The runtime does not need it; tests do.</summary>
        public static void ResetForNewScene()
        {
            installedAmbienceInto = null;
            installedMusicInto = null;
            InstalledAmbienceCount = 0;
            InstalledMusicCount = 0;
            OwnedAudioInfos.Clear();
        }
    }
}
