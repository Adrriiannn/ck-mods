using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The left hand rail: its items, its icons, and the maturity badge on a section.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        // The dashboard sections are colour-coded BLUE so a creator can tell "I'm in the section
        // list" at a peripheral glance — distinct from the orange Tileset Studio and (soon) the
        // blue-accented Portal Studio interior.
        private static readonly Color NavBlue = new Color(0.34f, 0.62f, 0.92f);

        private static readonly Color NavBlueSoft = new Color(0.34f, 0.62f, 0.92f, 0.14f);

        private static readonly Color NavIcon = new Color(0.5f, 0.74f, 1f);

        private GUIStyle navTitleStyle;

        private GUIStyle navTitleSelStyle;

        private GUIStyle navDescStyle;

        private void DrawNavigationSidebar()
        {
            EnsureNavStyles();
            // Width comes from the shared left column, so this vertical just fills it.
            EditorGUILayout.BeginVertical();
            GUILayout.Label("SECTIONS", EditorStyles.miniBoldLabel);
            GUILayout.Space(2f);

            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections =
                viewModel.Navigation == null ? null : viewModel.Navigation.Sections;
            if (sections != null)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    DrawNavItem(sections[i]);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNavItem(DimensionTemplateCustomizerSectionItem section)
        {
            bool selected = section.SectionId == activeSectionId;
            Rect r = EditorGUILayout.GetControlRect(false, 40f, GUILayout.Width(SidebarWidth - 18f));

            if (selected)
            {
                EditorGUI.DrawRect(r, NavBlueSoft);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), NavBlue);
            }
            else if (r.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.04f));
            }

            Rect icon = new Rect(r.x + 12f, r.y + 10f, 20f, 20f);
            DrawSectionIcon(icon, section.SectionId, selected ? new Color(0.66f, 0.84f, 1f) : NavIcon);

            float textX = icon.xMax + 12f;
            float textW = r.xMax - textX - 8f;
            GUI.Label(new Rect(textX, r.y + 5f, textW, 16f), section.DisplayName,
                selected ? navTitleSelStyle : navTitleStyle);
            GUI.Label(new Rect(textX, r.y + 21f, textW, 13f), SectionDescription(section.SectionId), navDescStyle);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                RequestSectionChange(section.SectionId);
            }
        }

        // Section icons: a real PNG dropped in Assets/ExpandNullforge/Editor/Icons/<id>.png wins
        // (drawn at full colour); otherwise a smooth code-generated icon is drawn, tinted to state.
        private static Dictionary<string, Texture2D> sectionIconPngCache;

        private static void DrawSectionIcon(Rect box, string sectionId, Color tint)
        {
            Texture2D png = SectionIconPng(sectionId);
            if (png != null)
            {
                GUI.DrawTexture(box, png, ScaleMode.ScaleToFit);
                return;
            }

            Texture2D generated = DimensionSectionIcons.Generated(sectionId);
            if (generated != null)
            {
                GUI.DrawTexture(box, generated, ScaleMode.ScaleToFit, true, 0f, tint, Vector4.zero, Vector4.zero);
            }
        }

        private static Texture2D SectionIconPng(string sectionId)
        {
            if (sectionIconPngCache == null)
            {
                sectionIconPngCache = new Dictionary<string, Texture2D>();
            }

            if (!sectionIconPngCache.TryGetValue(sectionId, out Texture2D tex))
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ExpandNullforge/Editor/Icons/" + sectionId + ".png");
                sectionIconPngCache[sectionId] = tex;
            }

            return tex;
        }

        private static Color StateColor(DimensionAuthoringReadinessState state)
        {
            if (state == DimensionAuthoringReadinessState.Ready)
            {
                return new Color(0.75f, 1f, 0.75f, 1f);
            }

            if (state == DimensionAuthoringReadinessState.Partial)
            {
                return new Color(1f, 0.92f, 0.62f, 1f);
            }

            if (state == DimensionAuthoringReadinessState.Blocked)
            {
                return new Color(1f, 0.65f, 0.65f, 1f);
            }

            return new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        private static string SectionDescription(string sectionId)
        {
            switch (sectionId)
            {
                case "overview": return "Everything at a glance";
                case "dimension": return "Identity & coordinates";
                case "portals": return "Design your portals";
                case "tilesets": return "Custom blocks & tiles";
                case "layout": return "Shape of the world";
                case "biomes": return "Zones & palettes";
                case "terrain": return "Ground, walls, liquids";
                case "generation": return "How the world builds";
                case "scenes": return "Handcrafted structures";
                case "resources": return "Items, recipes, loot";
                case "spawns": return "Creatures & mobs";
                case "export": return "Build the manifest";
                case "diagnostics": return "Readiness & issues";
                default: return string.Empty;
            }
        }

        private void EnsureNavStyles()
        {
            if (navTitleStyle != null)
            {
                return;
            }

            navTitleStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, fontSize = 13, alignment = TextAnchor.MiddleLeft };
            navTitleSelStyle = new GUIStyle(navTitleStyle);
            navTitleSelStyle.normal.textColor = new Color(0.72f, 0.85f, 1f);
            navDescStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            navDescStyle.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
        }

        private void DrawCurrentSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            DrawSectionMaturityBadge(activeSectionId);

            if (activeSectionId == "dimension")
            {
                DrawDimensionEditor();
            }
            else if (activeSectionId == "portals")
            {
                DrawPortalEditor();
            }
            else if (activeSectionId == "tilesets")
            {
                DrawTilesetsEditor();
            }
            else if (activeSectionId == "layout")
            {
                DrawLayoutEditor();
            }
            else if (activeSectionId == "biomes")
            {
                DrawBiomeEditor();
            }
            else if (activeSectionId == "terrain")
            {
                DrawTerrainEditor();
            }
            else if (activeSectionId == "generation")
            {
                DrawGenerationEditor();
            }
            else if (activeSectionId == "scenes")
            {
                DrawScenesEditor();
            }
            else if (activeSectionId == "resources")
            {
                DrawResourcesEditor();
            }
            else if (activeSectionId == "spawns")
            {
                DrawSpawnsEditor();
            }
            else if (activeSectionId == "export")
            {
                DrawExportEditor();
            }
            else if (activeSectionId == "diagnostics")
            {
                DrawDiagnosticsEditor();
            }
            else
            {
                DrawOverviewEditor();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Shows an honest maturity label for the active section, read from the capability
        /// registry, so a creator can see at a glance whether the controls below are proven,
        /// preview-only, or experimental before investing time in them.
        /// </summary>
        private void DrawSectionMaturityBadge(string sectionId)
        {
            string capabilityId = MaturityCapabilityForSection(sectionId);
            if (string.IsNullOrEmpty(capabilityId) ||
                !DimensionCapabilityRegistry.TryGet(
                    capabilityId,
                    out DimensionCapability capability))
            {
                return;
            }

            Color previousColor = GUI.color;
            GUI.color = MaturityColor(capability.Maturity);
            EditorGUILayout.LabelField(
                new GUIContent(
                    "● " + DimensionCapabilityRegistry.Describe(capability.Maturity),
                    capability.Note),
                EditorStyles.miniBoldLabel);
            GUI.color = previousColor;
            GUILayout.Space(4f);
        }

        /// <summary>
        /// One map, shared with the live shell. Two copies of "which capability is this section"
        /// is two answers waiting to disagree, and the one a creator reads would be whichever
        /// panel they happened to open.
        /// </summary>
        private static string MaturityCapabilityForSection(string sectionId)
        {
            return DimensionStageMaturity.CapabilityForSection(sectionId);
        }

        private static Color MaturityColor(DimensionCapabilityMaturity maturity)
        {
            switch (maturity)
            {
                case DimensionCapabilityMaturity.ImplementedAndEvidenced:
                    return new Color(0.45f, 1.0f, 0.55f);
                case DimensionCapabilityMaturity.ImplementedNotFullyProven:
                    return new Color(0.6f, 0.9f, 1.0f);
                case DimensionCapabilityMaturity.PartialVerticalSlice:
                    return new Color(1.0f, 0.92f, 0.5f);
                case DimensionCapabilityMaturity.ContractExtensionSeam:
                    return new Color(1.0f, 0.85f, 0.5f);
                case DimensionCapabilityMaturity.AuthoringModelOnly:
                    return new Color(1.0f, 0.7f, 0.4f);
                case DimensionCapabilityMaturity.ExperimentalUnstable:
                    return new Color(1.0f, 0.55f, 0.45f);
                case DimensionCapabilityMaturity.NotImplemented:
                    return new Color(0.72f, 0.72f, 0.72f);
                default:
                    return Color.white;
            }
        }
    }
}
