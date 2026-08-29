using ExpandNullforge.Authoring;
using NUnit.Framework;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Guards the two things that decide whether an authored clip is ever seen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A creature's art fails silently in both directions. A clip whose name hashes to the wrong
    /// number is simply never asked for — no error, no missing-asset warning, just a creature that
    /// stands still while the server tells it to attack. And a clip whose name is right but whose
    /// texture is named wrong loses the name at load, because Core Keeper derives a clip's name
    /// from its TEXTURE's name rather than storing it.
    /// </para>
    /// <para>
    /// So these tests pin the name table against the state systems that fire it, and run the
    /// game's own name-deriving code over the names this generator writes. The rest guard the
    /// validations, each of which stands for a way an asset loads without complaint and plays
    /// wrong.
    /// </para>
    /// </remarks>
    public sealed class DimensionCreatureAnimationTests
    {
        // The numbers Core Keeper's own state systems write into the animation buffer, read off
        // the call sites rather than computed here — a table that computed its own expectations
        // would agree with itself no matter what it said.
        private const int IdleHash = -601574123;
        private const int MoveHash = -281135240;
        private const int AttackHash = 1203776827;
        private const int RangedAttackHash = -1014102059;
        private const int SleepHash = 255050412;
        private const int WakeUpHash = 910517187;
        private const int DeathHash = -414722770;
        private const int SpawnHash = -1878077465;
        private const int JumpHash = -1481439722;
        private const int ChargeHash = 1433117748;
        private const int EnrageHash = 1354651601;
        private const int EatHash = -1697431782;

        // The second batch, copied out of ck-db\Pug.Base\AnimID.cs rather than recomputed, and
        // each one checked against the system that writes it into the animation buffer: preChase
        // and endChase at ChaseStateSystem.cs:361 and :508; chargeAnticipation, chargeEnd and
        // collide at ChargeAttackStateSystem.cs:376, :533 and :544; prevulnerable, vulnerable and
        // endVulnerable at VulnerableStateSystem.cs:27-29; startExplode at ExplodeStateSystem.cs:40;
        // startTeleport and endTeleport at TeleportStateSystem.cs:40-41; attackFire at
        // ShootMortarProjectileStateSystem.cs:50; startChanneling and channeling at
        // HealOtherEntityStateSystem.cs:33-34; isHatching, hatch and hasHatched at
        // HatchWhenPlayerNearbyStateSystem.cs:51-53; idleCombat at IdleInCombatStateSystem.cs:25;
        // takeDamage at AttackSystem.cs:1159; beamAttack at BeamAttackStateSystem.cs:28; feignDeath
        // and revive at AnimateDontDestroyOnZeroHealthSystem.cs:93 and :97; and goToBush, bush,
        // peak and leaveBush at BushStateSystem.cs:166, :178, :184 and :190.
        private const int PreChaseHash = 2074276498;
        private const int EndChaseHash = 618391746;
        private const int ChargeAnticipationHash = -1634423587;
        private const int ChargeEndHash = 198769013;
        private const int CollideHash = -1997722203;
        private const int PreVulnerableHash = -1498481396;
        private const int VulnerableHash = 425101933;
        private const int EndVulnerableHash = -2008574808;
        private const int StartExplodeHash = -1473092350;
        private const int StartTeleportHash = -1518581387;
        private const int EndTeleportHash = -1065991089;
        private const int AttackFireHash = -871297121;
        private const int StartChannelingHash = 1314006782;
        private const int ChannelingHash = -2074345483;
        private const int IsHatchingHash = 267581710;
        private const int HatchHash = -1296348555;
        private const int HasHatchedHash = -849250722;
        private const int IdleCombatHash = -1442707745;
        private const int TakeDamageHash = -1533413595;
        private const int BeamAttackHash = 669154430;
        private const int FeignDeathHash = 2053665356;
        private const int ReviveHash = -350899940;
        private const int GoToBushHash = 1792219624;
        private const int BushHash = -1523938960;
        private const int PeakHash = -1562274691;
        private const int LeaveBushHash = 1096900643;

        [Test]
        public void PlayerWordsCarryTheNamesTheGameFires()
        {
            Assert.AreEqual("idle", Name(DimensionCreatureClipKind.Standing));
            Assert.AreEqual("move", Name(DimensionCreatureClipKind.Walking));
            Assert.AreEqual("attack", Name(DimensionCreatureClipKind.Attacking));
            Assert.AreEqual("rangedAttack", Name(DimensionCreatureClipKind.Shooting));
            Assert.AreEqual("sleep", Name(DimensionCreatureClipKind.Sleeping));
            Assert.AreEqual("sleeping", Name(DimensionCreatureClipKind.FastAsleep));
            Assert.AreEqual("wakeUp", Name(DimensionCreatureClipKind.WakingUp));
            Assert.AreEqual("death", Name(DimensionCreatureClipKind.Dying));
            Assert.AreEqual("spawn", Name(DimensionCreatureClipKind.Appearing));
            Assert.AreEqual("jump", Name(DimensionCreatureClipKind.Leaping));
            Assert.AreEqual("charge", Name(DimensionCreatureClipKind.Charging));
            Assert.AreEqual("enrage", Name(DimensionCreatureClipKind.GettingAngry));
            Assert.AreEqual("eat", Name(DimensionCreatureClipKind.Eating));
            Assert.AreEqual("yawn", Name(DimensionCreatureClipKind.Yawning));
            Assert.AreEqual("talking", Name(DimensionCreatureClipKind.Talking));
            Assert.AreEqual("point", Name(DimensionCreatureClipKind.Pointing));
            Assert.AreEqual("taunt", Name(DimensionCreatureClipKind.Taunting));
            Assert.AreEqual("preChase", Name(DimensionCreatureClipKind.NoticingYou));
            Assert.AreEqual("endChase", Name(DimensionCreatureClipKind.GivingUpTheChase));
            Assert.AreEqual("chargeAnticipation", Name(DimensionCreatureClipKind.WindingUpToRun));
            Assert.AreEqual("chargeEnd", Name(DimensionCreatureClipKind.SkiddingToAHalt));
            Assert.AreEqual("collide", Name(DimensionCreatureClipKind.HittingAWall));
            Assert.AreEqual("prevulnerable", Name(DimensionCreatureClipKind.AboutToBeOpen));
            Assert.AreEqual("vulnerable", Name(DimensionCreatureClipKind.OpenToAttack));
            Assert.AreEqual("endVulnerable", Name(DimensionCreatureClipKind.ClosingUpAgain));
            Assert.AreEqual("startExplode", Name(DimensionCreatureClipKind.AboutToBlowUp));
            Assert.AreEqual("startTeleport", Name(DimensionCreatureClipKind.Vanishing));
            Assert.AreEqual("endTeleport", Name(DimensionCreatureClipKind.Reappearing));
            Assert.AreEqual("attackFire", Name(DimensionCreatureClipKind.Lobbing));
            Assert.AreEqual("startChanneling", Name(DimensionCreatureClipKind.StartingToChannel));
            Assert.AreEqual("channeling", Name(DimensionCreatureClipKind.Channelling));
            Assert.AreEqual("isHatching", Name(DimensionCreatureClipKind.AboutToHatch));
            Assert.AreEqual("hatch", Name(DimensionCreatureClipKind.Hatching));
            Assert.AreEqual("hasHatched", Name(DimensionCreatureClipKind.Hatched));
            Assert.AreEqual("idleCombat", Name(DimensionCreatureClipKind.SizingYouUp));
            Assert.AreEqual("takeDamage", Name(DimensionCreatureClipKind.BeingHurt));
            Assert.AreEqual("beamAttack", Name(DimensionCreatureClipKind.FiringABeam));
            Assert.AreEqual("feignDeath", Name(DimensionCreatureClipKind.PlayingDead));
            Assert.AreEqual("revive", Name(DimensionCreatureClipKind.GettingBackUp));
            Assert.AreEqual("goToBush", Name(DimensionCreatureClipKind.HeadingIntoABush));
            Assert.AreEqual("bush", Name(DimensionCreatureClipKind.HidingInABush));
            Assert.AreEqual("peak", Name(DimensionCreatureClipKind.PeekingOut));
            Assert.AreEqual("leaveBush", Name(DimensionCreatureClipKind.LeavingTheBush));
        }

        [Test]
        public void EveryNameHashesToTheNumberTheServerSends()
        {
            Assert.AreEqual(IdleHash, Hash(DimensionCreatureClipKind.Standing));
            Assert.AreEqual(MoveHash, Hash(DimensionCreatureClipKind.Walking));
            Assert.AreEqual(AttackHash, Hash(DimensionCreatureClipKind.Attacking));
            Assert.AreEqual(RangedAttackHash, Hash(DimensionCreatureClipKind.Shooting));
            Assert.AreEqual(SleepHash, Hash(DimensionCreatureClipKind.Sleeping));
            Assert.AreEqual(WakeUpHash, Hash(DimensionCreatureClipKind.WakingUp));
            Assert.AreEqual(DeathHash, Hash(DimensionCreatureClipKind.Dying));
            Assert.AreEqual(SpawnHash, Hash(DimensionCreatureClipKind.Appearing));
            Assert.AreEqual(JumpHash, Hash(DimensionCreatureClipKind.Leaping));
            Assert.AreEqual(ChargeHash, Hash(DimensionCreatureClipKind.Charging));
            Assert.AreEqual(EnrageHash, Hash(DimensionCreatureClipKind.GettingAngry));
            Assert.AreEqual(EatHash, Hash(DimensionCreatureClipKind.Eating));
            Assert.AreEqual(PreChaseHash, Hash(DimensionCreatureClipKind.NoticingYou));
            Assert.AreEqual(EndChaseHash, Hash(DimensionCreatureClipKind.GivingUpTheChase));
            Assert.AreEqual(ChargeAnticipationHash, Hash(DimensionCreatureClipKind.WindingUpToRun));
            Assert.AreEqual(ChargeEndHash, Hash(DimensionCreatureClipKind.SkiddingToAHalt));
            Assert.AreEqual(CollideHash, Hash(DimensionCreatureClipKind.HittingAWall));
            Assert.AreEqual(PreVulnerableHash, Hash(DimensionCreatureClipKind.AboutToBeOpen));
            Assert.AreEqual(VulnerableHash, Hash(DimensionCreatureClipKind.OpenToAttack));
            Assert.AreEqual(EndVulnerableHash, Hash(DimensionCreatureClipKind.ClosingUpAgain));
            Assert.AreEqual(StartExplodeHash, Hash(DimensionCreatureClipKind.AboutToBlowUp));
            Assert.AreEqual(StartTeleportHash, Hash(DimensionCreatureClipKind.Vanishing));
            Assert.AreEqual(EndTeleportHash, Hash(DimensionCreatureClipKind.Reappearing));
            Assert.AreEqual(AttackFireHash, Hash(DimensionCreatureClipKind.Lobbing));
            Assert.AreEqual(StartChannelingHash, Hash(DimensionCreatureClipKind.StartingToChannel));
            Assert.AreEqual(ChannelingHash, Hash(DimensionCreatureClipKind.Channelling));
            Assert.AreEqual(IsHatchingHash, Hash(DimensionCreatureClipKind.AboutToHatch));
            Assert.AreEqual(HatchHash, Hash(DimensionCreatureClipKind.Hatching));
            Assert.AreEqual(HasHatchedHash, Hash(DimensionCreatureClipKind.Hatched));
            Assert.AreEqual(IdleCombatHash, Hash(DimensionCreatureClipKind.SizingYouUp));
            Assert.AreEqual(TakeDamageHash, Hash(DimensionCreatureClipKind.BeingHurt));
            Assert.AreEqual(BeamAttackHash, Hash(DimensionCreatureClipKind.FiringABeam));
            Assert.AreEqual(FeignDeathHash, Hash(DimensionCreatureClipKind.PlayingDead));
            Assert.AreEqual(ReviveHash, Hash(DimensionCreatureClipKind.GettingBackUp));
            Assert.AreEqual(GoToBushHash, Hash(DimensionCreatureClipKind.HeadingIntoABush));
            Assert.AreEqual(BushHash, Hash(DimensionCreatureClipKind.HidingInABush));
            Assert.AreEqual(PeakHash, Hash(DimensionCreatureClipKind.PeekingOut));
            Assert.AreEqual(LeaveBushHash, Hash(DimensionCreatureClipKind.LeavingTheBush));
        }

        /// <summary>
        /// The view listens for "preChase" to play a creature's aggro sound. Until the clip kinds
        /// grew, that name was not in the enum at all, so the trigger the view waits for was one an
        /// author had no way to draw art for.
        /// </summary>
        [Test]
        public void TheNameTheViewListensForCanBeDrawnFor()
        {
            Assert.AreEqual(
                Animator.StringToHash("preChase"),
                Hash(DimensionCreatureClipKind.NoticingYou));
        }

        [Test]
        public void EveryKindIsOfferedAndNoneIsOfferedTwice()
        {
            DimensionCreatureClipKind[] offered = DimensionCreatureAnimationNames.AllKinds;
            System.Array all = System.Enum.GetValues(typeof(DimensionCreatureClipKind));
            Assert.AreEqual(all.Length, offered.Length);
            for (int i = 0; i < offered.Length; i++)
            {
                for (int j = i + 1; j < offered.Length; j++)
                {
                    Assert.AreNotEqual(offered[i], offered[j]);
                }
            }
        }

        /// <summary>
        /// The generator names a clip's texture "{asset}_{clip}". This runs the game's own name
        /// derivation over that and checks the clip name survives — the step nothing else guards.
        /// </summary>
        [Test]
        public void TheGameReadsTheClipNameBackOutOfTheTextureName()
        {
            const string assetName = "EmberBat";
            for (int i = 0; i < DimensionCreatureAnimationNames.AllKinds.Length; i++)
            {
                string animationName = Name(DimensionCreatureAnimationNames.AllKinds[i]);
                string textureName = assetName + "_" + animationName;
                Assert.AreEqual(
                    animationName,
                    SpriteAsset.PrettifyName(textureName, assetName),
                    "Texture name " + textureName + " does not read back as " + animationName);
            }
        }

        /// <summary>
        /// The direction variants are named one level down, off the clip's own texture name, and
        /// the camera-facing strip is deliberately unnamed.
        /// </summary>
        [Test]
        public void TheGameReadsUpAndSideBackOutOfTheVariantTextureNames()
        {
            const string stem = "EmberBat_move";
            Assert.AreEqual("up", SpriteAsset.PrettifyName(stem + "_up", stem));
            Assert.AreEqual("side", SpriteAsset.PrettifyName(stem + "_side", stem));
            Assert.AreEqual(
                1133833840,
                Animator.StringToHash(SpriteAsset.PrettifyName(stem + "_up", stem)));
            Assert.AreEqual(
                595663797,
                Animator.StringToHash(SpriteAsset.PrettifyName(stem + "_side", stem)));
        }

        [Test]
        public void ShadowSizesNameTheGamesOwnShadowSprites()
        {
            Assert.AreEqual(
                string.Empty,
                DimensionCreatureAnimationNames.ShadowVariantNameFor(
                    DimensionCreatureShadowSize.Medium),
                "Medium is the shared shadow's plain sprite, which is asked for by no name at all.");
            Assert.AreEqual(
                0,
                DimensionCreatureAnimationNames.ShadowVariantHashFor(
                    DimensionCreatureShadowSize.Medium));
            Assert.AreEqual(
                Animator.StringToHash("shadow_6x4"),
                DimensionCreatureAnimationNames.ShadowVariantHashFor(
                    DimensionCreatureShadowSize.Small));
            Assert.AreEqual(
                Animator.StringToHash("shadow_blur_24x22"),
                DimensionCreatureAnimationNames.ShadowVariantHashFor(
                    DimensionCreatureShadowSize.BigAndSoft));
        }

        // ---- Validation rules ------------------------------------------------------------------

        [Test]
        public void AHoldListOfTheWrongLengthIsCaught()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            Clip(clips, 0, DimensionCreatureClipKind.Walking, 4, new[] { 1, 1 });
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(template.Clips[0].HoldListIsTheWrongLength);
        }

        [Test]
        public void AHoldListOfTheRightLengthIsAccepted()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            Clip(clips, 0, DimensionCreatureClipKind.Walking, 4, new[] { 0, 1, 0, 2 });
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(template.Clips[0].HoldListIsTheWrongLength);
            Assert.AreEqual(new[] { 0, 1, 0, 2 }, template.Clips[0].HoldFrames);
        }

        [Test]
        public void AnEmptyHoldListStillProducesOneNumberPerPicture()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            Clip(clips, 0, DimensionCreatureClipKind.Standing, 5, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(template.Clips[0].HoldListIsTheWrongLength);
            Assert.AreEqual(5, template.Clips[0].HoldFrames.Length);
        }

        [Test]
        public void AMomentPastTheEndOfTheStripIsCaught()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            SerializedProperty clip = Clip(
                clips, 0, DimensionCreatureClipKind.Attacking, 3, new int[0]);
            SetStrings(clip, "momentNames", new[] { "swing" });
            SetInts(clip, "momentPictures", new[] { 7 });
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(template.Clips[0].HasAMomentOffTheEndOfTheStrip);
        }

        [Test]
        public void AMomentOnARealPictureIsAccepted()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            SerializedProperty clip = Clip(
                clips, 0, DimensionCreatureClipKind.Attacking, 3, new int[0]);
            SetStrings(clip, "momentNames", new[] { "swing" });
            SetInts(clip, "momentPictures", new[] { 2 });
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(template.Clips[0].HasAMomentOffTheEndOfTheStrip);
        }

        [Test]
        public void MoreThanThirtyTwoMomentsIsCaught()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            SerializedProperty clip = Clip(
                clips, 0, DimensionCreatureClipKind.Attacking, 40, new int[0]);
            string[] names = new string[33];
            int[] pictures = new int[33];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = "moment" + i;
                pictures[i] = i;
            }

            SetStrings(clip, "momentNames", names);
            SetInts(clip, "momentPictures", pictures);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(template.HasTooManyMoments);
            Assert.AreEqual(33, template.CollectMomentNames().Length);
        }

        [Test]
        public void ThirtyTwoMomentsIsAccepted()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            SerializedProperty clip = Clip(
                clips, 0, DimensionCreatureClipKind.Attacking, 40, new int[0]);
            string[] names = new string[32];
            int[] pictures = new int[32];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = "moment" + i;
                pictures[i] = i;
            }

            SetStrings(clip, "momentNames", names);
            SetInts(clip, "momentPictures", pictures);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(template.HasTooManyMoments);
        }

        [Test]
        public void TheSameThingDescribedTwiceIsCaught()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 2;
            Clip(clips, 0, DimensionCreatureClipKind.Walking, 4, new int[0]);
            Clip(clips, 1, DimensionCreatureClipKind.Walking, 6, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(template.HasDuplicateClips);
        }

        [Test]
        public void TwoDifferentThingsAreNotADuplicate()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 2;
            Clip(clips, 0, DimensionCreatureClipKind.Walking, 4, new int[0]);
            Clip(clips, 1, DimensionCreatureClipKind.Standing, 4, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(template.HasDuplicateClips);
        }

        [Test]
        public void ADirectionDrawnWithoutTheCameraFacingStripIsCaught()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            SerializedProperty clip = Clip(
                clips, 0, DimensionCreatureClipKind.Walking, 4, new int[0]);
            clip.FindPropertyRelative("strip").objectReferenceValue = null;
            clip.FindPropertyRelative("stripFromTheSide").objectReferenceValue = Strip();
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(template.Clips[0].HasADirectionWithoutABaseStrip);
        }

        // ---- Moments and their sounds ----------------------------------------------------------

        [Test]
        public void AMomentWithNoSoundIsReportedAsSilent()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            SerializedProperty clip = Clip(
                clips, 0, DimensionCreatureClipKind.Walking, 4, new int[0]);
            SetStrings(clip, "momentNames", new[] { "footstep" });
            SetInts(clip, "momentPictures", new[] { 1 });
            SetStrings(clip, "momentSounds", new[] { string.Empty });
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(new[] { "footstep" }, template.CollectMomentNames());
            Assert.AreEqual(new[] { string.Empty }, template.CollectMomentSounds());
        }

        [Test]
        public void AMomentKeepsTheSoundItWasFirstGiven()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 2;
            SerializedProperty walking = Clip(
                clips, 0, DimensionCreatureClipKind.Walking, 4, new int[0]);
            SetStrings(walking, "momentNames", new[] { "footstep" });
            SetInts(walking, "momentPictures", new[] { 1 });
            SetStrings(walking, "momentSounds", new[] { "enemyFootstep" });

            SerializedProperty charging = Clip(
                clips, 1, DimensionCreatureClipKind.Charging, 4, new int[0]);
            SetStrings(charging, "momentNames", new[] { "footstep" });
            SetInts(charging, "momentPictures", new[] { 0 });
            SetStrings(charging, "momentSounds", new[] { "petFootstep" });
            holder.ApplyModifiedPropertiesWithoutUndo();

            // One name is one bit shared by every clip, so it can only carry one sound. Letting
            // the later clip win would mean reordering the list changed how a footstep sounded.
            Assert.AreEqual(new[] { "footstep" }, template.CollectMomentNames());
            Assert.AreEqual(new[] { "enemyFootstep" }, template.CollectMomentSounds());
        }

        [Test]
        public void EveryMomentGetsASoundSlotEvenWhenNoneWasGiven()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 1;
            SerializedProperty clip = Clip(
                clips, 0, DimensionCreatureClipKind.Attacking, 6, new int[0]);
            SetStrings(clip, "momentNames", new[] { "windUp", "hit" });
            SetInts(clip, "momentPictures", new[] { 0, 3 });
            SetStrings(clip, "momentSounds", new[] { "cavelingAnticipation" });
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(2, template.CollectMomentNames().Length);
            Assert.AreEqual(2, template.CollectMomentSounds().Length);
            Assert.AreEqual("cavelingAnticipation", template.CollectMomentSounds()[0]);
            Assert.AreEqual(string.Empty, template.CollectMomentSounds()[1]);
        }

        // ---- The exit chain --------------------------------------------------------------------

        /// <summary>
        /// Sleeping is the case that proves the chain is data. The server only ever sends "sleep"
        /// and "wakeUp"; everything between them comes from clips naming what plays next.
        /// </summary>
        [Test]
        public void SleepChainsThroughBeingAsleepExactlyAsTheCavelingDoes()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 4;
            Clip(clips, 0, DimensionCreatureClipKind.Standing, 5, new int[0]);
            Clip(clips, 1, DimensionCreatureClipKind.Sleeping, 5, new int[0]);
            Clip(clips, 2, DimensionCreatureClipKind.FastAsleep, 4, new int[0]);
            Clip(clips, 3, DimensionCreatureClipKind.WakingUp, 10, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(
                DimensionCreatureClipKind.FastAsleep,
                template.ExitFor(DimensionCreatureClipKind.Sleeping));
            Assert.AreEqual(
                DimensionCreatureClipKind.WakingUp,
                template.ExitFor(DimensionCreatureClipKind.FastAsleep));
            Assert.AreEqual(
                DimensionCreatureClipKind.Standing,
                template.ExitFor(DimensionCreatureClipKind.WakingUp));
            Assert.IsTrue(template.RepeatsFor(template.ClipFor(DimensionCreatureClipKind.FastAsleep)));
            Assert.IsFalse(template.RepeatsFor(template.ClipFor(DimensionCreatureClipKind.Sleeping)));
        }

        [Test]
        public void SleepRepeatsWhenThereIsNoAsleepClipToHandOverTo()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 2;
            Clip(clips, 0, DimensionCreatureClipKind.Standing, 5, new int[0]);
            Clip(clips, 1, DimensionCreatureClipKind.Sleeping, 5, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsNull(template.ExitFor(DimensionCreatureClipKind.Sleeping));
            Assert.IsTrue(template.RepeatsFor(template.ClipFor(DimensionCreatureClipKind.Sleeping)));
        }

        [Test]
        public void AOneShotClipHandsBackToStanding()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 2;
            Clip(clips, 0, DimensionCreatureClipKind.Standing, 5, new int[0]);
            Clip(clips, 1, DimensionCreatureClipKind.Attacking, 6, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(
                DimensionCreatureClipKind.Standing,
                template.ExitFor(DimensionCreatureClipKind.Attacking));
        }

        [Test]
        public void DyingHandsBackToNothing()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 2;
            Clip(clips, 0, DimensionCreatureClipKind.Standing, 5, new int[0]);
            Clip(clips, 1, DimensionCreatureClipKind.Dying, 6, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsNull(template.ExitFor(DimensionCreatureClipKind.Dying));
        }

        [Test]
        public void ARepeatingClipHandsBackToNothing()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 2;
            Clip(clips, 0, DimensionCreatureClipKind.Standing, 5, new int[0]);
            Clip(clips, 1, DimensionCreatureClipKind.Walking, 6, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsNull(template.ExitFor(DimensionCreatureClipKind.Walking));
            Assert.IsNull(template.ExitFor(DimensionCreatureClipKind.Standing));
        }

        /// <summary>
        /// The four openers hand over to the stretch that follows them, because the state system
        /// holds the creature there before it triggers anything else.
        /// </summary>
        [Test]
        public void AnOpeningClipHandsOverToTheStretchItOpens()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 8;
            Clip(clips, 0, DimensionCreatureClipKind.Standing, 5, new int[0]);
            Clip(clips, 1, DimensionCreatureClipKind.AboutToBeOpen, 4, new int[0]);
            Clip(clips, 2, DimensionCreatureClipKind.OpenToAttack, 4, new int[0]);
            Clip(clips, 3, DimensionCreatureClipKind.StartingToChannel, 4, new int[0]);
            Clip(clips, 4, DimensionCreatureClipKind.Channelling, 4, new int[0]);
            Clip(clips, 5, DimensionCreatureClipKind.HeadingIntoABush, 4, new int[0]);
            Clip(clips, 6, DimensionCreatureClipKind.HidingInABush, 4, new int[0]);
            Clip(clips, 7, DimensionCreatureClipKind.PeekingOut, 4, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(
                DimensionCreatureClipKind.OpenToAttack,
                template.ExitFor(DimensionCreatureClipKind.AboutToBeOpen));
            Assert.AreEqual(
                DimensionCreatureClipKind.Channelling,
                template.ExitFor(DimensionCreatureClipKind.StartingToChannel));
            Assert.AreEqual(
                DimensionCreatureClipKind.HidingInABush,
                template.ExitFor(DimensionCreatureClipKind.HeadingIntoABush));

            // Peeking goes BACK into the bush rather than on to standing: BushStateSystem sets the
            // next stage to being in the bush again after a peek.
            Assert.AreEqual(
                DimensionCreatureClipKind.HidingInABush,
                template.ExitFor(DimensionCreatureClipKind.PeekingOut));

            // The three holds stay put and are not chained onward — the system decides when they
            // end and a chain would cut them short.
            Assert.IsNull(template.ExitFor(DimensionCreatureClipKind.OpenToAttack));
            Assert.IsNull(template.ExitFor(DimensionCreatureClipKind.Channelling));
            Assert.IsNull(template.ExitFor(DimensionCreatureClipKind.HidingInABush));
            Assert.IsTrue(
                template.RepeatsFor(template.ClipFor(DimensionCreatureClipKind.OpenToAttack)));
            Assert.IsTrue(
                template.RepeatsFor(template.ClipFor(DimensionCreatureClipKind.Channelling)));
            Assert.IsTrue(
                template.RepeatsFor(template.ClipFor(DimensionCreatureClipKind.HidingInABush)));
        }

        /// <summary>
        /// An opener whose follow-on was never drawn falls back to nothing rather than writing a
        /// guid that resolves to no clip — the same rule sleeping already follows.
        /// </summary>
        [Test]
        public void AnOpeningClipWithNothingToHandOverToChainsToNothing()
        {
            DimensionCreatureAnimationTemplate template = Template(
                out SerializedProperty clips,
                out SerializedObject holder);
            clips.arraySize = 2;
            Clip(clips, 0, DimensionCreatureClipKind.Standing, 5, new int[0]);
            Clip(clips, 1, DimensionCreatureClipKind.AboutToBeOpen, 4, new int[0]);
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsNull(template.ExitFor(DimensionCreatureClipKind.AboutToBeOpen));
        }

        // ---- Scaffolding -----------------------------------------------------------------------

        private static string Name(DimensionCreatureClipKind kind)
        {
            return DimensionCreatureAnimationNames.AnimationNameFor(kind);
        }

        private static int Hash(DimensionCreatureClipKind kind)
        {
            return DimensionCreatureAnimationNames.HashFor(kind);
        }

        /// <summary>
        /// A live animation template, reached through a host asset.
        /// </summary>
        /// <remarks>
        /// The template is a plain serializable class with private fields, so a host object and a
        /// SerializedObject is how a test fills one — the same way the dashboard fills it.
        /// </remarks>
        private static DimensionCreatureAnimationTemplate Template(
            out SerializedProperty clips,
            out SerializedObject holder)
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            holder = new SerializedObject(host);
            SerializedProperty animation = holder.FindProperty("visual")
                .FindPropertyRelative("animation");
            clips = animation.FindPropertyRelative("clips");
            return host.Visual.Animation;
        }

        private static SerializedProperty Clip(
            SerializedProperty clips,
            int index,
            DimensionCreatureClipKind kind,
            int frames,
            int[] holds)
        {
            SerializedProperty clip = clips.GetArrayElementAtIndex(index);
            clip.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            clip.FindPropertyRelative("frames").intValue = frames;
            clip.FindPropertyRelative("speed").floatValue = 10f;
            clip.FindPropertyRelative("enabled").boolValue = true;
            clip.FindPropertyRelative("repeats").enumValueIndex =
                (int)DimensionClipRepeat.SameAsTheGame;
            clip.FindPropertyRelative("strip").objectReferenceValue = Strip();
            SetInts(clip, "holdEachPictureFor", holds);
            SetStrings(clip, "momentNames", new string[0]);
            SetInts(clip, "momentPictures", new int[0]);
            SetStrings(clip, "momentSounds", new string[0]);
            return clip;
        }

        private static void SetInts(SerializedProperty parent, string name, int[] values)
        {
            SerializedProperty array = parent.FindPropertyRelative(name);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).intValue = values[i];
            }
        }

        private static void SetStrings(SerializedProperty parent, string name, string[] values)
        {
            SerializedProperty array = parent.FindPropertyRelative(name);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        /// <summary>A stand-in strip, so a clip counts as drawn without any art on disk.</summary>
        private static Texture2D strip;

        private static Texture2D Strip()
        {
            if (strip == null)
            {
                strip = new Texture2D(64, 16);
                strip.name = "TestStrip";
            }

            return strip;
        }
    }
}
