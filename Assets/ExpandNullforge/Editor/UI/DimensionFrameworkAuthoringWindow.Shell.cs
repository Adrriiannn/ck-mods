using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The window's frame: the wordmark bar, the Home landing screen, and the journey rail.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This half is UI Toolkit, which is what buys the rounded panels, the bundled typefaces,
    /// the hover transitions and the measured Core Keeper palette. The stage bodies underneath
    /// are still the original IMGUI panels, hosted unchanged inside an <see cref="IMGUIContainer"/>
    /// so that nothing a creator can reach today stops working while the panels are migrated one
    /// at a time.
    /// </para>
    /// <para>
    /// The frame owns navigation and the frame alone: the rail lists
    /// <see cref="DimensionJourney"/>'s fixed stages, so the order never shifts underfoot the way
    /// the old readiness-sorted sidebar did.
    /// </para>
    /// </remarks>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private const string StyleSheetPath =
            "Assets/ExpandNullforge/Editor/UI/DimensionsApi.uss";

        private VisualElement homeScreen;
        private VisualElement journeyScreen;
        private VisualElement railStageHost;
        private VisualElement listHost;
        private Label stageTitleLabel;
        private Label stageMaturityChip;
        private VisualElement diagnosticsRailRow;
        private Label diagnosticsAlertChip;
        private VisualElement actionFeedback;
        private Label actionFeedbackLabel;
        private bool showingHome = true;
        private DimensionBiomeStagePage biomePage;
        private VisualElement biomePageRoot;
        private IMGUIContainer legacyBodyHost;
        private VisualElement catalogPageHost;
        private readonly HashSet<string> showOriginalPanel = new HashSet<string>();
        private Button originalPanelToggle;
        private DimensionIdentityStagePage identityPage;
        private DimensionReviewStagePage reviewPage;
        private DimensionMapStagePage mapPage;
        private DimensionIssuesStagePage issuesPage;
        private DimensionPortalStagePage portalPage;
        private VisualElement portalPageRoot;
        private DimensionBlockStagePage blockPage;
        private VisualElement blockPageRoot;
        private VisualElement issuesPageRoot;
        private VisualElement mapPageRoot;
        private VisualElement reviewPageRoot;
        private VisualElement identityPageRoot;
        private Button continueButton;
        private readonly Dictionary<string, DimensionCollectionStagePage> catalogPages =
            new Dictionary<string, DimensionCollectionStagePage>();
        private readonly Dictionary<string, VisualElement> catalogPageRoots =
            new Dictionary<string, VisualElement>();

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("dim-root");

            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            root.Add(BuildTitleBar());

            homeScreen = BuildHomeScreen();
            journeyScreen = BuildJourneyScreen();
            root.Add(homeScreen);
            root.Add(journeyScreen);

            // A window reopened on a dimension it already had should land back in the journey,
            // not send the creator through the front door again.
            ShowHome(selectedTemplate == null);

            // A recompile rebuilds the frame while the last message is still held. Without this
            // the strip would come back blank and the creator would be told nothing about the
            // action they had just taken.
            RefreshActionFeedback();
        }

        private VisualElement BuildTitleBar()
        {
            VisualElement bar = new VisualElement();
            bar.AddToClassList("dim-titlebar");

            Label wordmark = new Label("DIMENSIONS");
            wordmark.AddToClassList("dim-wordmark");
            bar.Add(wordmark);

            Label accent = new Label("API");
            accent.AddToClassList("dim-wordmark-accent");
            bar.Add(accent);

            VisualElement spacer = new VisualElement();
            spacer.AddToClassList("dim-titlebar-spacer");
            bar.Add(spacer);
            return bar;
        }

        // ------------------------------------------------------------------ home ---

        private VisualElement BuildHomeScreen()
        {
            // No scroller: the landing is a poster, not a document — it fills the window and
            // holds still. Its content centres itself, and the one line that used to sit as
            // an eyebrow now signs off from the bottom edge instead.
            VisualElement home = new VisualElement();
            home.AddToClassList("dim-home");
            home.AddToClassList("dim-fill");
            home.style.overflow = Overflow.Hidden;
            AddCavernGlow(home);
            AddDriftingMotes(home);

            Label eyebrow = new Label("A Core Keeper modding framework");
            eyebrow.AddToClassList("dim-index");
            eyebrow.AddToClassList("dim-home-eyebrow");
            eyebrow.style.position = Position.Absolute;
            eyebrow.style.left = 0;
            eyebrow.style.right = 0;
            eyebrow.style.bottom = 10;
            eyebrow.style.marginBottom = 0;
            eyebrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            eyebrow.pickingMode = PickingMode.Ignore;
            home.Add(eyebrow);

            VisualElement titleRow = new VisualElement();
            titleRow.AddToClassList("dim-row");

            // The headline is the game's own TITLE artwork, re-lettered: "KEEPER" and the O
            // and R are the logo's actual pixels, the W, L and D are built in its stroke
            // language, and the cyan gradient is sampled from it — so the lockup matches the
            // splash art, not a font. Colours are baked into the art; no tint. Real text stays
            // underneath as the fallback: a missing art file should cost the flourish, never
            // the landing page.
            Texture2D lockupArt = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ExpandNullforge/Editor/UI/Art/CKTitleWorldKeeper.png");
            if (lockupArt != null)
            {
                VisualElement lockup = new VisualElement();
                lockup.style.backgroundImage = new StyleBackground(lockupArt);
                // Exactly 2x — integer scaling is what keeps pixel art from shimmering.
                lockup.style.width = lockupArt.width * 2;
                lockup.style.height = lockupArt.height * 2;
                lockup.pickingMode = PickingMode.Ignore;
                titleRow.Add(lockup);
            }
            else
            {
                Label world = new Label("World ");
                world.AddToClassList("dim-home-title");
                Label keeper = new Label("Keeper");
                keeper.AddToClassList("dim-home-title");
                keeper.AddToClassList("dim-home-title-accent");
                titleRow.Add(world);
                titleRow.Add(keeper);
            }

            home.Add(titleRow);

            home.Add(BuildWavySubtitle(
                "Let your creativity flow freely when creating your dream dimension."));

            VisualElement ctaRow = new VisualElement();
            ctaRow.AddToClassList("dim-cta-row");
            Button create = new Button(CreateNewDimension) { text = "Create a Dimension" };
            create.AddToClassList("dim-button");
            create.AddToClassList("dim-button-primary");
            ctaRow.Add(create);

            // Only offered when there is genuinely something to reopen. With no dimensions yet
            // the row holds the one button that can do anything, centred on its own.
            continueButton = new Button(OpenLastDimension) { text = "Open last dimension" };
            continueButton.AddToClassList("dim-button");
            continueButton.AddToClassList("dim-button-ghost");
            ctaRow.Add(continueButton);
            home.Add(ctaRow);

            VisualElement list = new VisualElement();
            list.AddToClassList("dim-list");
            Label listLabel = new Label("Your dimensions");
            listLabel.AddToClassList("dim-index");
            list.Add(listLabel);

            listHost = new VisualElement();
            list.Add(listHost);
            home.Add(list);

            return home;
        }

        // ----------------------------------------------------------------- depth ---

        /// <summary>
        /// The subtitle with a slow pulse of light travelling through it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One label cannot brighten letter by letter, so the line is rebuilt as one label
        /// per character, grouped into per-word rows inside a wrapping container — words wrap
        /// as words, characters stay individually paintable. A single scheduled tick moves a
        /// gaussian window of brightness along the string: letters inside the window lerp
        /// toward white and carry a soft cyan text-shadow, letters outside rest at the body
        /// colour. The window loops with a pause-width of slack so the sweep breathes instead
        /// of nagging.
        /// </para>
        /// <para>
        /// The tick lives on the container's own scheduler, so it runs only while the landing
        /// page is actually attached to a panel — no work goes on behind other pages.
        /// </para>
        /// </remarks>
        private static VisualElement BuildWavySubtitle(string text)
        {
            VisualElement host = new VisualElement();
            host.AddToClassList("dim-home-sub");
            host.style.flexDirection = FlexDirection.Row;
            host.style.flexWrap = Wrap.Wrap;
            host.style.justifyContent = Justify.Center;
            host.pickingMode = PickingMode.Ignore;

            List<Label> letters = new List<Label>();
            string[] words = text.Split(' ');
            for (int w = 0; w < words.Length; w++)
            {
                VisualElement word = new VisualElement();
                word.style.flexDirection = FlexDirection.Row;
                word.pickingMode = PickingMode.Ignore;
                foreach (char c in words[w])
                {
                    Label letter = new Label(c.ToString());
                    letter.style.marginLeft = 0;
                    letter.style.marginRight = 0;
                    letter.style.paddingLeft = 0;
                    letter.style.paddingRight = 0;
                    letter.pickingMode = PickingMode.Ignore;
                    word.Add(letter);
                    letters.Add(letter);
                }

                host.Add(word);
                if (w < words.Length - 1)
                {
                    VisualElement gap = new VisualElement();
                    gap.style.width = 4;
                    gap.pickingMode = PickingMode.Ignore;
                    host.Add(gap);
                }
            }

            Color restColor = new Color(0xa1 / 255f, 0xae / 255f, 0xdd / 255f);
            Color brightColor = new Color(0.92f, 0.97f, 1f);
            Color glowColor = new Color(0x64 / 255f, 0xdc / 255f, 1f);
            int count = letters.Count;

            // One full sweep plus a rest, about seven seconds end to end.
            const float SweepSeconds = 5.5f;
            const float RestSlack = 14f;
            const float Sigma = 3.2f;

            host.schedule.Execute(() =>
            {
                double now = EditorApplication.timeSinceStartup;
                float cycle = (float)(now % SweepSeconds) / SweepSeconds;
                float sweep = cycle * (count + RestSlack) - RestSlack * 0.5f;

                for (int i = 0; i < count; i++)
                {
                    float distance = (i - sweep) / Sigma;
                    float intensity = Mathf.Exp(-0.5f * distance * distance);

                    letters[i].style.color = Color.Lerp(restColor, brightColor, intensity);
                    letters[i].style.textShadow = new TextShadow
                    {
                        offset = Vector2.zero,
                        blurRadius = 5f,
                        color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.85f * intensity)
                    };
                }
            }).Every(40);

            return host;
        }

        /// <summary>
        /// The soft pool of light low on every working page.
        /// </summary>
        /// <remarks>
        /// This began as a rendering accident: the Portal Studio's floor glow once escaped its
        /// preview and washed across the page below, and it looked like the page itself was lit
        /// from beneath — a cavern floor catching portal light. The accident was kept on
        /// purpose, quieter than the Home screen's glow so the forms above it stay legible.
        /// It ignores the pointer entirely and sits behind everything added after it.
        /// </remarks>
        private static void AddPageGlow(VisualElement host)
        {
            VisualElement pool = new VisualElement();
            pool.pickingMode = PickingMode.Ignore;
            pool.style.position = Position.Absolute;
            pool.style.left = new Length(50, LengthUnit.Percent);
            pool.style.bottom = -190;
            pool.style.width = 1040;
            pool.style.height = 520;
            pool.style.marginLeft = -520;
            pool.style.backgroundImage = new StyleBackground(DimensionsApiTextures.RadialGlow);
            pool.style.unityBackgroundImageTintColor = new StyleColor(
                new Color(0.098f, 0.741f, 0.776f, 0.30f));
            host.Add(pool);

            VisualElement ember = new VisualElement();
            ember.pickingMode = PickingMode.Ignore;
            ember.style.position = Position.Absolute;
            ember.style.left = new Length(26, LengthUnit.Percent);
            ember.style.bottom = -130;
            ember.style.width = 560;
            ember.style.height = 320;
            ember.style.marginLeft = -280;
            ember.style.backgroundImage = new StyleBackground(DimensionsApiTextures.RadialGlow);
            ember.style.unityBackgroundImageTintColor = new StyleColor(
                new Color(0.290f, 0.800f, 0.969f, 0.16f));
            host.Add(ember);
        }

        /// <summary>
        /// The pool of Core light below the words. One soft glow, the way the game lights a
        /// cavern: darkness everywhere and one thing emitting.
        /// </summary>
        private static void AddCavernGlow(VisualElement host)
        {
            VisualElement glow = new VisualElement();
            glow.pickingMode = PickingMode.Ignore;
            glow.style.position = Position.Absolute;
            glow.style.left = new Length(50, LengthUnit.Percent);
            glow.style.bottom = -260;
            glow.style.width = 900;
            glow.style.height = 520;
            glow.style.marginLeft = -450;
            glow.style.backgroundImage = new StyleBackground(DimensionsApiTextures.RadialGlow);
            glow.style.unityBackgroundImageTintColor = new StyleColor(
                new Color(0.290f, 0.800f, 0.969f, 0.20f));
            host.Add(glow);
            // The purple counter-glow that used to hang from the top edge is gone by request:
            // one light source, from below, and it is the Core's blue.
        }

        /// <summary>
        /// The spore motes: a handful of slow drifting lights, the cave breathing. USS has no
        /// keyframes, so each one is nudged along a slow sine on the panel's own scheduler.
        /// </summary>
        private void AddDriftingMotes(VisualElement host)
        {
            float[,] motes =
            {
                // x%,  y%,  size, hue index, seconds per drift, phase
                { 16f, 62f, 5f, 0f, 9.0f, 0.0f },
                { 78f, 46f, 3f, 1f, 12.0f, 2.0f },
                { 64f, 74f, 4f, 2f, 10.5f, 1.0f },
                { 30f, 34f, 3f, 3f, 13.0f, 3.0f },
                { 48f, 26f, 2f, 4f, 11.0f, 4.2f },
                { 86f, 68f, 3f, 0f, 14.0f, 1.6f },
            };

            Color[] hues =
            {
                new Color(0.290f, 0.800f, 0.969f, 1f),
                new Color(0.098f, 0.741f, 0.776f, 1f),
                new Color(1.000f, 0.580f, 0.000f, 1f),
                new Color(0.647f, 0.333f, 0.643f, 1f),
                new Color(0.549f, 0.910f, 1.000f, 1f),
            };

            List<VisualElement> spawned = new List<VisualElement>();
            List<float> periods = new List<float>();
            List<float> phases = new List<float>();
            List<float> baseTop = new List<float>();

            for (int i = 0; i < motes.GetLength(0); i++)
            {
                VisualElement mote = new VisualElement();
                mote.pickingMode = PickingMode.Ignore;
                float size = motes[i, 2];
                mote.style.position = Position.Absolute;
                mote.style.left = new Length(motes[i, 0], LengthUnit.Percent);
                mote.style.top = new Length(motes[i, 1], LengthUnit.Percent);
                // The glow sprite is much larger than the mote so the light spills around it.
                mote.style.width = size * 7f;
                mote.style.height = size * 7f;
                mote.style.backgroundImage = new StyleBackground(DimensionsApiTextures.RadialGlow);
                mote.style.unityBackgroundImageTintColor =
                    new StyleColor(hues[(int)motes[i, 3]]);
                host.Add(mote);

                spawned.Add(mote);
                periods.Add(motes[i, 4]);
                phases.Add(motes[i, 5]);
                baseTop.Add(motes[i, 1]);
            }

            double startTime = EditorApplication.timeSinceStartup;
            host.schedule.Execute(() =>
            {
                // The home screen hides while a dimension is open; twelve style writes every
                // 40ms for an invisible panel is exactly the waste this UI must not have.
                if (homeScreen != null &&
                    homeScreen.resolvedStyle.display == DisplayStyle.None)
                {
                    return;
                }

                float now = (float)(EditorApplication.timeSinceStartup - startTime);
                for (int i = 0; i < spawned.Count; i++)
                {
                    float t = (now / periods[i] + phases[i]) * Mathf.PI * 2f;
                    float rise = Mathf.Sin(t) * 16f;
                    float sway = Mathf.Cos(t * 0.6f) * 7f;
                    spawned[i].style.translate =
                        new StyleTranslate(new Translate(sway, rise));
                    // Motes fade as they rise, the way a spore catches the light and loses it.
                    float alpha = 0.35f + 0.4f * (0.5f + 0.5f * Mathf.Cos(t));
                    spawned[i].style.opacity = alpha;
                }
            }).Every(40);
        }

        private void RefreshDimensionList()
        {
            if (listHost == null)
            {
                return;
            }

            RefreshContinueButton();
            listHost.Clear();
            List<DimensionTemplateAsset> templates = FindDimensionTemplates();
            if (templates.Count == 0)
            {
                Label empty = new Label(
                    "No dimensions yet. Create one and it will appear here.");
                empty.AddToClassList("dim-list-empty");
                listHost.Add(empty);
                return;
            }

            for (int i = 0; i < templates.Count; i++)
            {
                listHost.Add(BuildDimensionRow(templates[i]));
            }
        }

        private VisualElement BuildDimensionRow(DimensionTemplateAsset template)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("dim-list-row");

            // Name over its progress bar on the left, counts on the right. The bar reads the
            // same navigation model as the journey rail's ticks, so the two can never tell a
            // creator different stories about how far along a dimension is.
            VisualElement left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.flexShrink = 1;

            Label name = new Label(ResolveTemplateDisplayName(template));
            name.AddToClassList("dim-list-name");
            left.Add(name);

            int percent = ComputeCompletionPercent(template);
            VisualElement track = new VisualElement();
            track.AddToClassList("dim-progress-track");
            track.tooltip = percent + "% of this dimension's stages are ready.";
            VisualElement fill = new VisualElement();
            fill.AddToClassList("dim-progress-fill");
            fill.style.width = new Length(percent, LengthUnit.Percent);
            track.Add(fill);
            left.Add(track);
            row.Add(left);

            Label meta = new Label(DescribeTemplate(template) + " · " + percent + "%");
            meta.AddToClassList("dim-list-meta");
            row.Add(meta);

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                OpenDimension(template);
                evt.StopPropagation();
            });
            return row;
        }

        /// <summary>
        /// How far along a dimension is, as the fraction of journey stages the readiness
        /// model calls Ready (Partial counts half).
        /// </summary>
        /// <remarks>
        /// World Generation is left out of the maths for now: its readiness is permanently
        /// Missing until that domain's rebuild (the honesty audit's #7), and counting it
        /// would cap every bar below full for a reason no creator can act on.
        /// </remarks>
        private static int ComputeCompletionPercent(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return 0;
            }

            try
            {
                DimensionTemplateAuthoringWorkspace judged =
                    DimensionTemplateAuthoringWorkspaceUtility.BuildWorkspaceFromTemplate(
                        template,
                        DimensionApiModFolderUtility.ResolvePreferredDimensionAssetFolder());
                DimensionTemplateCustomizerNavigationModel navigation =
                    judged == null ? null : judged.Navigation;
                if (navigation == null)
                {
                    return 0;
                }

                float total = 0f;
                float sum = 0f;
                IReadOnlyList<DimensionJourneyStage> stages = DimensionJourney.Stages;
                for (int i = 0; i < stages.Count; i++)
                {
                    if (string.Equals(stages[i].SectionId, "worldrules", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    total += 1f;
                    DimensionAuthoringReadinessState state =
                        DimensionJourney.ResolveState(navigation, stages[i].SectionId);
                    if (state == DimensionAuthoringReadinessState.Ready)
                    {
                        sum += 1f;
                    }
                    else if (state == DimensionAuthoringReadinessState.Partial)
                    {
                        sum += 0.5f;
                    }
                }

                return total <= 0f ? 0 : Mathf.RoundToInt(sum / total * 100f);
            }
            catch (System.Exception)
            {
                // A template too broken to judge shows an empty bar, not a broken landing page.
                return 0;
            }
        }

        private static string ResolveTemplateDisplayName(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return "Unnamed dimension";
            }

            string displayName = template.DisplayName;
            return string.IsNullOrEmpty(displayName) ? template.name : displayName;
        }

        private static string DescribeTemplate(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return string.Empty;
            }

            int biomes = template.Biomes == null ? 0 : template.Biomes.Length;
            int tilesets = template.Tilesets == null ? 0 : template.Tilesets.Length;
            return Plural(biomes, "biome") + " · " + Plural(tilesets, "tileset");
        }

        private static string Plural(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? string.Empty : "s");
        }

        private static List<DimensionTemplateAsset> FindDimensionTemplates()
        {
            List<DimensionTemplateAsset> results = new List<DimensionTemplateAsset>();
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(DimensionTemplateAsset));
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                DimensionTemplateAsset template =
                    AssetDatabase.LoadAssetAtPath<DimensionTemplateAsset>(path);
                if (template != null)
                {
                    results.Add(template);
                }
            }

            results.Sort((left, right) => string.Compare(
                ResolveTemplateDisplayName(left),
                ResolveTemplateDisplayName(right),
                System.StringComparison.OrdinalIgnoreCase));
            return results;
        }

        private void CreateNewDimension()
        {
            DimensionTemplateCreationWizardWindow.Open();
        }

        /// <summary>Where the last opened dimension is remembered between sessions.</summary>
        private const string LastDimensionKey = "ExpandNullforge.LastDimensionGuid";

        /// <summary>
        /// The dimension the creator was last working on, if it still exists. Stored per project
        /// by asset guid rather than by path, so moving or renaming the asset does not lose it.
        /// </summary>
        private static DimensionTemplateAsset ResolveLastDimension()
        {
            string guid = EditorPrefs.GetString(LastDimensionKey, string.Empty);
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<DimensionTemplateAsset>(path);
        }

        private static void RememberLastDimension(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(template);
            string guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
            {
                EditorPrefs.SetString(LastDimensionKey, guid);
            }
        }

        private void OpenLastDimension()
        {
            DimensionTemplateAsset last = ResolveLastDimension();
            if (last == null)
            {
                List<DimensionTemplateAsset> templates = FindDimensionTemplates();
                last = templates.Count > 0 ? templates[0] : null;
            }

            OpenDimension(last);
        }

        /// <summary>
        /// Shows the continue button only when it would do something, and names the dimension it
        /// would open so pressing it is never a surprise.
        /// </summary>
        private void RefreshContinueButton()
        {
            if (continueButton == null)
            {
                return;
            }

            DimensionTemplateAsset last = ResolveLastDimension();
            if (last == null)
            {
                List<DimensionTemplateAsset> templates = FindDimensionTemplates();
                last = templates.Count > 0 ? templates[0] : null;
            }

            if (last == null)
            {
                continueButton.style.display = DisplayStyle.None;
                return;
            }

            continueButton.style.display = DisplayStyle.Flex;
            continueButton.text = "Open " + ResolveTemplateDisplayName(last);
        }

        private void OpenDimension(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return;
            }

            RememberLastDimension(template);
            SelectTemplateFromShell(template);
            ShowHome(false);
        }

        // --------------------------------------------------------------- journey ---

        private VisualElement BuildJourneyScreen()
        {
            VisualElement shell = new VisualElement();
            shell.AddToClassList("dim-row");
            shell.AddToClassList("dim-fill");

            VisualElement rail = new VisualElement();
            rail.AddToClassList("dim-rail");
            Label railTitle = new Label("SECTIONS");
            railTitle.AddToClassList("dim-rail-title");
            rail.Add(railTitle);

            railStageHost = new VisualElement();
            rail.Add(railStageHost);

            VisualElement railSpacer = new VisualElement();
            railSpacer.AddToClassList("dim-grow");
            rail.Add(railSpacer);

            VisualElement railFoot = new VisualElement();
            railFoot.AddToClassList("dim-rail-foot");

            // Diagnostics is not a build step, so it does not sit among them. It is pinned at
            // the foot the way a status bar is: always in the same place, out of the way until
            // something needs attention.
            diagnosticsRailRow = BuildDiagnosticsRailRow();
            railFoot.Add(diagnosticsRailRow);

            Button backHome = new Button(() => ShowHome(true)) { text = "← All dimensions" };
            backHome.AddToClassList("dim-button");
            backHome.AddToClassList("dim-button-ghost");
            backHome.AddToClassList("dim-rail-home");
            railFoot.Add(backHome);
            rail.Add(railFoot);
            shell.Add(rail);

            VisualElement content = new VisualElement();
            content.AddToClassList("dim-content");
            AddPageGlow(content);

            VisualElement head = new VisualElement();
            head.AddToClassList("dim-stage-head");
            stageTitleLabel = new Label(string.Empty);
            stageTitleLabel.AddToClassList("dim-h2");
            head.Add(stageTitleLabel);

            // How far this section actually got, read from the capability registry. It sits beside
            // the stage's name because that is the moment it matters — before a creator spends an
            // afternoon on a page whose backend is a preview. The registry was written to be this
            // and until now was drawn only by the panel body nobody can reach.
            stageMaturityChip = new Label(string.Empty);
            stageMaturityChip.AddToClassList("dim-chip");
            stageMaturityChip.style.display = DisplayStyle.None;
            head.Add(stageMaturityChip);

            // Some stages own a specialised editor as well as their fields. Rather than choose
            // one and hide the other, the header offers both.
            originalPanelToggle = new Button(ToggleOriginalPanel);
            originalPanelToggle.AddToClassList("dim-button");
            originalPanelToggle.AddToClassList("dim-button-ghost");
            originalPanelToggle.AddToClassList("dim-header-toggle");
            head.Add(originalPanelToggle);
            content.Add(head);

            content.Add(BuildActionFeedback());

            // Biomes has a page of its own because a biome is not a list of fields, it is a
            // place; everything else is built from its description in the catalog. Whatever the
            // catalog does not describe still shows its original panel, so no capability is lost
            // while the last of them are brought across.
            biomePage = new DimensionBiomeStagePage(
                source => RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.CreateBiome(selectedTemplate, source)),
                RunAssetAction,
                Repaint);
            biomePageRoot = biomePage.Build();
            content.Add(biomePageRoot);

            blockPage = new DimensionBlockStagePage(
                tilesetStudio,
                RunAssetAction,
                FocusBlockItemFromShell,
                Repaint,
                ReportToCreator);
            blockPageRoot = blockPage.Build();
            content.Add(blockPageRoot);

            portalPage = new DimensionPortalStagePage(
                ResolvePortalStudioForShell,
                HandlePortalStudioResultFromShell,
                GetPortalVersionTabForShell,
                SetPortalVersionTabFromShell,
                HasPortalVersionForShell,
                IsPortalVersionEnabledForShell,
                TogglePortalVersionEnabledFromShell,
                ReportToCreator);
            portalPageRoot = portalPage.Build();
            content.Add(portalPageRoot);

            issuesPage = new DimensionIssuesStagePage(
                () => workspace,
                () => viewModel == null ? null : viewModel.Navigation,
                GoToStage);
            issuesPageRoot = issuesPage.Build();
            content.Add(issuesPageRoot);

            mapPage = new DimensionMapStagePage(
                DrawLayoutStudioForShell,
                () => RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.CreateLayoutTemplate(selectedTemplate)));
            mapPageRoot = mapPage.Build();
            content.Add(mapPageRoot);

            reviewPage = new DimensionReviewStagePage(
                () => viewModel != null && viewModel.SessionReport != null,
                () => viewModel.SessionReport.ManifestExportPreview,
                () =>
                {
                    if (viewModel != null && viewModel.SessionReport != null)
                    {
                        ValidateExportPreview(viewModel.SessionReport.ManifestExportPreview);
                    }
                },
                BuildDimensionFromShell,
                ShowGeneratedAssetsFromShell,
                () => lastGeneratedManifestAsset != null);
            reviewPageRoot = reviewPage.Build();
            content.Add(reviewPageRoot);

            identityPage = new DimensionIdentityStagePage();
            identityPageRoot = identityPage.Build();
            content.Add(identityPageRoot);

            catalogPageHost = new VisualElement();
            catalogPageHost.AddToClassList("dim-fill");
            content.Add(catalogPageHost);

            legacyBodyHost = new IMGUIContainer(DrawLegacyStageBody);
            legacyBodyHost.AddToClassList("dim-panel-host");
            legacyBodyHost.style.flexGrow = 1;
            content.Add(legacyBodyHost);

            shell.Add(content);
            return shell;
        }

        /// <summary>
        /// The strip under the stage's name where the window tells the creator what just happened.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Built once and hidden, rather than added and removed, so that publishing a message can
        /// never reorder the page under a creator's cursor. It sits directly beneath the header
        /// because that is where the eye already is after clicking a button in it, and above the
        /// body so it never scrolls out of sight with the fields.
        /// </para>
        /// <para>
        /// The message is dismissable and nothing dismisses it on a timer: a warning that
        /// disappears by itself is a warning the creator can miss entirely.
        /// </para>
        /// </remarks>
        private VisualElement BuildActionFeedback()
        {
            actionFeedback = new VisualElement();
            actionFeedback.AddToClassList("dim-feedback");
            actionFeedback.style.display = DisplayStyle.None;

            actionFeedbackLabel = new Label(string.Empty);
            actionFeedbackLabel.AddToClassList("dim-feedback-text");
            actionFeedback.Add(actionFeedbackLabel);

            Button dismiss = new Button(DismissActionFeedback) { text = "×" };
            dismiss.AddToClassList("dim-button");
            dismiss.AddToClassList("dim-button-ghost");
            dismiss.AddToClassList("dim-feedback-dismiss");
            dismiss.tooltip = "Put this message away.";
            actionFeedback.Add(dismiss);
            return actionFeedback;
        }

        /// <summary>
        /// Puts the last published message on screen, or takes the strip away when there is none.
        /// </summary>
        /// <remarks>
        /// Called from the setter of <c>lastEditorActionMessage</c>, so every one of the
        /// twenty-one places that publish one arrives here without knowing this exists. Guarded on
        /// the elements being built, because a message can be published while the window is still
        /// opening and before <c>CreateGUI</c> has run.
        /// </remarks>
        /// <summary>Whether a published message is waiting for the strip to be rebuilt.</summary>
        private bool actionFeedbackRefreshQueued;

        /// <summary>
        /// Asks for the strip to be rebuilt on the next UITK frame rather than here and now.
        /// </summary>
        /// <remarks>
        /// <para>
        /// BECAUSE FIVE OF THE PUBLISH SITES ARE INSIDE OnGUI. <c>DrawPortalEditor</c>,
        /// <c>DrawTilesetsEditor</c>, <c>DrawSerializedAsset</c> and the two portal-profile
        /// creators all run from the <c>IMGUIContainer</c> that hosts the legacy body, and the
        /// strip is that container's SIBLING in the UITK tree. Rebuilding it from inside an IMGUI
        /// pass changes an element's <c>display</c> and its class list between that pass's Layout
        /// and Repaint — the same class of mid-pass mutation <c>DrawLegacyStageBodyInner</c> already
        /// defends against by deferring its own read.
        /// </para>
        /// <para>
        /// The flag is not an optimisation: without it a message published on every draw would
        /// queue one callback per frame for as long as the window is open.
        /// </para>
        /// </remarks>
        private void ScheduleActionFeedbackRefresh()
        {
            if (actionFeedback == null || actionFeedbackRefreshQueued)
            {
                // No strip yet means the message was published before CreateGUI built the frame.
                // CreateGUI calls RefreshActionFeedback once at the end for exactly that case, so
                // nothing published this early is lost.
                return;
            }

            actionFeedbackRefreshQueued = true;
            actionFeedback.schedule.Execute(() =>
            {
                actionFeedbackRefreshQueued = false;
                RefreshActionFeedback();
            });
        }

        private void RefreshActionFeedback()
        {
            if (actionFeedback == null || actionFeedbackLabel == null)
            {
                return;
            }

            string message = lastEditorActionMessageValue;
            if (string.IsNullOrEmpty(message))
            {
                actionFeedback.style.display = DisplayStyle.None;
                return;
            }

            actionFeedbackLabel.text = message;
            actionFeedback.RemoveFromClassList("dim-feedback-warn");
            actionFeedback.RemoveFromClassList("dim-feedback-bad");
            if (lastEditorActionTypeValue == MessageType.Warning)
            {
                actionFeedback.AddToClassList("dim-feedback-warn");
            }
            else if (lastEditorActionTypeValue == MessageType.Error)
            {
                actionFeedback.AddToClassList("dim-feedback-bad");
            }

            actionFeedback.style.display = DisplayStyle.Flex;
        }

        private void DismissActionFeedback()
        {
            lastEditorActionMessage = string.Empty;
        }

        /// <summary>
        /// A message from a page that has no asset action to report, said in the same strip.
        /// </summary>
        /// <remarks>
        /// Two pages had nowhere to put a failure and wrote it to the Console instead — where a
        /// creator who has not opened the Console never sees it, and where it looks like a bug in
        /// the framework rather than an answer to what they just clicked.
        /// </remarks>
        private void ReportToCreator(string message, MessageType type)
        {
            lastEditorActionMessage = message ?? string.Empty;
            lastEditorActionType = type;
            Repaint();
        }

        /// <summary>The catalog page for a stage, built once and kept.</summary>
        private DimensionCollectionStagePage ResolveCatalogPage(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
            {
                return null;
            }

            DimensionCollectionStagePage page;
            if (catalogPages.TryGetValue(sectionId, out page))
            {
                return page;
            }

            DimensionStageDescriptor descriptor;
            if (!DimensionStageCatalog.TryGetStage(sectionId, out descriptor))
            {
                return null;
            }

            page = new DimensionCollectionStagePage(descriptor, RunAssetAction);
            VisualElement root = page.Build();
            root.style.display = DisplayStyle.None;
            catalogPageHost.Add(root);
            catalogPageRoots[sectionId] = root;
            catalogPages[sectionId] = page;
            return page;
        }

        /// <summary>
        /// Sections that also have an original panel behind them. A synthetic stage has none, so
        /// offering the toggle there would promise something that does not exist.
        /// </summary>
        private static bool HasOriginalPanel(string sectionId)
        {
            // Nothing offers it any more. Every stage that used to fall back to an original
            // panel now covers all of it: Items and Gear, Mobs and Places each carry their
            // content types as tabs, and any value no card names is still reachable under
            // "Everything else" on the page itself. Offering a second, older looking door to
            // the same values is exactly what this rebuild set out to remove.
            return false;
        }

        private void ToggleOriginalPanel()
        {
            if (string.IsNullOrEmpty(activeSectionId))
            {
                return;
            }

            if (!showOriginalPanel.Remove(activeSectionId))
            {
                showOriginalPanel.Add(activeSectionId);
            }

            RefreshStageBody();
            Repaint();
        }

        private void RefreshStageBody()
        {
            if (biomePageRoot == null || legacyBodyHost == null || catalogPageHost == null ||
                identityPageRoot == null || reviewPageRoot == null || mapPageRoot == null ||
                issuesPageRoot == null || blockPageRoot == null || portalPageRoot == null)
            {
                return;
            }

            bool original = showOriginalPanel.Contains(activeSectionId);
            bool isBiomes = !original &&
                string.Equals(activeSectionId, "biomes", System.StringComparison.Ordinal);
            bool isIdentity = !original &&
                string.Equals(activeSectionId, "dimension", System.StringComparison.Ordinal);
            bool isReview = !original &&
                string.Equals(activeSectionId, "export", System.StringComparison.Ordinal);
            bool isMap = !original &&
                string.Equals(activeSectionId, "layout", System.StringComparison.Ordinal);
            bool isIssues = !original &&
                string.Equals(activeSectionId, "diagnostics", System.StringComparison.Ordinal);
            bool isBlocks = !original &&
                string.Equals(activeSectionId, "tilesets", System.StringComparison.Ordinal);
            bool isPortal = !original &&
                string.Equals(activeSectionId, "portals", System.StringComparison.Ordinal);
            DimensionCollectionStagePage catalogPage = original || isBiomes || isIdentity || isReview || isMap || isIssues || isBlocks || isPortal
                ? null
                : ResolveCatalogPage(activeSectionId);

            biomePageRoot.style.display = isBiomes ? DisplayStyle.Flex : DisplayStyle.None;
            identityPageRoot.style.display = isIdentity ? DisplayStyle.Flex : DisplayStyle.None;
            reviewPageRoot.style.display = isReview ? DisplayStyle.Flex : DisplayStyle.None;
            mapPageRoot.style.display = isMap ? DisplayStyle.Flex : DisplayStyle.None;
            issuesPageRoot.style.display = isIssues ? DisplayStyle.Flex : DisplayStyle.None;
            blockPageRoot.style.display = isBlocks ? DisplayStyle.Flex : DisplayStyle.None;
            portalPageRoot.style.display = isPortal ? DisplayStyle.Flex : DisplayStyle.None;
            catalogPageHost.style.display =
                catalogPage != null ? DisplayStyle.Flex : DisplayStyle.None;
            legacyBodyHost.style.display =
                isBiomes || isIdentity || isReview || isMap || isIssues || isBlocks || isPortal ||
                catalogPage != null
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;

            foreach (KeyValuePair<string, VisualElement> pair in catalogPageRoots)
            {
                pair.Value.style.display =
                    catalogPage != null &&
                    string.Equals(pair.Key, activeSectionId, System.StringComparison.Ordinal)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            if (isBiomes)
            {
                biomePage.Refresh(selectedTemplate);
            }
            else if (isIdentity)
            {
                identityPage.Refresh(selectedTemplate);
            }
            else if (isReview)
            {
                reviewPage.Refresh(selectedTemplate);
            }
            else if (isMap)
            {
                mapPage.Refresh(selectedTemplate);
            }
            else if (isIssues)
            {
                issuesPage.Refresh(selectedTemplate);
            }
            else if (isBlocks)
            {
                blockPage.Refresh(selectedTemplate);
            }
            else if (isPortal)
            {
                EnsurePortalProfileReadyForShell();
                portalPage.Refresh(selectedTemplate, ResolvePortalProfileForShell());
            }
            else if (catalogPage != null)
            {
                catalogPage.Refresh(selectedTemplate);
            }

            RefreshOriginalPanelToggle(original);
            RefreshStageMaturityChip();
        }

        /// <summary>
        /// Puts the active section's honest maturity beside its name.
        /// </summary>
        /// <remarks>
        /// Hidden rather than filled with a guess when a section has no capability record of its
        /// own: a badge naming another feature's maturity would be believed, and believed wrongly.
        /// The full note goes in the tooltip, because the one word on the chip is a summary and the
        /// note is where "what is still missing" is actually written down.
        /// </remarks>
        private void RefreshStageMaturityChip()
        {
            if (stageMaturityChip == null)
            {
                return;
            }

            ExpandNullforge.Api.DimensionCapability capability;
            if (!DimensionStageMaturity.TryResolve(activeSectionId, out capability))
            {
                stageMaturityChip.style.display = DisplayStyle.None;
                return;
            }

            stageMaturityChip.RemoveFromClassList("dim-chip-ready");
            stageMaturityChip.RemoveFromClassList("dim-chip-warn");
            stageMaturityChip.RemoveFromClassList("dim-chip-blocked");
            stageMaturityChip.AddToClassList(DimensionStageMaturity.ChipClass(capability.Maturity));
            stageMaturityChip.text = DimensionStageMaturity.Label(capability.Maturity);
            stageMaturityChip.tooltip = capability.Title + " — " + capability.Note;
            stageMaturityChip.style.display = DisplayStyle.Flex;
        }

        private void RefreshOriginalPanelToggle(bool showingOriginal)
        {
            if (originalPanelToggle == null)
            {
                return;
            }

            if (!HasOriginalPanel(activeSectionId))
            {
                originalPanelToggle.style.display = DisplayStyle.None;
                return;
            }

            originalPanelToggle.style.display = DisplayStyle.Flex;
            originalPanelToggle.text = showingOriginal
                ? "Back to the new page"
                : ResolveOriginalPanelLabel(activeSectionId);
        }

        private static string ResolveOriginalPanelLabel(string sectionId)
        {
            switch (sectionId)
            {
                case "tilesets":
                    return "Open the Block Studio";
                case "biomes":
                    return "Open the original panel";
                default:
                    return "Open the original panel";
            }
        }

        private void RefreshRail()
        {
            if (railStageHost == null)
            {
                return;
            }

            railStageHost.Clear();
            IReadOnlyList<DimensionJourneyStage> stages = DimensionJourney.Stages;
            int activeIndex = DimensionJourney.IndexOf(activeSectionId);
            for (int i = 0; i < stages.Count; i++)
            {
                railStageHost.Add(BuildRailStage(stages[i], i, activeIndex));
            }

            if (diagnosticsRailRow != null)
            {
                bool onDiagnostics = string.Equals(
                    activeSectionId,
                    DimensionJourney.DiagnosticsStageId,
                    System.StringComparison.Ordinal);
                diagnosticsRailRow.EnableInClassList("dim-stage-current", onDiagnostics);
            }

            if (diagnosticsAlertChip != null)
            {
                DimensionAuthoringReadinessState diagnosticsState = DimensionJourney.ResolveState(
                    viewModel == null ? null : viewModel.Navigation,
                    DimensionJourney.DiagnosticsStageId);
                diagnosticsAlertChip.style.display =
                    diagnosticsState == DimensionAuthoringReadinessState.Blocked
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            // Nothing may be silently unreachable. Every known section either is a rail item,
            // is pinned, or had its fields moved onto a page that is. A section this warning
            // names has none of those and needs a home before it ships.
            List<string> outside = DimensionJourney.FindSectionsOutsideTheJourney(
                viewModel == null ? null : viewModel.Navigation);
            if (outside.Count > 0)
            {
                Debug.LogWarning(
                    "[Dimensions API] Sections with no way in from the rail: " +
                    string.Join(", ", outside));
            }
        }

        /// <summary>The pinned Diagnostics row at the rail's foot.</summary>
        private VisualElement BuildDiagnosticsRailRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("dim-stage");

            Label bullet = new Label("◦");
            bullet.AddToClassList("dim-stage-num");
            row.Add(bullet);

            VisualElement labels = new VisualElement();
            labels.AddToClassList("dim-stage-labels");
            Label name = new Label("Diagnostics");
            name.AddToClassList("dim-stage-name");
            labels.Add(name);
            row.Add(labels);

            // A badge only when something demands action: errors light it, warnings do not,
            // and a quiet dimension shows a quiet row.
            diagnosticsAlertChip = new Label("!");
            diagnosticsAlertChip.AddToClassList("dim-chip");
            diagnosticsAlertChip.AddToClassList("dim-chip-blocked");
            diagnosticsAlertChip.style.display = DisplayStyle.None;
            row.Add(diagnosticsAlertChip);

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                GoToStage(DimensionJourney.DiagnosticsStageId);
                evt.StopPropagation();
            });
            return row;
        }

        private VisualElement BuildRailStage(
            DimensionJourneyStage stage,
            int index,
            int activeIndex)
        {
            DimensionAuthoringReadinessState state = DimensionJourney.ResolveState(
                viewModel == null ? null : viewModel.Navigation,
                stage.SectionId);
            bool done = state == DimensionAuthoringReadinessState.Ready;

            VisualElement row = new VisualElement();
            row.AddToClassList("dim-stage");
            if (index == activeIndex)
            {
                row.AddToClassList("dim-stage-current");
            }
            if (done)
            {
                row.AddToClassList("dim-stage-done");
            }

            Label number = new Label(done ? "✓" : (index + 1).ToString());
            number.AddToClassList("dim-stage-num");
            row.Add(number);

            VisualElement labels = new VisualElement();
            labels.AddToClassList("dim-stage-labels");
            Label name = new Label(stage.Name);
            name.AddToClassList("dim-stage-name");
            labels.Add(name);
            row.Add(labels);

            if (state == DimensionAuthoringReadinessState.Blocked)
            {
                Label chip = new Label("!");
                chip.AddToClassList("dim-chip");
                chip.AddToClassList("dim-chip-blocked");
                row.Add(chip);
            }
            else if (state == DimensionAuthoringReadinessState.Partial)
            {
                Label chip = new Label("·");
                chip.AddToClassList("dim-chip");
                chip.AddToClassList("dim-chip-warn");
                row.Add(chip);
            }

            string sectionId = stage.SectionId;
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                GoToStage(sectionId);
                evt.StopPropagation();
            });
            return row;
        }

        private void GoToStage(string sectionId)
        {
            activeSectionId = sectionId;
            RefreshRail();
            RefreshStageHeader();
            RefreshStageBody();
            Repaint();
        }

        private void RefreshStageHeader()
        {
            if (stageTitleLabel == null)
            {
                return;
            }

            DimensionJourneyStage stage;
            if (!DimensionJourney.TryGetStage(activeSectionId, out stage))
            {
                stageTitleLabel.text = string.Equals(
                    activeSectionId,
                    DimensionJourney.DiagnosticsStageId,
                    System.StringComparison.Ordinal)
                        ? "Diagnostics"
                        : activeSectionId;
                return;
            }

            stageTitleLabel.text = stage.Name;
        }

        /// <summary>Shows the landing screen, or the journey when <paramref name="home"/> is false.</summary>
        private void ShowHome(bool home)
        {
            showingHome = home;
            if (homeScreen == null || journeyScreen == null)
            {
                return;
            }

            homeScreen.style.display = home ? DisplayStyle.Flex : DisplayStyle.None;
            journeyScreen.style.display = home ? DisplayStyle.None : DisplayStyle.Flex;
            if (home)
            {
                RefreshDimensionList();
                return;
            }

            if (DimensionJourney.IndexOf(activeSectionId) < 0)
            {
                activeSectionId = DimensionJourney.Stages[0].SectionId;
            }

            RefreshRail();
            RefreshStageHeader();
            RefreshStageBody();
        }

        /// <summary>Rebuilds the frame after the workspace behind it changed.</summary>
        private void RefreshShell()
        {
            if (showingHome)
            {
                RefreshDimensionList();
                return;
            }

            RefreshRail();
            RefreshStageHeader();
            RefreshStageBody();
        }
    }
}
