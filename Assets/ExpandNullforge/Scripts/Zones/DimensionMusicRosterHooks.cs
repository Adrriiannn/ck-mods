using System.Collections.Generic;
using ExpandNullforge.Foundation;
using HarmonyLib;
using UnityEngine;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Stable roster ids for the mod's own music, from the tileset-identity playbook.
    /// </summary>
    /// <remarks>
    /// FNV-1a of the cue name folded into [1000, int.MaxValue). Vanilla's highest roster value
    /// is under fifty, so everything at a thousand and up is clear water; the id is derived,
    /// never allocated, so every world and every session agrees on it without a registry file.
    /// Collisions are astronomically unlikely and deliberately NOT probed for — the tileset
    /// identity work established that probing turns a stable id into a load-order-dependent
    /// one, which is the worse bug.
    /// </remarks>
    public static class DimensionMusicRosterIds
    {
        public const int FirstCustomId = 1000;

        public static int For(string cueName)
        {
            if (string.IsNullOrEmpty(cueName))
            {
                return 0;
            }

            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < cueName.Length; i++)
                {
                    hash = (hash ^ cueName[i]) * 1099511628211UL;
                }

                ulong span = (ulong)(int.MaxValue - FirstCustomId);
                return FirstCustomId + (int)(hash % span);
            }
        }
    }

    /// <summary>One authored playlist: a name and the clips that are it.</summary>
    public sealed class DimensionMusicCueDefinition
    {
        public DimensionMusicCueDefinition(string cueName, IReadOnlyList<string> clipKeys)
        {
            CueName = cueName ?? string.Empty;
            ClipKeys = clipKeys ?? System.Array.Empty<string>();
            RosterId = DimensionMusicRosterIds.For(CueName);
        }

        public readonly string CueName;

        /// <summary>Clip keys the framework's clip cache resolves — paths, the way sounds are named.</summary>
        public readonly IReadOnlyList<string> ClipKeys;

        public readonly int RosterId;
    }

    /// <summary>
    /// The mod's own music rosters, and the two seams that make them play.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game's music manager searches a public roster list linearly by value, so appending a
    /// roster with a custom id integrates selection completely — a music area or sub-biome
    /// naming the custom id just works. The one thing that cannot ride vanilla is the LOADING:
    /// tracks are Addressables references, and mod clips have none. So the appended roster
    /// carries placeholder tracks (the count drives the game's index roll), and the PlayMusic
    /// prefix below feeds the real clip straight into the audio source.
    /// </para>
    /// </remarks>
    public static class DimensionMusicRosterRegistry
    {
        private static readonly List<DimensionMusicCueDefinition> Cues =
            new List<DimensionMusicCueDefinition>();

        private static MusicManager installedInto;

        public static bool HasAny
        {
            get { return Cues.Count > 0; }
        }

        public static void RegisterCue(string cueName, IReadOnlyList<string> clipKeys)
        {
            if (string.IsNullOrEmpty(cueName) || clipKeys == null || clipKeys.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Cues.Count; i++)
            {
                if (string.Equals(Cues[i].CueName, cueName, System.StringComparison.Ordinal))
                {
                    Cues[i] = new DimensionMusicCueDefinition(cueName, clipKeys);
                    installedInto = null;
                    return;
                }
            }

            Cues.Add(new DimensionMusicCueDefinition(cueName, clipKeys));
            installedInto = null;
        }

        /// <summary>Whether a name is one of the mod's cues rather than a vanilla roster.</summary>
        public static bool IsCue(string name)
        {
            for (int i = 0; i < Cues.Count; i++)
            {
                if (string.Equals(Cues[i].CueName, name, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Appends every cue as a roster the game's selection can find. Idempotent per manager.</summary>
        public static void EnsureInstalled(MusicManager music)
        {
            if (music == null || music.musicRosters == null || Cues.Count == 0 ||
                ReferenceEquals(installedInto, music))
            {
                return;
            }

            for (int i = 0; i < Cues.Count; i++)
            {
                DimensionMusicCueDefinition cue = Cues[i];
                List<MusicManager.MusicTrack> tracks = new List<MusicManager.MusicTrack>();
                for (int t = 0; t < cue.ClipKeys.Count; t++)
                {
                    // Placeholders: the count is what the game rolls its track index from; the
                    // PlayMusic prefix supplies the actual clip.
                    tracks.Add(new MusicManager.MusicTrack());
                }

                music.musicRosters.Add(new MusicManager.MusicRoster
                {
                    rosterType = (MusicRosterType)cue.RosterId,
                    musicType = MusicType.Dungeon,
                    tracks = tracks
                });
            }

            installedInto = music;
        }

        /// <summary>The clip for a roster and track index, or null when the roster is vanilla's.</summary>
        public static AudioClip ClipFor(MusicRosterType roster, int index)
        {
            for (int i = 0; i < Cues.Count; i++)
            {
                DimensionMusicCueDefinition cue = Cues[i];
                if ((int)roster != cue.RosterId)
                {
                    continue;
                }

                if (cue.ClipKeys.Count == 0)
                {
                    return null;
                }

                int clamped = index;
                if (clamped < 0 || clamped >= cue.ClipKeys.Count)
                {
                    clamped = 0;
                }

                AudioClip clip = Portals.DimensionPortalSoundRegistry.TryGetLoopClip(cue.ClipKeys[clamped]);
                if (clip == null)
                {
                    DimensionFrameworkLog.Warning(
                        "Music cue '" + cue.CueName + "' track '" +
                        cue.ClipKeys[clamped] + "' resolved no clip, so that track is silent.");
                }

                return clip;
            }

            return null;
        }

        public static void Clear()
        {
            Cues.Clear();
            installedInto = null;
        }
    }

    /// <summary>Installs the mod's rosters when the music handler wakes, beside the biome music.</summary>
    [HarmonyPatch(typeof(GameMusicHandler), "Start")]
    public static class DimensionMusicRosterInstallHook
    {
        private static void Postfix()
        {
            DimensionMusicRosterRegistry.EnsureInstalled(Manager.music);
        }
    }

    /// <summary>
    /// Plays a custom roster's track from the mod's own clips instead of Addressables.
    /// </summary>
    /// <remarks>
    /// Vanilla's PlayMusic loads through an Addressables reference our placeholder tracks do
    /// not have — left alone it logs one error per attempt and plays silence. For a vanilla
    /// roster this prefix returns true and touches nothing.
    /// </remarks>
    [HarmonyPatch(typeof(MusicManager), nameof(MusicManager.PlayMusic))]
    public static class DimensionMusicRosterPlayHook
    {
        private static bool Prefix(MusicManager __instance, int index)
        {
            AudioClip clip = DimensionMusicRosterRegistry.ClipFor(
                __instance.currentMusicRosterType, index);
            if (clip == null)
            {
                return true;
            }

            __instance.musicAudioSource.volume = 1f;
            __instance.musicAudioSource.clip = clip;
            __instance.musicAudioSource.Play();
            __instance.musicAudioSource.UnPause();
            return false;
        }
    }
}
