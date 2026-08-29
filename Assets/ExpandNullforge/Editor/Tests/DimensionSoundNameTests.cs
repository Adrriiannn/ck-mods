#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using NUnit.Framework;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the quietest failure in the framework: a sound name nothing plays.
    /// </summary>
    /// <remarks>
    /// Every string hashes, so a typo produces a perfectly valid number that no sound answers to.
    /// Nothing throws and nothing is logged; the creature simply swings in silence. The only moment
    /// it can be caught is generate time, and only by comparing the name against the list of names
    /// the game actually ships.
    /// </remarks>
    public sealed class DimensionSoundNameTests
    {
        /// <summary>
        /// Every shipped name turns into a number, and that number names a sound again.
        /// </summary>
        /// <remarks>
        /// Deliberately not "and back to the same string". The hash is Unity's, not ours, and two
        /// of the 1,413 names colliding is Unity's own business — the reverse map documents that
        /// the later name wins, and the game's own dictionary would have done the same. What must
        /// hold is that no name goes into the table and comes out as nothing, which is the case
        /// that would put a blank where the editor shows a sound.
        /// </remarks>
        [Test]
        public void EveryShippedNameTurnsIntoANumberThatNamesASound()
        {
            string[] all = DimensionSoundNames.All;
            Assert.That(all.Length, Is.GreaterThan(0));

            for (int i = 0; i < all.Length; i++)
            {
                int hash = DimensionSoundNames.Hash(all[i]);
                string back = DimensionSoundNames.NameForHash(hash);

                Assert.That(back, Is.Not.Null, "'" + all[i] + "' hashes to a number nothing names.");
                Assert.That(
                    DimensionSoundNames.Hash(back),
                    Is.EqualTo(hash),
                    "'" + all[i] + "' and '" + back + "' are held under one number but do not " +
                    "hash to the same one, which means the reverse map was built from something " +
                    "other than the list.");
            }
        }

        [Test]
        public void ANameNobodyShippedIsNotMistakenForOneThatWas()
        {
            Assert.That(DimensionSoundNames.IsAGameSound("hydraBossBiteAnticipatoin"), Is.False);
            Assert.That(DimensionSoundNames.IsAGameSound(string.Empty), Is.False);
            Assert.That(DimensionSoundNames.IsAGameSound(null), Is.False);
        }

        [Test]
        public void ANameTheGameShipsSaysNothing()
        {
            List<string> said = new List<string>();
            DimensionSoundNames.WarnIfUnknown(
                DimensionSoundNames.All[0], "'A creature'", said.Add);

            Assert.That(said, Is.Empty);
        }

        [Test]
        public void NothingAtAllSaysNothing()
        {
            List<string> said = new List<string>();
            DimensionSoundNames.WarnIfUnknown(null, "'A creature'", said.Add);
            DimensionSoundNames.WarnIfUnknown(string.Empty, "'A creature'", said.Add);

            Assert.That(
                said,
                Is.Empty,
                "Leaving a sound out is a normal thing to do and must not read as a mistake.");
        }

        [Test]
        public void ANameTheGameDoesNotShipIsSaidOutLoud()
        {
            List<string> said = new List<string>();
            DimensionSoundNames.WarnIfUnknown(
                "hydraBossBiteAnticipatoin", "'The Hydra'", said.Add);

            Assert.That(said.Count, Is.EqualTo(1));
            Assert.That(said[0], Does.Contain("The Hydra"));
            Assert.That(said[0], Does.Contain("hydraBossBiteAnticipatoin"));
            Assert.That(
                said[0],
                Does.Contain("silence"),
                "The sentence has to say what the author will actually observe, which is nothing " +
                "playing.");
        }

        [Test]
        public void AFileAddressInASoundNameBoxIsSaidToBeTheWrongSortOfThing()
        {
            List<string> said = new List<string>();
            DimensionSoundNames.WarnIfUnknown(
                "assets/audio/storm.ogg", "'A biome'", said.Add);

            Assert.That(said.Count, Is.EqualTo(1));
            Assert.That(
                said[0],
                Does.Contain("audio file"),
                "Pasting an ambience address into a sound-name box is the mistake that has cost " +
                "the most time here, and 'not one Core Keeper ships' would send the author " +
                "hunting for a spelling mistake that is not there.");

            said.Clear();
            DimensionSoundNames.WarnIfUnknown("SomeSound.ogg", "'A vehicle'", said.Add);
            Assert.That(said.Count, Is.EqualTo(1));
            Assert.That(said[0], Does.Contain("audio file"));
        }

        [Test]
        public void NothingIsSaidWhenThereIsNowhereToSayIt()
        {
            // A null sink is how a caller says it has no report to write into. It must not throw.
            Assert.DoesNotThrow(
                () => DimensionSoundNames.WarnIfUnknown("notASound", "'A creature'", null));
        }
    }
}
#endif
