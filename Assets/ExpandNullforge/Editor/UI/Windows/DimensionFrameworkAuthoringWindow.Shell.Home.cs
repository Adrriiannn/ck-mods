using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The home screen: the dimensions on disk, and the decorations behind them.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        // ------------------------------------------------------------------ home ---

        private VisualElement BuildHomeScreen()
        {
            // No scroller: the landing is a poster, not a document — it fills the window and
            // holds still. Its content centres itself, and the sign-off line sits at the bottom
            // edge rather than as an eyebrow above.
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
            // No counter-glow from the top edge, by request: one light source, from below, and
            // it is the Core's blue.
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
    }
}
