#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Skills;
using NUnit.Framework;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers what a mod's own creature is worth killing.
    /// </summary>
    /// <remarks>
    /// The registry is the half that can be held to account without a running game: which entries
    /// it accepts, which it refuses, and that it costs nothing when a mod never asked for it. What
    /// it does with the entries — one <c>AddSkillValueCD</c> per death, on the entity that landed
    /// the blow — needs a world, and is not claimed here.
    /// <para>
    /// These eight passed for as long as the registry has existed while the feature was dead in
    /// game: <c>DimensionSkillXpSystem</c>, the only reader of this registry, had no creation call
    /// in <c>ExpandNullforgeModEntry</c>, so it never ticked and no kill ever paid out. The server
    /// block now creates it, and <c>DimensionSystemLivenessTests</c> is what keeps it created.
    /// </para>
    /// </remarks>
    public sealed class DimensionSkillXpTests
    {
        [SetUp]
        [TearDown]
        public void Cleanup()
        {
            DimensionSkillXpRegistry.Clear();
        }

        [Test]
        public void NothingIsWorthAnythingUntilSomebodySaysSo()
        {
            Assert.That(
                DimensionSkillXpRegistry.HasAny,
                Is.False,
                "The system reads this before it does anything else, so an empty registry is what " +
                "makes the feature free for a player whose mods never asked for it.");
            Assert.That(DimensionSkillXpRegistry.Count, Is.Zero);
        }

        [Test]
        public void ACreatureCanBeMadeWorthKilling()
        {
            Assert.That(
                DimensionSkillXpRegistry.Register("MyMod:grubKing", SkillID.Melee, 25),
                Is.True);
            Assert.That(DimensionSkillXpRegistry.HasAny, Is.True);
            Assert.That(DimensionSkillXpRegistry.Count, Is.EqualTo(1));
        }

        [Test]
        public void SayingItTwiceDoesNotMakeItWorthTwice()
        {
            DimensionSkillXpRegistry.Register("MyMod:grubKing", SkillID.Melee, 25);
            DimensionSkillXpRegistry.Register("MyMod:grubKing", SkillID.Range, 40);

            Assert.That(
                DimensionSkillXpRegistry.Count,
                Is.EqualTo(1),
                "A mod reloaded in the editor registers everything again, and a creature that " +
                "doubled in value on every reload would be a bug nobody could reproduce.");
        }

        [Test]
        public void AnAmountOfNothingIsRefused()
        {
            Assert.That(
                DimensionSkillXpRegistry.Register("MyMod:grubKing", SkillID.Melee, 0),
                Is.False);
            Assert.That(
                DimensionSkillXpRegistry.Register("MyMod:grubKing", SkillID.Melee, -5),
                Is.False);
            Assert.That(DimensionSkillXpRegistry.HasAny, Is.False);
        }

        [Test]
        public void ASkillTheGameDoesNotHaveIsRefused()
        {
            Assert.That(
                DimensionSkillXpRegistry.Register(
                    "MyMod:grubKing", SkillID.NUM_SKILLS, 25),
                Is.False,
                "NUM_SKILLS is the count and not a skill. Letting it through would index one past " +
                "the end of the player's skill buffer.");
            Assert.That(
                DimensionSkillXpRegistry.Register("MyMod:grubKing", (SkillID)99, 25),
                Is.False);
            Assert.That(DimensionSkillXpRegistry.HasAny, Is.False);
        }

        [Test]
        public void ACreatureWithNoNameIsRefused()
        {
            Assert.That(
                DimensionSkillXpRegistry.Register(null, SkillID.Melee, 25), Is.False);
            Assert.That(
                DimensionSkillXpRegistry.Register(string.Empty, SkillID.Melee, 25), Is.False);
            Assert.That(DimensionSkillXpRegistry.HasAny, Is.False);
        }

        [Test]
        public void ClearingEmptiesIt()
        {
            DimensionSkillXpRegistry.Register("MyMod:grubKing", SkillID.Melee, 25);
            DimensionSkillXpRegistry.Clear();

            Assert.That(DimensionSkillXpRegistry.HasAny, Is.False);
            Assert.That(DimensionSkillXpRegistry.ResolvedCount, Is.Zero);
        }

        [Test]
        public void NothingResolvesWhileNothingWasRegistered()
        {
            Assert.That(
                DimensionSkillXpRegistry.EnsureResolved(),
                Is.False,
                "Answering true with nothing in it would let the system walk every corpse in the " +
                "world looking for entries that are not there.");
        }
    }
}
#endif
