using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{

    /// <summary>
    /// One strip of pictures and how it plays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A STRIP IS ONE ROW, ALWAYS. Core Keeper slices a clip's texture by width only — frame n is
    /// the nth slice of <c>width / frames</c> pixels. A sheet stacked in rows cannot be read at all,
    /// and there is no setting that makes it work.
    /// </para>
    /// <para>
    /// The two extra strips are the same motion seen from behind and from the side. There is
    /// deliberately no strip for facing the camera: that is what the main strip IS, and the game
    /// asks for it by asking for no variant at all.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureClipTemplate
    {
        [Tooltip("What the creature is doing.")]
        [SerializeField] private DimensionCreatureClipKind kind = DimensionCreatureClipKind.Standing;

        [Tooltip("The pictures, side by side in one row, facing the camera.")]
        [SerializeField] private Texture2D strip;

        [Tooltip("The same motion seen from behind. Optional.")]
        [SerializeField] private Texture2D stripFromBehind;

        [Tooltip("The same motion seen from the side, facing right. Optional.")]
        [SerializeField] private Texture2D stripFromTheSide;

        [Tooltip("How many pictures are in the row. Leave at 0 to work it out from the strip.")]
        [SerializeField] private int frames;

        [Tooltip("Pictures per second.")]
        [SerializeField] private float speed = 10f;

        [Tooltip("Whether it repeats.")]
        [SerializeField] private DimensionClipRepeat repeats = DimensionClipRepeat.SameAsTheGame;

        [Tooltip("Extra ticks to linger on each picture, one number per picture. Leave empty for none.")]
        [SerializeField] private int[] holdEachPictureFor = new int[0];

        [Tooltip("Names for the moments in this clip that make a noise: the frame a foot lands, " +
                 "a claw connects, wings beat.")]
        [SerializeField] private string[] momentNames = new string[0];

        [Tooltip("Which picture each moment happens on, counting from 0.")]
        [SerializeField] private int[] momentPictures = new int[0];

        [Tooltip("The sound each moment plays, by name. Leave one empty for a silent moment.")]
        [SerializeField] private string[] momentSounds = new string[0];

        [SerializeField] private bool enabled = true;

        public DimensionCreatureClipKind Kind
        {
            get { return kind; }
        }

        public Texture2D Strip
        {
            get { return strip; }
        }

        public Texture2D StripFromBehind
        {
            get { return stripFromBehind; }
        }

        public Texture2D StripFromTheSide
        {
            get { return stripFromTheSide; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        /// <summary>Pictures per second, never zero — a zero would divide by nothing at playback.</summary>
        public float Speed
        {
            get { return speed <= 0f ? 10f : speed; }
        }

        /// <summary>The Core Keeper animation name this clip will carry.</summary>
        public string AnimationName
        {
            get { return DimensionCreatureAnimationNames.AnimationNameFor(kind); }
        }

        public bool Repeats
        {
            get
            {
                if (repeats == DimensionClipRepeat.KeepsGoing)
                {
                    return true;
                }

                if (repeats == DimensionClipRepeat.PlaysOnce)
                {
                    return false;
                }

                return DimensionCreatureAnimationNames.RepeatsByDefault(kind);
            }
        }

        /// <summary>
        /// How many pictures the strip holds.
        /// </summary>
        /// <remarks>
        /// An unset count is worked out the way the game itself works one out for a new clip:
        /// width divided by height, rounded up. That is right for the square-ish frames creature
        /// art uses and wrong for nothing an author is likely to draw — but it is a guess, so an
        /// author who draws wide frames sets the number instead.
        /// </remarks>
        public int FrameCount
        {
            get
            {
                if (frames > 0)
                {
                    return frames;
                }

                if (strip == null || strip.height <= 0)
                {
                    return 1;
                }

                return Mathf.Max(1, Mathf.CeilToInt((float)strip.width / strip.height));
            }
        }

        /// <summary>The extra ticks per picture, always exactly one number per picture.</summary>
        public int[] HoldFrames
        {
            get
            {
                int count = FrameCount;
                int[] result = new int[count];
                if (holdEachPictureFor == null)
                {
                    return result;
                }

                for (int i = 0; i < count && i < holdEachPictureFor.Length; i++)
                {
                    result[i] = holdEachPictureFor[i] < 0 ? 0 : holdEachPictureFor[i];
                }

                return result;
            }
        }

        /// <summary>Whether the author gave a hold list that does not match the picture count.</summary>
        public bool HoldListIsTheWrongLength
        {
            get
            {
                return holdEachPictureFor != null &&
                    holdEachPictureFor.Length > 0 &&
                    holdEachPictureFor.Length != FrameCount;
            }
        }

        public string[] MomentNames
        {
            get { return momentNames ?? new string[0]; }
        }

        public int[] MomentPictures
        {
            get { return momentPictures ?? new int[0]; }
        }

        public string[] MomentSounds
        {
            get { return momentSounds ?? new string[0]; }
        }

        /// <summary>Whether a moment names no picture, or names one the strip does not have.</summary>
        public bool HasAMomentOffTheEndOfTheStrip
        {
            get
            {
                string[] names = MomentNames;
                int[] pictures = MomentPictures;
                int count = FrameCount;
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.IsNullOrEmpty(names[i]))
                    {
                        continue;
                    }

                    int picture = i < pictures.Length ? pictures[i] : 0;
                    if (picture < 0 || picture >= count)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Whether a direction was drawn but the clip itself was not.</summary>
        public bool HasADirectionWithoutABaseStrip
        {
            get
            {
                return strip == null &&
                    (stripFromBehind != null || stripFromTheSide != null);
            }
        }
    }

    /// <summary>
    /// Everything about how a creature looks while it moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A LIST OF CLIPS AND NOT ONE PICTURE. Core Keeper drives a creature's art by sending the
    /// name of what it is doing — the same name for a caveling and for a boss. A creature with one
    /// still picture receives all of those and can answer none of them, which is why every mob this
    /// framework generated before this template existed stood frozen, and mostly invisible.
    /// </para>
    /// <para>
    /// Nothing here is required. A creature with no clips at all still generates; it simply has no
    /// body, and the generator says so.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureAnimationTemplate
    {
        [Tooltip("What the creature is seen doing, one row of pictures per thing.")]
        [SerializeField] private DimensionCreatureClipTemplate[] clips = new DimensionCreatureClipTemplate[0];

        [Tooltip("The shadow it casts.")]
        [SerializeField] private DimensionCreatureShadowSize shadow = DimensionCreatureShadowSize.Medium;

        [Tooltip("Whether it turns to face the way it is walking. Off for anything that never turns.")]
        [SerializeField] private bool turnsToFaceWhereItGoes = true;

        [Tooltip("Whether it flashes white when hit, the way the game's creatures do.")]
        [SerializeField] private bool flashesWhenHit = true;

        [Tooltip("The material its body draws with. Leave empty for the game's standard lit one.")]
        [SerializeField] private Material bodyMaterial;

        [Tooltip("Notes for whoever edits this next. Never reaches the game.")]
        [SerializeField] private string notes = string.Empty;

        public DimensionCreatureClipTemplate[] Clips
        {
            get { return clips ?? new DimensionCreatureClipTemplate[0]; }
        }

        public DimensionCreatureShadowSize Shadow
        {
            get { return shadow; }
        }

        public bool TurnsToFaceWhereItGoes
        {
            get { return turnsToFaceWhereItGoes; }
        }

        public bool FlashesWhenHit
        {
            get { return flashesWhenHit; }
        }

        public Material BodyMaterial
        {
            get { return bodyMaterial; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        /// <summary>Whether anything here would produce a body at all.</summary>
        public bool HasAnyClip
        {
            get
            {
                DimensionCreatureClipTemplate[] all = Clips;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].Enabled && all[i].Strip != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Whether the same thing is described twice, which would lose one of them.</summary>
        /// <remarks>
        /// A sprite asset keeps the FIRST clip under a name and silently drops later ones, so a
        /// second "Walking" is not an override — it is art that never plays.
        /// </remarks>
        public bool HasDuplicateClips
        {
            get
            {
                DimensionCreatureClipTemplate[] all = Clips;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || !all[i].Enabled)
                    {
                        continue;
                    }

                    for (int j = i + 1; j < all.Length; j++)
                    {
                        if (all[j] != null && all[j].Enabled && all[j].Kind == all[i].Kind)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        /// <summary>Every distinct moment name across every clip, in the order they were written.</summary>
        /// <remarks>
        /// The order IS the storage: a moment's position in this list is the bit it occupies in
        /// each frame's mask, so reordering it would move which frames fire which sound.
        /// </remarks>
        public string[] CollectMomentNames()
        {
            System.Collections.Generic.List<string> ordered =
                new System.Collections.Generic.List<string>();
            DimensionCreatureClipTemplate[] all = Clips;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].Enabled)
                {
                    continue;
                }

                string[] names = all[i].MomentNames;
                for (int n = 0; n < names.Length; n++)
                {
                    if (string.IsNullOrEmpty(names[n]) || ordered.Contains(names[n]))
                    {
                        continue;
                    }

                    ordered.Add(names[n]);
                }
            }

            return ordered.ToArray();
        }

        /// <summary>
        /// The sound each collected moment plays, one per name, in the same order.
        /// </summary>
        /// <remarks>
        /// A moment name reused across clips keeps the FIRST sound anyone gave it. The name is a
        /// single bit shared by every clip that mentions it, so it can only have one sound — and
        /// the alternative, letting the last clip in the list win, would mean reordering the clips
        /// changed what a footstep sounds like.
        /// </remarks>
        public string[] CollectMomentSounds()
        {
            string[] names = CollectMomentNames();
            string[] sounds = new string[names.Length];
            DimensionCreatureClipTemplate[] all = Clips;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].Enabled)
                {
                    continue;
                }

                string[] clipNames = all[i].MomentNames;
                string[] clipSounds = all[i].MomentSounds;
                for (int n = 0; n < clipNames.Length; n++)
                {
                    if (string.IsNullOrEmpty(clipNames[n]) || n >= clipSounds.Length ||
                        string.IsNullOrEmpty(clipSounds[n]))
                    {
                        continue;
                    }

                    int slot = System.Array.IndexOf(names, clipNames[n]);
                    if (slot >= 0 && string.IsNullOrEmpty(sounds[slot]))
                    {
                        sounds[slot] = clipSounds[n];
                    }
                }
            }

            for (int i = 0; i < sounds.Length; i++)
            {
                if (sounds[i] == null)
                {
                    sounds[i] = string.Empty;
                }
            }

            return sounds;
        }

        /// <summary>Whether more moments were named than a frame has bits to record.</summary>
        public bool HasTooManyMoments
        {
            get
            {
                return CollectMomentNames().Length > DimensionCreatureAnimationNames.MaximumEvents;
            }
        }

        /// <summary>The clip for a kind, or null when the author did not draw one.</summary>
        public DimensionCreatureClipTemplate ClipFor(DimensionCreatureClipKind kind)
        {
            DimensionCreatureClipTemplate[] all = Clips;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].Enabled && all[i].Kind == kind && all[i].Strip != null)
                {
                    return all[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Whether a clip repeats, once the rest of the list is taken into account.
        /// </summary>
        /// <remarks>
        /// Falling asleep is the one clip whose answer depends on its neighbours. With a fast
        /// asleep clip beside it, it plays once and hands over; without one, it has to repeat or
        /// the creature would finish lying down and then have nothing to be doing for the whole
        /// minute it is meant to be asleep.
        /// </remarks>
        public bool RepeatsFor(DimensionCreatureClipTemplate clip)
        {
            if (clip == null)
            {
                return false;
            }

            if (clip.Kind == DimensionCreatureClipKind.Sleeping &&
                ClipFor(DimensionCreatureClipKind.FastAsleep) == null)
            {
                return true;
            }

            return clip.Repeats;
        }

        /// <summary>
        /// What a clip hands over to when it finishes, or empty when it holds where it is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE CHAIN IS DATA, NOT CODE. No server system ever asks a creature to go back to
        /// standing after a swing; the sprite asset does it, by naming the clip to play next.
        /// Sleeping is the case that proves it: the server only ever sends "sleep" and "wakeUp",
        /// and the long stretch of lying there comes from sleep handing over to being fast asleep,
        /// exactly as the caveling's own asset does it.
        /// </para>
        /// <para>
        /// Dying deliberately hands over to nothing. A death clip that returned to standing would
        /// leave a corpse breathing for the second before the game hides it.
        /// </para>
        /// </remarks>
        public DimensionCreatureClipKind? ExitFor(DimensionCreatureClipKind kind)
        {
            if (kind == DimensionCreatureClipKind.Dying)
            {
                return null;
            }

            if (kind == DimensionCreatureClipKind.Sleeping)
            {
                return ClipFor(DimensionCreatureClipKind.FastAsleep) != null
                    ? DimensionCreatureClipKind.FastAsleep
                    : (DimensionCreatureClipKind?)null;
            }

            if (kind == DimensionCreatureClipKind.FastAsleep)
            {
                return ClipFor(DimensionCreatureClipKind.WakingUp) != null
                    ? DimensionCreatureClipKind.WakingUp
                    : (DimensionCreatureClipKind?)null;
            }

            // THE SIX GAPS A STATE SYSTEM LEAVES OPEN. Each of these is a short clip the game
            // plays at the START of a stretch it then holds the creature in for a timer's worth of
            // seconds before triggering the next name itself. The generic answer below — hand back
            // to standing — fills that stretch with the creature standing still in the middle of
            // channelling a heal, dropping its guard, or burrowing into a bush. Handing over to the
            // clip that IS the stretch is what the game's own assets do.
            //
            // Only these six. "vulnerable", "bush", "channeling" and "idleCombat" are NOT chained
            // onward, because the system triggers what comes after them on its own schedule and a
            // chain would end the look early; they repeat instead (see RepeatsByDefault). Chained
            // conditionally, exactly like sleeping above: a chain to a clip the author never drew
            // writes a guid that resolves to nothing.
            DimensionCreatureClipKind? staged = StagedExitFor(kind);
            if (staged.HasValue && ClipFor(kind) != null)
            {
                return ClipFor(staged.Value) != null ? staged : (DimensionCreatureClipKind?)null;
            }

            DimensionCreatureClipTemplate clip = ClipFor(kind);
            if (clip == null || clip.Repeats)
            {
                return null;
            }

            return ClipFor(DimensionCreatureClipKind.Standing) != null
                ? DimensionCreatureClipKind.Standing
                : (DimensionCreatureClipKind?)null;
        }

        /// <summary>
        /// The clip that carries on where a staged one leaves off, or nothing when the kind is not
        /// the opening beat of a stage.
        /// </summary>
        /// <remarks>
        /// Read off the systems that drive the stages, not chosen:
        /// <c>VulnerableStateSystem.cs:116</c> fires "prevulnerable" and only fires "vulnerable" at
        /// <c>:126</c> once the anticipation timer elapses;
        /// <c>HealOtherEntityStateSystem.cs:33-34</c> is the same shape for "startChanneling" and
        /// "channeling"; <c>HatchWhenPlayerNearbyStateSystem.cs:51-53</c> steps "isHatching",
        /// "hatch", "hasHatched" on its own timers; and <c>BushStateSystem.cs:166</c> fires
        /// "goToBush", then holds for the go-to-bush duration before firing "bush" at <c>:178</c>.
        /// Peeking is the odd one out and goes BACK: <c>:184</c> fires "peak" and <c>:185</c> sets
        /// the next stage to being in the bush again, so a peek that handed back to standing would
        /// show the creature in the open while the game has it hidden.
        /// </remarks>
        private static DimensionCreatureClipKind? StagedExitFor(DimensionCreatureClipKind kind)
        {
            switch (kind)
            {
                case DimensionCreatureClipKind.AboutToBeOpen:
                    return DimensionCreatureClipKind.OpenToAttack;
                case DimensionCreatureClipKind.StartingToChannel:
                    return DimensionCreatureClipKind.Channelling;
                case DimensionCreatureClipKind.AboutToHatch:
                    return DimensionCreatureClipKind.Hatching;
                case DimensionCreatureClipKind.Hatching:
                    return DimensionCreatureClipKind.Hatched;
                case DimensionCreatureClipKind.HeadingIntoABush:
                    return DimensionCreatureClipKind.HidingInABush;
                case DimensionCreatureClipKind.PeekingOut:
                    return DimensionCreatureClipKind.HidingInABush;
                default:
                    return null;
            }
        }
    }
}
