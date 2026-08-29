using System.Globalization;
using System.Text;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits the half of a creature's look that only exists once the game is running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THE GENERATOR CANNOT JUST BAKE THIS INTO THE PREFAB. Core Keeper hands a pooled view
    /// instance from one creature to the next, so a view has to re-learn which sprite asset to
    /// draw, how big its shadow is and what it sounds like every time it is given an entity. It
    /// looks all of that up by object id — and object ids do not exist until the mod is loaded.
    /// The bootstrap is the only place the two can be introduced.
    /// </para>
    /// <para>
    /// The sprite asset is registered as its ADDRESS, derived from the creature's object name by
    /// the same arithmetic the generator used when it wrote the asset. Neither step has to find
    /// the other's output; they agree because they compute the same number from the same name.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>Writes one registration per creature that has a body worth drawing.</summary>
        internal static void AppendCreaturePresentationRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            DimensionMobAsset[] mobs = template.GlobalMobs;
            for (int i = 0; mobs != null && i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null || !mob.Enabled || string.IsNullOrEmpty(mob.MobId))
                {
                    continue;
                }

                AppendOne(builder, modName, mob.MobId, mob.Visual, mob.Audio);

                // An elite is a second object with the same art and the same voice, so it needs a
                // registration of its own — the runtime looks these up by object id, and the
                // elite's id is not the mob's.
                DimensionEliteVariantTemplate elite = mob.EliteVariant;
                if (elite != null && elite.Enabled)
                {
                    AppendOne(
                        builder,
                        modName,
                        DimensionEliteVariantTemplate.IdFor(mob.MobId),
                        mob.Visual,
                        mob.Audio);
                }
            }

            DimensionBossAsset[] bosses = template.GlobalBosses;
            for (int i = 0; bosses != null && i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || !boss.Enabled || string.IsNullOrEmpty(boss.BossId))
                {
                    continue;
                }

                AppendOne(builder, modName, boss.BossId, boss.Visual, boss.Audio);
            }

            DimensionAnimalAsset[] animals = template.GlobalAnimals;
            for (int i = 0; animals != null && i < animals.Length; i++)
            {
                DimensionAnimalAsset animal = animals[i];
                if (animal == null || !animal.Enabled || string.IsNullOrEmpty(animal.AnimalId))
                {
                    continue;
                }

                AppendOne(builder, modName, animal.AnimalId, animal.Visual, animal.Audio);
            }

            DimensionCritterAsset[] critters = template.GlobalCritters;
            for (int i = 0; critters != null && i < critters.Length; i++)
            {
                DimensionCritterAsset critter = critters[i];
                if (critter == null || !critter.Enabled || string.IsNullOrEmpty(critter.CritterId))
                {
                    continue;
                }

                AppendOne(builder, modName, critter.CritterId, critter.Visual, critter.Audio);
            }
        }

        /// <summary>
        /// One creature's row, skipped entirely when there is nothing to say about it.
        /// </summary>
        /// <remarks>
        /// A creature with no clips and no sounds would register a row that changes nothing, and
        /// the view treats a missing row as "leave the instance alone" — which is exactly the right
        /// answer for a creature with nothing of its own. Emitting an empty row instead would blank
        /// a pooled instance and turn a visible mistake into an invisible creature.
        /// </remarks>
        private static void AppendOne(
            StringBuilder builder,
            string modName,
            string creatureId,
            DimensionSpawnableVisualTemplate visual,
            DimensionSpawnableAudioTemplate audio)
        {
            DimensionCreatureAnimationTemplate animation =
                visual == null ? null : visual.Animation;
            bool hasBody = animation != null && animation.HasAnyClip;
            int spawnSound = SoundNumber(audio == null ? null : audio.SpawnSoundId);
            int idleSound = SoundNumber(audio == null ? null : audio.IdleSoundId);
            int aggroSound = SoundNumber(audio == null ? null : audio.AggroSoundId);
            int hitSound = SoundNumber(audio == null ? null : audio.HitSoundId);
            int deathSound = SoundNumber(audio == null ? null : audio.DeathSoundId);
            bool hasVoice = spawnSound != 0 || idleSound != 0 || aggroSound != 0 ||
                hitSound != 0 || deathSound != 0;
            if (!hasBody && !hasVoice)
            {
                return;
            }

            string objectName = DimensionObjectNamespace.Qualify(modName, creatureId);
            long addressLow = 0L;
            long addressHigh = 0L;
            if (hasBody)
            {
                DimensionCreatureSpriteAssetUtility.AddressFor(
                    objectName, out addressLow, out addressHigh);
            }

            DimensionCreatureShadowSize shadow = animation == null
                ? DimensionCreatureShadowSize.None
                : animation.Shadow;

            builder.AppendLine("    DimensionCreaturePresentationRegistry.Register(");
            builder.AppendLine("        new DimensionCreaturePresentationDefinition(");
            builder.Append("            ").Append(ToCSharpString(objectName)).AppendLine(",");
            builder.Append("            ").Append(Literal(addressLow)).AppendLine(",");
            builder.Append("            ").Append(Literal(addressHigh)).AppendLine(",");
            builder.Append("            ")
                .Append(animation != null && animation.TurnsToFaceWhereItGoes ? "true" : "false")
                .AppendLine(",");
            builder.Append("            ")
                .Append(DimensionCreatureAnimationNames.ShadowVariantHashFor(shadow)
                    .ToString(CultureInfo.InvariantCulture))
                .AppendLine(",");
            builder.Append("            ")
                .Append(shadow == DimensionCreatureShadowSize.None ? "false" : "true")
                .AppendLine(",");
            builder.Append("            ").Append(Number(spawnSound)).AppendLine(",");
            builder.Append("            ").Append(Number(idleSound)).AppendLine(",");
            builder.Append("            ").Append(Number(aggroSound)).AppendLine(",");
            builder.Append("            ").Append(Number(hitSound)).AppendLine(",");
            builder.Append("            ").Append(Number(deathSound)).AppendLine(",");
            AppendMoments(builder, animation);
        }

        /// <summary>
        /// Writes the named moments inside a creature's clips and what each one sounds like.
        /// </summary>
        /// <remarks>
        /// Two parallel arrays rather than one array of pairs, because a serialized list of a
        /// custom class does not survive into the game. This one is generated source rather than
        /// serialized data, but the shape stays the same so nobody has to remember which of the
        /// two rules applies where.
        /// </remarks>
        private static void AppendMoments(
            StringBuilder builder,
            DimensionCreatureAnimationTemplate animation)
        {
            string[] names = animation == null ? new string[0] : animation.CollectMomentNames();
            string[] sounds = animation == null ? new string[0] : animation.CollectMomentSounds();
            if (names.Length == 0)
            {
                builder.AppendLine("            null,");
                builder.AppendLine("            null));");
                return;
            }

            int count = System.Math.Min(
                names.Length,
                DimensionCreatureAnimationNames.MaximumEvents);
            builder.Append("            new int[] { ");
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Number(MomentNumber(names[i])));
            }

            builder.AppendLine(" },");

            builder.Append("            new int[] { ");
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Number(
                    i < sounds.Length ? DimensionSoundNames.Hash(sounds[i]) : 0));
            }

            builder.AppendLine(" }));");
        }

        /// <summary>The number the game stores a sound under, which is a hash of its name.</summary>
        private static int SoundNumber(string soundName)
        {
            return string.IsNullOrEmpty(soundName) ? 0 : DimensionSoundNames.Hash(soundName);
        }

        /// <summary>
        /// The number a sprite object hands its listeners when a named moment fires.
        /// </summary>
        /// <remarks>
        /// The same arithmetic as a sound name by coincidence rather than by design — Core Keeper
        /// hashes very nearly every name it stores with <c>Animator.StringToHash</c>. Kept as its
        /// own step so a change to how sounds are numbered cannot quietly change how moments are.
        /// </remarks>
        private static int MomentNumber(string momentName)
        {
            return string.IsNullOrEmpty(momentName)
                ? 0
                : UnityEngine.Animator.StringToHash(momentName);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A long written so the C# compiler reads it back as the same number.
        /// </summary>
        /// <remarks>
        /// long.MinValue has no positive counterpart, so writing it as a bare literal is a
        /// negation of a number that does not fit and will not compile. It is vanishingly unlikely
        /// out of a hash, and it costs one line to make impossible rather than rare.
        /// </remarks>
        private static string Literal(long value)
        {
            return value == long.MinValue
                ? "long.MinValue"
                : value.ToString(CultureInfo.InvariantCulture) + "L";
        }
    }
}
