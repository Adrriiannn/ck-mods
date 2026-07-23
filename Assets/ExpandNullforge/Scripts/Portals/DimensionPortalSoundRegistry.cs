using System;
using System.Collections.Generic;
using PugMod;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace ExpandNullforge.Portals
{
    /// <summary>How a portal uses its configured sounds.</summary>
    public enum DimensionPortalSoundMode
    {
        /// <summary>One-shots at the edges of the portal's life: activation and deactivation.</summary>
        Peak = 0,

        /// <summary>A looping bed while the portal stands open (instant portals only).</summary>
        Loop = 1
    }

    /// <summary>A portal object's configured sounds, registered by the generated bootstrap.</summary>
    public readonly struct DimensionPortalSoundProfile
    {
        public DimensionPortalSoundProfile(
            DimensionPortalSoundMode mode,
            string activationSound,
            string deactivationSound,
            string loopSound)
        {
            Mode = mode;
            ActivationSound = activationSound ?? string.Empty;
            DeactivationSound = deactivationSound ?? string.Empty;
            LoopSound = loopSound ?? string.Empty;
        }

        public DimensionPortalSoundMode Mode { get; }
        public string ActivationSound { get; }
        public string DeactivationSound { get; }
        public string LoopSound { get; }
    }

    /// <summary>
    /// Registry and playback helper for configurable portal sounds.
    ///
    /// Sound keys accept two forms: a game <see cref="SfxID"/> name (played through
    /// <c>AudioManager.Sfx</c>, so it uses the game's sound banks and mixers), or a Sound
    /// Library key — an Addressables asset path copied from the dashboard's Sound Library
    /// window — loaded as an <see cref="AudioClip"/> through the game's own Addressables
    /// catalog. Loop sounds require the clip form (an SfxID cannot be held and stopped
    /// reliably from here).
    ///
    /// Every portal sound is deliberately short-ranged (<see cref="RangeTiles"/>): portals sit
    /// in bases and would otherwise serenade the whole server.
    /// </summary>
    public static class DimensionPortalSoundRegistry
    {
        /// <summary>How far (in tiles) portal sounds carry. Beyond this they are silent.</summary>
        public const float RangeTiles = 8f;

        private static readonly Dictionary<string, DimensionPortalSoundProfile> ProfilesByObjectName =
            new Dictionary<string, DimensionPortalSoundProfile>(StringComparer.Ordinal);

        private static readonly Dictionary<ObjectID, DimensionPortalSoundProfile> ProfilesByObjectId =
            new Dictionary<ObjectID, DimensionPortalSoundProfile>();
        private static int objectIdResolvedForProfileCount = -1;

        /// <summary>Registers (or replaces) a portal object's sound profile. Called by bootstraps.</summary>
        public static void Register(
            string portalObjectName,
            int mode,
            string activationSound,
            string deactivationSound,
            string loopSound)
        {
            if (string.IsNullOrEmpty(portalObjectName))
            {
                return;
            }

            DimensionPortalSoundMode soundMode = mode == 1
                ? DimensionPortalSoundMode.Loop
                : DimensionPortalSoundMode.Peak;
            ProfilesByObjectName[portalObjectName] = new DimensionPortalSoundProfile(
                soundMode, activationSound, deactivationSound, loopSound);

            // Warm the clip cache so a clip-key sound is ready by the time a portal first plays it.
            if (soundMode == DimensionPortalSoundMode.Loop)
            {
                PrewarmClipKey(loopSound);
            }
            else
            {
                PrewarmClipKey(activationSound);
                PrewarmClipKey(deactivationSound);
            }
        }

        /// <summary>Resolves the profile for a live portal entity from its ObjectID.</summary>
        public static bool TryGetForObjectId(ObjectID objectId, out DimensionPortalSoundProfile profile)
        {
            profile = default(DimensionPortalSoundProfile);
            if (objectId == ObjectID.None || ProfilesByObjectName.Count == 0)
            {
                return false;
            }

            if (objectIdResolvedForProfileCount != ProfilesByObjectName.Count)
            {
                ProfilesByObjectId.Clear();
                foreach (KeyValuePair<string, DimensionPortalSoundProfile> entry in ProfilesByObjectName)
                {
                    ObjectID id = API.Authoring.GetObjectID(entry.Key);
                    if (id != ObjectID.None)
                    {
                        ProfilesByObjectId[id] = entry.Value;
                    }
                }

                objectIdResolvedForProfileCount = ProfilesByObjectName.Count;
            }

            return ProfilesByObjectId.TryGetValue(objectId, out profile);
        }

        public static void Clear()
        {
            ProfilesByObjectName.Clear();
            ProfilesByObjectId.Clear();
            objectIdResolvedForProfileCount = -1;
            // Clip cache entries survive Clear on purpose: the loaded clips are game assets and
            // stay valid for the whole session, so re-registration reuses them for free.
        }

        // ---- Playback --------------------------------------------------------------------------

        /// <summary>
        /// Plays a one-shot portal sound at a position, range-limited. SfxID names go through the
        /// game's sfx system; clip keys play on a transient spatial source routed to the effects
        /// mixer. Unknown/unready keys are silent.
        /// </summary>
        public static void PlayOneShot(string soundKey, Vector3 position)
        {
            if (string.IsNullOrEmpty(soundKey))
            {
                return;
            }

            if (TryParseSfxId(soundKey, out SfxID sfxId))
            {
                AudioManager.Sfx(sfxId, position, maxSpatialDistance: RangeTiles);
                return;
            }

            AudioClip clip = TryGetClip(soundKey);
            if (clip == null)
            {
                return;
            }

            GameObject holder = new GameObject("PortalSoundOneShot");
            holder.transform.position = position;
            AudioSource source = holder.AddComponent<AudioSource>();
            ConfigureSpatialSource(source, clip, false);
            source.Play();
            UnityEngine.Object.Destroy(holder, clip.length + 0.5f);
        }

        /// <summary>
        /// The loop clip for a key, or null while loading / for non-clip keys. Callers poll.
        /// </summary>
        public static AudioClip TryGetLoopClip(string soundKey)
        {
            if (string.IsNullOrEmpty(soundKey) || TryParseSfxId(soundKey, out _))
            {
                // SfxID loops are not supported — the field UI documents the clip-key requirement.
                return null;
            }

            return TryGetClip(soundKey);
        }

        /// <summary>Applies the shared portal-sound spatial settings to a source.</summary>
        public static void ConfigureSpatialSource(AudioSource source, AudioClip clip, bool loop)
        {
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = loop;
            source.volume = 0.65f;
            source.spatialBlend = 1.0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = RangeTiles;
            source.dopplerLevel = 0.0f;

            AudioManager audioManager = Manager.audio;
            if (audioManager != null)
            {
                source.outputAudioMixerGroup = loop
                    ? audioManager.ambientMixerGroup
                    : audioManager.effectsMixerGroup;
            }
        }

        private static bool TryParseSfxId(string soundKey, out SfxID sfxId)
        {
            // Clip keys are paths ("assets/audio/....ogg") — never valid enum names, so the
            // cheap path check avoids Enum.TryParse on every call.
            sfxId = default(SfxID);
            if (soundKey.IndexOf('/') >= 0 || soundKey.IndexOf('\\') >= 0)
            {
                return false;
            }

            return Enum.TryParse(soundKey, false, out sfxId) || Enum.TryParse(soundKey, true, out sfxId);
        }

        // ---- Clip cache (Addressables) ---------------------------------------------------------

        private sealed class ClipCacheEntry
        {
            public AudioClip Clip;
            public bool Failed;
            public bool LoggedFailure;
            public int NextKeyForm;
            public string[] KeyForms;
            public bool ProbePending;
            public AsyncOperationHandle<IList<IResourceLocation>> ProbeHandle;
            public bool LoadPending;
            public AsyncOperationHandle<AudioClip> LoadHandle;
        }

        private static readonly Dictionary<string, ClipCacheEntry> ClipCache =
            new Dictionary<string, ClipCacheEntry>(StringComparer.Ordinal);

        private static void PrewarmClipKey(string soundKey)
        {
            if (!string.IsNullOrEmpty(soundKey) && !TryParseSfxId(soundKey, out _))
            {
                TryGetClip(soundKey);
            }
        }

        /// <summary>
        /// The cached clip for a key, advancing its async load one step per call. The game's
        /// binary catalog composes keys from path fragments, so a few key forms are probed via
        /// LoadResourceLocationsAsync (quiet on miss) before anything is actually loaded.
        /// </summary>
        private static AudioClip TryGetClip(string soundKey)
        {
            if (!ClipCache.TryGetValue(soundKey, out ClipCacheEntry entry))
            {
                entry = new ClipCacheEntry
                {
                    KeyForms = BuildKeyForms(soundKey)
                };
                ClipCache[soundKey] = entry;
            }

            if (entry.Clip != null || entry.Failed)
            {
                return entry.Clip;
            }

            Pump(soundKey, entry);
            return entry.Clip;
        }

        private static string[] BuildKeyForms(string soundKey)
        {
            string trimmed = soundKey.Trim().Replace('\\', '/');
            string lower = trimmed.ToLowerInvariant();
            string bareName = trimmed;
            int slash = trimmed.LastIndexOf('/');
            if (slash >= 0 && slash < trimmed.Length - 1)
            {
                bareName = trimmed.Substring(slash + 1);
            }

            int dot = bareName.LastIndexOf('.');
            if (dot > 0)
            {
                bareName = bareName.Substring(0, dot);
            }

            List<string> forms = new List<string> { trimmed };
            if (!forms.Contains(lower))
            {
                forms.Add(lower);
            }

            if (!forms.Contains(bareName))
            {
                forms.Add(bareName);
            }

            return forms.ToArray();
        }

        private static void Pump(string soundKey, ClipCacheEntry entry)
        {
            if (entry.LoadPending)
            {
                if (!entry.LoadHandle.IsDone)
                {
                    return;
                }

                entry.LoadPending = false;
                if (entry.LoadHandle.Status == AsyncOperationStatus.Succeeded &&
                    entry.LoadHandle.Result != null)
                {
                    // Handle kept for the session so the clip stays valid.
                    entry.Clip = entry.LoadHandle.Result;
                    return;
                }

                Addressables.Release(entry.LoadHandle);
                entry.Failed = true;
                LogFailureOnce(soundKey, entry);
                return;
            }

            if (entry.ProbePending)
            {
                if (!entry.ProbeHandle.IsDone)
                {
                    return;
                }

                entry.ProbePending = false;
                bool found = entry.ProbeHandle.Status == AsyncOperationStatus.Succeeded &&
                    entry.ProbeHandle.Result != null &&
                    entry.ProbeHandle.Result.Count > 0;
                IResourceLocation location = found ? entry.ProbeHandle.Result[0] : null;
                Addressables.Release(entry.ProbeHandle);

                if (found)
                {
                    entry.LoadHandle = Addressables.LoadAssetAsync<AudioClip>(location);
                    entry.LoadPending = true;
                    return;
                }
            }

            if (entry.NextKeyForm >= entry.KeyForms.Length)
            {
                entry.Failed = true;
                LogFailureOnce(soundKey, entry);
                return;
            }

            entry.ProbeHandle = Addressables.LoadResourceLocationsAsync(
                entry.KeyForms[entry.NextKeyForm], typeof(AudioClip));
            entry.NextKeyForm++;
            entry.ProbePending = true;
        }

        private static void LogFailureOnce(string soundKey, ClipCacheEntry entry)
        {
            if (entry.LoggedFailure)
            {
                return;
            }

            entry.LoggedFailure = true;
            Debug.LogWarning(
                "[ExpandNullforge] Portal sound key '" + soundKey +
                "' is neither an SfxID name nor a resolvable audio clip key; it stays silent. " +
                "Copy keys from Dimensions API ▸ Sound Library.");
        }
    }
}
