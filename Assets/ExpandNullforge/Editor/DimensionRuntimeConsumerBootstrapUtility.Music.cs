using System.Text;
using ExpandNullforge.Authoring;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits the music a whole dimension plays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS IS A REGISTRATION AND NOT A MUSIC AREA. Core Keeper decides the music per frame in
    /// <c>GameMusicHandler.GetActiveMusicArea</c>, and the framework already sits on the end of
    /// that decision: <c>DimensionMusicOverrideHook</c> answers with the dimension's roster when
    /// the player stands inside its bounds and nothing higher-ranking has won. A music-area ENTITY
    /// would have been the other option and is worse in three ways — its trigger is a circle and a
    /// dimension is a rectangle, it has to out-argue a boss's own area on priority, and it would
    /// put a client-only concern into server state.
    /// </para>
    /// <para>
    /// A NAME OF YOUR OWN COSTS ONE EXTRA LINE. The mod's own rosters are appended to the game's
    /// list with derived ids, so once a cue is registered the game's own lookup finds it and
    /// nothing further is needed. The one thing a mod cannot ride is track LOADING — vanilla tracks
    /// are Addressables references a mod has none of — which is why the cue carries clip keys and
    /// the framework feeds the clip in itself.
    /// </para>
    /// <para>
    /// BOTH REGISTRATIONS ARE IDEMPOTENT ON PURPOSE. <c>RegisterCue</c> replaces a cue of the same
    /// name and <c>SetRoster</c> overwrites a dimension's entry, so emitting this block twice — for
    /// instance while the call below is being moved to its proper home — changes nothing.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>Writes the dimension's own music, when its author named any.</summary>
        internal static void AppendDimensionMusicRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template)
        {
            if (template == null || string.IsNullOrEmpty(template.Music))
            {
                return;
            }

            string musicName = template.Music;
            string dimensionId = template.DimensionId;
            if (string.IsNullOrEmpty(dimensionId))
            {
                return;
            }

            MusicRosterType vanillaRoster;
            bool isVanilla = System.Enum.TryParse(musicName, false, out vanillaRoster);
            string[] tracks = template.MusicTracks;

            if (!isVanilla)
            {
                if (!template.HasOwnMusicTracks)
                {
                    // Registering it anyway would put a name in the override table that resolves to
                    // nothing, and the runtime would fall back with a warning every session. Saying
                    // it here, once, where the author can act on it, is the honest version.
                    Debug.LogWarning(
                        "[ExpandNullforge] Dimension '" + dimensionId + "' asks for music called '" +
                        musicName + "', which is not one of the game's own music names, and lists " +
                        "no tracks of its own. Either type one of the game's names — MOLD_DUNGEON, " +
                        "MYSTERY, HOME_BASE and the rest — or list the clip keys of your own " +
                        "tracks beside it. No music was set for this dimension.");
                    return;
                }

                builder.Append("    DimensionMusicRosterRegistry.RegisterCue(")
                    .Append(ToCSharpString(musicName))
                    .Append(", new string[] { ");
                bool wroteTrack = false;
                for (int i = 0; i < tracks.Length; i++)
                {
                    if (string.IsNullOrEmpty(tracks[i]))
                    {
                        continue;
                    }

                    if (wroteTrack)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(ToCSharpString(tracks[i]));
                    wroteTrack = true;
                }

                builder.AppendLine(" });");
            }
            else if (template.HasOwnMusicTracks)
            {
                // Not fatal, and not silently obeyed either: the tracks would never be reached,
                // because a vanilla roster already knows what it plays.
                Debug.LogWarning(
                    "[ExpandNullforge] Dimension '" + dimensionId + "' asks for the game's own '" +
                    musicName + "' music and also lists tracks of its own. The game's roster " +
                    "brings its own tracks, so the listed ones are never played. Give the music a " +
                    "name of your own if you meant to use them.");
            }

            builder.Append("    DimensionMusicOverrideRegistry.SetRoster(")
                .Append(ToCSharpString(dimensionId))
                .Append(", ")
                .Append(ToCSharpString(musicName))
                .AppendLine(");");
        }
    }
}
