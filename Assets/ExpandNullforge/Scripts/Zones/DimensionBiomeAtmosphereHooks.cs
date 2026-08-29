using HarmonyLib;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Installs custom biome ambience before Core Keeper compiles the lookup that decides which
    /// ambient sound a tile contributes to.
    /// </summary>
    /// <remarks>
    /// A PREFIX, and it has to be. The handler builds that lookup once, in <c>Awake</c>, from whatever
    /// is in <c>ambientSounds</c> at that instant. An entry appended one line later exists, is mixed
    /// every frame, and is never once selected — silent, with nothing logged anywhere.
    /// </remarks>
    [HarmonyPatch(typeof(AmbientSoundsHandler), "Awake")]
    public static class DimensionAmbienceInstallHook
    {
        private static void Prefix(AmbientSoundsHandler __instance)
        {
            // A fresh handler means a fresh scene, so what was remembered about the last one is stale.
            DimensionBiomeAtmosphereInstaller.ResetForNewScene();
            DimensionBiomeAtmosphereInstaller.EnsureAmbienceInstalled(__instance);
        }
    }

    /// <summary>
    /// Binds custom biomes to their music roster once the music handler exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A postfix is fine here: sub-biome music is read fresh from the list every frame, so there is no
    /// compiled table to be late for.
    /// </para>
    /// <para>
    /// THE SEAM IS <c>Start</c>, NOT <c>Awake</c>. <see cref="GameMusicHandler"/> has no <c>Awake</c> at
    /// all, and naming a method that does not exist does not fail quietly: Harmony throws while binding,
    /// the loader turns that into a single log line, and every patch it had not reached yet is silently
    /// never applied. This one typo cost custom biome music and put every other hook in this mod at the
    /// mercy of assembly ordering. Verify a target exists before patching it.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(GameMusicHandler), "Start")]
    public static class DimensionBiomeMusicInstallHook
    {
        private static void Postfix(GameMusicHandler __instance)
        {
            DimensionBiomeAtmosphereInstaller.EnsureMusicInstalled(__instance);
        }
    }

    /// <summary>
    /// Keeps Core Keeper's ambience streaming from touching clips this mod already owns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game streams ambience clips in and out through Addressables as their volume crosses a
    /// threshold. Ours do not come from Addressables — the framework's own clip cache has already
    /// loaded them and assigned them — so there is nothing to stream. Left alone, the loader would
    /// find no asset reference and log an error every time the player walked near a custom biome.
    /// </para>
    /// <para>
    /// Only the load side needs stopping. The release side already returns immediately for a clip it
    /// never loaded, so patching it too would add a hook that does nothing.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(AmbientSoundsHandler.AudioInfo), nameof(AmbientSoundsHandler.AudioInfo.LoadAudioAsset))]
    public static class DimensionAmbienceAssetHook
    {
        private static bool Prefix(AmbientSoundsHandler.AudioInfo __instance)
        {
            // False skips the original. Vanilla's own ambience is untouched.
            return !DimensionBiomeAtmosphereInstaller.IsOwned(__instance);
        }
    }
}
