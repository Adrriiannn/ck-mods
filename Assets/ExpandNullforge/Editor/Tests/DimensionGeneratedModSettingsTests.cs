using NUnit.Framework;

namespace ExpandNullforge.EditorTools.Tests
{
    /// <summary>
    /// The two switches on a creator's mod that decide whether it runs at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both live on the mod's own settings asset, both are off on a new mod, and neither shows a
    /// problem while the creator is building: the project compiles here either way. The first only
    /// bites when the game compiles the mod on load; the second only when somebody tries to join.
    /// </para>
    /// <para>
    /// This reads the generator's source rather than generating a mod, because writing them needs a
    /// real settings asset and the AssetDatabase. That is a weaker test than running the thing, and
    /// it is deliberately the one guard that matters most: it fails when somebody removes the call,
    /// which is the way this would quietly come back.
    /// </para>
    /// </remarks>
    internal sealed class DimensionGeneratedModSettingsTests
    {
        private const string Generator = "DimensionRuntimeConsumerBootstrapUtility.cs";

        [Test]
        public void TheGeneratorSetsBothSwitchesOnTheCreatorsMod()
        {
            string source = DimensionFrameworkSourceScanner.ReadByName(Generator);

            Assert.That(
                source,
                Does.Contain("settings.metadata.accessesExtraAssemblies = true;"),
                "Nothing turns on \"accesses extra assemblies\" for the generated mod. Without it "
                + "the game cannot compile the generated script when the mod loads, even though it "
                + "builds in the editor.");

            Assert.That(
                source,
                Does.Contain(
                    "settings.metadata.requiredOn = ModMetadata.ModExistsOn.ClientAndServer;"),
                "Nothing marks the generated mod as needed on both sides. A dimension is spawned by "
                + "the server and drawn by the client, so without it a player without the mod is "
                + "refused with \"BadProtocolVersion\" and never told which mod is missing.");
        }

        [Test]
        public void TheSwitchesAreSetOnEveryGenerateAndNotOnlyWhenTheModIsNew()
        {
            string source = DimensionFrameworkSourceScanner.ReadByName(Generator);

            Assert.That(
                source,
                Does.Contain("EnsureTheModCanLoadAndBeJoined(templatePath);"),
                "The switches are written by a method nobody calls. It has to run beside "
                + "EnsureFrameworkModDependency on every generate, because both are ordinary tick "
                + "boxes a creator can turn off by hand.");
        }

        [Test]
        public void TheGeneratorSaysWhenItChangesEitherSwitch()
        {
            string source = DimensionFrameworkSourceScanner.ReadByName(Generator);

            Assert.That(
                source,
                Does.Contain("Turned on \\\"accesses extra assemblies\\\" for '"),
                "Changing a setting on somebody's mod without saying so is the silent kind of "
                + "helpfulness this framework is meant not to do.");

            Assert.That(
                source,
                Does.Contain("as needed on both "),
                "Same for the multiplayer switch: the creator has to be able to see it was changed.");
        }
    }
}
