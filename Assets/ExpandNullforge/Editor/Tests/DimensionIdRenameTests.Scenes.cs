#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// A scene has two names, and renaming either one.
    /// </summary>
    internal sealed partial class DimensionIdRenameTests
    {
        // -------------------------------------------------- the scene's two identities ---

        [Test]
        public void AScenesOwnIdIsRewrittenInWhatPointsAtIt()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Arena");
            SetString(scene, "templateId", "arena-template");

            DimensionBossAsset boss = Make<DimensionBossAsset>();
            SetString(boss, "bossId", "Ashling");
            SetString(boss, "arenaSceneId", "Arena");

            template.SetGlobalScenes(new[] { scene });
            template.SetGlobalBosses(new[] { boss });

            DimensionIdentityField sceneId = NameOn<SceneTemplateAsset>(scene, "sceneId");
            Assert.That(
                sceneId.InboundNamesUseIt,
                Is.True,
                "Other authored content finds a scene by sceneId — that is what FindSceneById " +
                "matches on — so renaming it has to rewrite what points at it.");

            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, scene, sceneId, "Hollow", false);
            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));

            string report;
            Assert.That(DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);
            Assert.That(ReadString(boss, "arenaSceneId"), Is.EqualTo("Hollow"));
            Assert.That(ReadString(scene, "sceneId"), Is.EqualTo("Hollow"));
            Assert.That(
                ReadString(scene, "templateId"),
                Is.EqualTo("arena-template"),
                "The scene's other name is a different identity and is not dragged along.");
        }

        [Test]
        public void AScenesNameToTheGameIsARenameOfItsOwn()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Arena");
            SetString(scene, "templateId", "arena-template");
            template.SetGlobalScenes(new[] { scene });

            DimensionIdentityField templateId = NameOn<SceneTemplateAsset>(scene, "templateId");
            Assert.That(
                templateId.InboundNamesUseIt,
                Is.False,
                "templateId is the key the running service files the compiled scene under — " +
                "NullforgeDimensionService looks scenes up by it in a dictionary. Nothing a " +
                "creator authors points at it, so renaming it rewrites nothing and changes what " +
                "the game is handed.");

            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, scene, templateId, "hollow-template", false);
            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));

            string report;
            Assert.That(DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);
            Assert.That(ReadString(scene, "templateId"), Is.EqualTo("hollow-template"));
            Assert.That(
                ReadString(scene, "sceneId"),
                Is.EqualTo("Arena"),
                "And the id other content points at is untouched.");
        }

        [Test]
        public void AScenesTwoNamesMayNotBecomeOneString()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Arena");
            SetString(scene, "templateId", "arena-template");
            template.SetGlobalScenes(new[] { scene });

            DimensionIdentityField sceneId = NameOn<SceneTemplateAsset>(scene, "sceneId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, scene, sceneId, "arena-template", false);

            Assert.That(
                plan.CanGo,
                Is.False,
                "Two names on one asset becoming the same string makes it impossible to tell " +
                "afterwards which of them anything meant.");
        }

        [Test]
        public void AScenesSecondNameDoesNotAlsoOfferADelete()
        {
            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            List<DimensionIdentityField> names = DimensionIdentityCatalog.Of(scene);

            int deletable = 0;
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].InboundNamesUseIt && names[i].ContainerProperty.Length > 0 &&
                    names[i].DeleteIsOfferedHere)
                {
                    deletable++;
                }
            }

            Assert.That(
                names.Count,
                Is.EqualTo(2),
                "A scene carries two names, and the card draws a section for each.");
            Assert.That(
                deletable,
                Is.EqualTo(1),
                "But only one of them is the name other content points at. The second planned " +
                "its delete against templateId — a string nothing points at — so it printed " +
                "'Nothing points at it.' and deleted a scene that boss arenas and biome pools do.");
        }
    }
}
#endif
