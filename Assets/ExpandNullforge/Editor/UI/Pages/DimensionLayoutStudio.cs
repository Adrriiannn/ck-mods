using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The Layout Studio: a map of the dimension you can edit by dragging, showing the biome shape the
    /// game will actually generate, next to the version machinery that keeps existing saves intact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY DRAG AND NOT TYPE. A layout is a picture. Expressed as a list of numbered rectangles it is
    /// nearly impossible to hold in your head — the earlier authoring surface was exactly that, and
    /// "which of these seven regions is the one at the top-left" was a question you could only answer
    /// by editing a number and rebuilding. Here the answer is: the one at the top-left.
    /// </para>
    /// <para>
    /// WHY THE PREVIEW IS COMPILED, NOT DRAWN. The canvas renders the output of
    /// <see cref="DimensionTemplateCompiler"/>, the same code the build runs, rather than a second
    /// drawing routine that interprets rings and grids its own way. A preview with its own idea of the
    /// rules is worse than no preview, because it is convincing.
    /// </para>
    /// </remarks>
    internal sealed class DimensionLayoutStudio
    {
        internal struct DrawResult
        {
            public bool Changed;
            public string Message;
            public MessageType MessageType;
            public bool PublishRequested;
            public bool AddRingRequested;
            public bool AddRegionRequested;
            public string RemoveEntryId;
        }

        private const float CanvasHeight = 460f;
        private const float HandleGrab = 7f;

        private static readonly Color Ink = new Color(0.07f, 0.09f, 0.11f, 1f);
        private static readonly Color Amber = new Color(0.90f, 0.58f, 0.26f);
        private static readonly Color GridLine = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color AxisLine = new Color(1f, 1f, 1f, 0.18f);

        private GUIStyle headerTitle;
        private GUIStyle panelTitle;
        private GUIStyle sub;

        private DrawResult result;

        /// <summary>Which ring or region the pointer is currently dragging, and by which handle.</summary>
        private string dragEntryId;
        private DragHandle dragHandle;

        private int selectedEntry = -1;

        private enum DragHandle
        {
            None = 0,
            InnerRadius,
            OuterRadius,
            RectMin,
            RectMax,
            RectWhole
        }

        public DrawResult Draw(
            DimensionTemplateAsset template,
            DimensionLayoutTemplateAsset layout,
            IReadOnlyList<DimensionCompiledBiomeRegion> compiledRegions,
            DimensionBounds playableBounds)
        {
            result = default(DrawResult);
            result.MessageType = MessageType.Info;
            EnsureStyles();

            if (layout == null)
            {
                return result;
            }

            SerializedObject so = new SerializedObject(layout);
            so.UpdateIfRequiredOrScript();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawHeaderBar(layout);
            GUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            DrawCanvasColumn(so, layout, compiledRegions, playableBounds);
            GUILayout.Space(8f);
            DrawSideColumn(so, template, layout);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            if (so.ApplyModifiedProperties())
            {
                result.Changed = true;
            }

            return result;
        }

        // ---- header ----

        private void DrawHeaderBar(DimensionLayoutTemplateAsset layout)
        {
            Rect bar = EditorGUILayout.GetControlRect(false, 40f);
            EditorGUI.DrawRect(bar, new Color(Amber.r, Amber.g, Amber.b, 0.15f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, 3f, bar.height), Amber);

            GUI.Label(new Rect(bar.x + 12f, bar.y, 260f, bar.height), "Layout Studio", headerTitle);

            string state = layout.HasUnpublishedChanges
                ? "v" + layout.LayoutVersion + " · edited since publishing"
                : "v" + layout.LayoutVersion + " · published";
            GUI.Label(new Rect(bar.x + 170f, bar.y, 420f, bar.height), state, sub);
        }

        // ---- canvas ----

        private void DrawCanvasColumn(
            SerializedObject so,
            DimensionLayoutTemplateAsset layout,
            IReadOnlyList<DimensionCompiledBiomeRegion> compiledRegions,
            DimensionBounds playableBounds)
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            Rect canvas = GUILayoutUtility.GetRect(
                420f,
                CanvasHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.MinHeight(CanvasHeight));

            EditorGUI.DrawRect(canvas, Ink);

            DimensionBounds view = ResolveViewBounds(playableBounds, layout);
            if (view.MaxExclusive.x <= view.Min.x || view.MaxExclusive.y <= view.Min.y)
            {
                GUI.Label(canvas, "This layout has no area yet.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawCanvasGrid(canvas, view);
            DrawCompiledRegions(canvas, view, compiledRegions);

            if (layout.LayoutKind == DimensionLayoutKind.RadialRings ||
                layout.LayoutKind == DimensionLayoutKind.Hybrid)
            {
                DrawRingHandles(so, canvas, view, layout);
            }

            if (layout.LayoutKind == DimensionLayoutKind.ManualRegions ||
                layout.LayoutKind == DimensionLayoutKind.Hybrid ||
                layout.LayoutKind == DimensionLayoutKind.GridRegions)
            {
                DrawRegionHandles(so, canvas, view, layout);
            }

            DrawCanvasLegend(canvas, view);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// The area the canvas shows.
        /// </summary>
        /// <remarks>
        /// Widened past the dimension's own bounds when a radial ring reaches further, so a ring
        /// dragged beyond the edge stays visible instead of vanishing off-canvas with no way to drag it
        /// back. A layout that overshoots its bounds is a real mistake worth seeing, not hiding.
        /// </remarks>
        private static DimensionBounds ResolveViewBounds(
            DimensionBounds playableBounds,
            DimensionLayoutTemplateAsset layout)
        {
            int extent = Mathf.Max(
                Mathf.Abs(playableBounds.Min.x),
                Mathf.Abs(playableBounds.Min.y),
                Mathf.Abs(playableBounds.MaxExclusive.x),
                Mathf.Abs(playableBounds.MaxExclusive.y));

            DimensionLayoutRadialRingDefinition[] rings = layout.RadialRings;
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] != null && rings[i].Enabled)
                {
                    extent = Mathf.Max(extent, rings[i].MaxRadiusTiles);
                }
            }

            if (extent <= 0)
            {
                extent = 128;
            }

            // A little air around the content so an outermost edge is grabbable rather than pinned to
            // the canvas border.
            extent = Mathf.CeilToInt(extent * 1.08f);
            return new DimensionBounds(
                new Unity.Mathematics.int2(-extent, -extent),
                new Unity.Mathematics.int2(extent, extent));
        }

        private void DrawCanvasGrid(Rect canvas, DimensionBounds view)
        {
            // Handles draw into the GUI's own repaint pass; issuing them during Layout is wasted work
            // at best and an error at worst. Hit-testing deliberately stays outside this guard, since
            // that is exactly what has to run on the mouse events Repaint never sees.
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Color old = Handles.color;
            Handles.color = GridLine;
            for (int i = 1; i < 8; i++)
            {
                float x = Mathf.Lerp(canvas.xMin, canvas.xMax, i / 8f);
                float y = Mathf.Lerp(canvas.yMin, canvas.yMax, i / 8f);
                Handles.DrawLine(new Vector3(x, canvas.yMin), new Vector3(x, canvas.yMax));
                Handles.DrawLine(new Vector3(canvas.xMin, y), new Vector3(canvas.xMax, y));
            }

            // Local 0,0 is where every radius is measured from and where players arrive, so it is drawn
            // brighter than the rest of the grid rather than left to be counted out.
            Vector2 origin = ToCanvas(canvas, view, Vector2.zero);
            Handles.color = AxisLine;
            Handles.DrawLine(new Vector3(origin.x, canvas.yMin), new Vector3(origin.x, canvas.yMax));
            Handles.DrawLine(new Vector3(canvas.xMin, origin.y), new Vector3(canvas.xMax, origin.y));
            Handles.color = old;
        }

        private void DrawCompiledRegions(
            Rect canvas,
            DimensionBounds view,
            IReadOnlyList<DimensionCompiledBiomeRegion> compiledRegions)
        {
            if (compiledRegions == null)
            {
                return;
            }

            // Lowest priority first, so the region that wins in game is the one drawn on top here.
            List<DimensionCompiledBiomeRegion> ordered = new List<DimensionCompiledBiomeRegion>(compiledRegions);
            ordered.Sort(delegate (DimensionCompiledBiomeRegion a, DimensionCompiledBiomeRegion b)
            {
                return a.Priority.CompareTo(b.Priority);
            });

            for (int i = 0; i < ordered.Count; i++)
            {
                DimensionCompiledBiomeRegion region = ordered[i];
                Rect rect = ToCanvasRect(canvas, view, region.LocalBounds);
                if (rect.width < 0.5f || rect.height < 0.5f)
                {
                    continue;
                }

                Color fill = BiomeColor(region.BiomeId);
                EditorGUI.DrawRect(rect, new Color(fill.r, fill.g, fill.b, 0.55f));
            }
        }

        private void DrawCanvasLegend(Rect canvas, DimensionBounds view)
        {
            string span = (view.MaxExclusive.x - view.Min.x) + " x " +
                (view.MaxExclusive.y - view.Min.y) + " tiles";
            GUI.Label(
                new Rect(canvas.xMin + 6f, canvas.yMax - 18f, 320f, 16f),
                span + "   ·   drag the round handles to resize",
                EditorStyles.centeredGreyMiniLabel);
        }

        // ---- radial handles ----

        private void DrawRingHandles(
            SerializedObject so,
            Rect canvas,
            DimensionBounds view,
            DimensionLayoutTemplateAsset layout)
        {
            SerializedProperty ringsProp = so.FindProperty("radialRings");
            if (ringsProp == null)
            {
                return;
            }

            Vector2 origin = ToCanvas(canvas, view, Vector2.zero);
            float scale = CanvasScale(canvas, view);

            for (int i = 0; i < ringsProp.arraySize; i++)
            {
                SerializedProperty ring = ringsProp.GetArrayElementAtIndex(i);
                SerializedProperty enabledProp = ring.FindPropertyRelative("enabled");
                if (enabledProp != null && !enabledProp.boolValue)
                {
                    continue;
                }

                SerializedProperty minProp = ring.FindPropertyRelative("minRadiusTiles");
                SerializedProperty maxProp = ring.FindPropertyRelative("maxRadiusTiles");
                SerializedProperty idProp = ring.FindPropertyRelative("ringId");
                SerializedProperty biomeProp = ring.FindPropertyRelative("biomeId");
                if (minProp == null || maxProp == null)
                {
                    continue;
                }

                string entryId = "ring:" + i;
                Color color = BiomeColor(biomeProp == null ? string.Empty : biomeProp.stringValue);

                if (Event.current.type == EventType.Repaint)
                {
                    // Drawn as true circles, not as the banded rectangles the compiler turns them
                    // into. The bands are an implementation detail of writing a ring into a tile grid;
                    // the ring is what the author is actually shaping.
                    Handles.color = new Color(color.r, color.g, color.b, 0.85f);
                    if (minProp.intValue > 0)
                    {
                        Handles.DrawWireDisc(origin, Vector3.forward, minProp.intValue * scale);
                    }

                    Handles.DrawWireDisc(origin, Vector3.forward, maxProp.intValue * scale);

                    if (selectedEntry == i)
                    {
                        GUI.Label(
                            new Rect(origin.x + 4f, origin.y - maxProp.intValue * scale - 14f, 220f, 14f),
                            idProp == null ? "ring" : idProp.stringValue,
                            EditorStyles.whiteMiniLabel);
                    }
                }

                // Handles sit on the +X axis: one place to look, and they never land under each other
                // the way handles placed by angle would when two rings share a radius.
                Vector2 innerHandle = new Vector2(origin.x + minProp.intValue * scale, origin.y);
                Vector2 outerHandle = new Vector2(origin.x + maxProp.intValue * scale, origin.y);

                if (minProp.intValue > 0)
                {
                    DrawHandleDot(innerHandle, color);
                }

                DrawHandleDot(outerHandle, color);

                HandleRadiusDrag(canvas, origin, scale, entryId, DragHandle.InnerRadius, innerHandle, minProp, maxProp, i);
                HandleRadiusDrag(canvas, origin, scale, entryId, DragHandle.OuterRadius, outerHandle, minProp, maxProp, i);
            }
        }

        /// <summary>
        /// Turns a drag on a radius handle into a new radius, clamped so a ring cannot invert.
        /// </summary>
        /// <remarks>
        /// The clamp matters more than it looks: a ring whose inner radius passes its outer one compiles
        /// into no regions at all, so without it a moment's overshoot makes a biome silently disappear
        /// from the world rather than simply refusing to shrink further.
        /// </remarks>
        private void HandleRadiusDrag(
            Rect canvas,
            Vector2 origin,
            float scale,
            string entryId,
            DragHandle handle,
            Vector2 handlePosition,
            SerializedProperty minProp,
            SerializedProperty maxProp,
            int index)
        {
            Event e = Event.current;
            Rect grab = new Rect(
                handlePosition.x - HandleGrab,
                handlePosition.y - HandleGrab,
                HandleGrab * 2f,
                HandleGrab * 2f);

            EditorGUIUtility.AddCursorRect(grab, MouseCursor.ResizeHorizontal);

            if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(e.mousePosition))
            {
                dragEntryId = entryId;
                dragHandle = handle;
                selectedEntry = index;
                e.Use();
                return;
            }

            bool isDragging = dragEntryId == entryId && dragHandle == handle;
            if (!isDragging)
            {
                return;
            }

            if (e.type == EventType.MouseDrag)
            {
                float radius = Mathf.Max(0f, (e.mousePosition.x - origin.x) / Mathf.Max(scale, 0.0001f));
                int rounded = Mathf.RoundToInt(radius);

                if (handle == DragHandle.InnerRadius)
                {
                    minProp.intValue = Mathf.Clamp(rounded, 0, Mathf.Max(0, maxProp.intValue - 1));
                }
                else
                {
                    maxProp.intValue = Mathf.Max(rounded, minProp.intValue + 1);
                }

                e.Use();
                GUI.changed = true;
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                dragEntryId = null;
                dragHandle = DragHandle.None;
                e.Use();
            }
        }

        // ---- rectangle handles ----

        private void DrawRegionHandles(
            SerializedObject so,
            Rect canvas,
            DimensionBounds view,
            DimensionLayoutTemplateAsset layout)
        {
            SerializedProperty regionsProp = so.FindProperty("regions");
            if (regionsProp == null)
            {
                return;
            }

            for (int i = 0; i < regionsProp.arraySize; i++)
            {
                SerializedProperty region = regionsProp.GetArrayElementAtIndex(i);
                SerializedProperty enabledProp = region.FindPropertyRelative("enabled");
                if (enabledProp != null && !enabledProp.boolValue)
                {
                    continue;
                }

                SerializedProperty minProp = region.FindPropertyRelative("localMin");
                SerializedProperty maxProp = region.FindPropertyRelative("localMaxExclusive");
                SerializedProperty biomeProp = region.FindPropertyRelative("biomeId");
                if (minProp == null || maxProp == null)
                {
                    continue;
                }

                Vector2Int min = minProp.vector2IntValue;
                Vector2Int max = maxProp.vector2IntValue;
                Color color = BiomeColor(biomeProp == null ? string.Empty : biomeProp.stringValue);

                Rect rect = ToCanvasRect(
                    canvas,
                    view,
                    new DimensionBounds(
                        new Unity.Mathematics.int2(min.x, min.y),
                        new Unity.Mathematics.int2(max.x, max.y)));

                DrawRectOutline(rect, new Color(color.r, color.g, color.b, 0.95f), selectedEntry == i ? 2f : 1f);

                Vector2 minHandle = new Vector2(rect.xMin, rect.yMax);
                Vector2 maxHandle = new Vector2(rect.xMax, rect.yMin);
                DrawHandleDot(minHandle, color);
                DrawHandleDot(maxHandle, color);

                HandleRectDrag(canvas, view, "region:" + i, DragHandle.RectMin, minHandle, minProp, maxProp, i);
                HandleRectDrag(canvas, view, "region:" + i, DragHandle.RectMax, maxHandle, minProp, maxProp, i);
            }
        }

        private void HandleRectDrag(
            Rect canvas,
            DimensionBounds view,
            string entryId,
            DragHandle handle,
            Vector2 handlePosition,
            SerializedProperty minProp,
            SerializedProperty maxProp,
            int index)
        {
            Event e = Event.current;
            Rect grab = new Rect(
                handlePosition.x - HandleGrab,
                handlePosition.y - HandleGrab,
                HandleGrab * 2f,
                HandleGrab * 2f);

            EditorGUIUtility.AddCursorRect(
                grab,
                handle == DragHandle.RectMin ? MouseCursor.ResizeUpRight : MouseCursor.ResizeUpLeft);

            if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(e.mousePosition))
            {
                dragEntryId = entryId;
                dragHandle = handle;
                selectedEntry = index;
                e.Use();
                return;
            }

            if (dragEntryId != entryId || dragHandle != handle)
            {
                return;
            }

            if (e.type == EventType.MouseDrag)
            {
                Vector2Int tile = ToTile(canvas, view, e.mousePosition);
                Vector2Int min = minProp.vector2IntValue;
                Vector2Int max = maxProp.vector2IntValue;

                // The max corner is exclusive, so a region is only real when max exceeds min on both
                // axes. Clamping here rather than at compile time keeps the drag from producing a
                // region the compiler would just reject with an error the author has to go read.
                if (handle == DragHandle.RectMin)
                {
                    minProp.vector2IntValue = new Vector2Int(
                        Mathf.Min(tile.x, max.x - 1),
                        Mathf.Min(tile.y, max.y - 1));
                }
                else
                {
                    maxProp.vector2IntValue = new Vector2Int(
                        Mathf.Max(tile.x, min.x + 1),
                        Mathf.Max(tile.y, min.y + 1));
                }

                e.Use();
                GUI.changed = true;
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                dragEntryId = null;
                dragHandle = DragHandle.None;
                e.Use();
            }
        }

        // ---- side column ----

        private void DrawSideColumn(
            SerializedObject so,
            DimensionTemplateAsset template,
            DimensionLayoutTemplateAsset layout)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(320f));

            DrawVersionPanel(so, layout);
            GUILayout.Space(8f);
            DrawShapePanel(so, template, layout);

            EditorGUILayout.EndVertical();
        }

        private void DrawVersionPanel(SerializedObject so, DimensionLayoutTemplateAsset layout)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Versions", panelTitle);

            EditorGUILayout.LabelField("Current", "v" + layout.LayoutVersion);
            EditorGUILayout.LabelField("Shape code", layout.CurrentFingerprint);

            SerializedProperty policy = so.FindProperty("driftPolicy");
            if (policy != null)
            {
                EditorGUILayout.PropertyField(policy, new GUIContent("Existing saves"));
            }

            if (layout.HasUnpublishedChanges)
            {
                EditorGUILayout.HelpBox(
                    "This layout has changed since v" + layout.LayoutVersion + " was published. " +
                    "Publish before you build, or worlds made from this build will disagree with " +
                    "worlds made from the last one.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Publish this layout as a new version", GUILayout.Height(26f)))
            {
                result.PublishRequested = true;
            }

            DimensionLayoutArchiveEntry[] published = layout.PublishedVersions;
            if (published.Length == 0)
            {
                EditorGUILayout.LabelField(
                    "Nothing published yet. Until you publish, a save cannot be pinned to the layout that made it.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                for (int i = published.Length - 1; i >= 0; i--)
                {
                    DimensionLayoutArchiveEntry entry = published[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    EditorGUILayout.LabelField(
                        "v" + entry.Version + "  ·  " + entry.Fingerprint + "  ·  " +
                        entry.Regions.Length + " regions",
                        EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawShapePanel(
            SerializedObject so,
            DimensionTemplateAsset template,
            DimensionLayoutTemplateAsset layout)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Shape", panelTitle);

            SerializedProperty kind = so.FindProperty("layoutKind");
            if (kind != null)
            {
                EditorGUILayout.PropertyField(kind, new GUIContent("Mode"));
            }

            bool radial = layout.LayoutKind == DimensionLayoutKind.RadialRings ||
                layout.LayoutKind == DimensionLayoutKind.Hybrid;

            if (radial)
            {
                // "Band size" is deliberately not edited here. The compiler reads it, hands it to
                // BuildRadialRingBands and the body of that method never mentions it: the scanline
                // walk writes an exact circle. The stored value stays on the asset, because every
                // pinned layout fingerprint includes it.
                EditorGUILayout.LabelField(
                    "Core Keeper biomes are rings around the centre. Each ring is written into the " +
                    "world exactly as a circle.",
                    EditorStyles.wordWrappedMiniLabel);

                if (GUILayout.Button("Add ring"))
                {
                    result.AddRingRequested = true;
                }

                DrawRingList(so, template, layout);
            }

            if (layout.LayoutKind == DimensionLayoutKind.ManualRegions ||
                layout.LayoutKind == DimensionLayoutKind.Hybrid ||
                layout.LayoutKind == DimensionLayoutKind.GridRegions)
            {
                if (GUILayout.Button("Add rectangle"))
                {
                    result.AddRegionRequested = true;
                }

                DrawRegionList(so, template, layout);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRingList(
            SerializedObject so,
            DimensionTemplateAsset template,
            DimensionLayoutTemplateAsset layout)
        {
            SerializedProperty rings = so.FindProperty("radialRings");
            if (rings == null)
            {
                return;
            }

            string[] biomeIds = CollectBiomeIds(template);

            for (int i = 0; i < rings.arraySize; i++)
            {
                SerializedProperty ring = rings.GetArrayElementAtIndex(i);
                bool selected = selectedEntry == i;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                SerializedProperty biome = ring.FindPropertyRelative("biomeId");
                DrawBiomeSwatch(biome == null ? string.Empty : biome.stringValue);
                DrawBiomeDropdown(biome, biomeIds);

                if (GUILayout.Button(selected ? "•" : "○", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    selectedEntry = selected ? -1 : i;
                }

                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    result.RemoveEntryId = "ring:" + i;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawCompactInt(ring.FindPropertyRelative("minRadiusTiles"), "from");
                DrawCompactInt(ring.FindPropertyRelative("maxRadiusTiles"), "to");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawRegionList(
            SerializedObject so,
            DimensionTemplateAsset template,
            DimensionLayoutTemplateAsset layout)
        {
            SerializedProperty regions = so.FindProperty("regions");
            if (regions == null)
            {
                return;
            }

            string[] biomeIds = CollectBiomeIds(template);

            for (int i = 0; i < regions.arraySize; i++)
            {
                SerializedProperty region = regions.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                SerializedProperty biome = region.FindPropertyRelative("biomeId");
                DrawBiomeSwatch(biome == null ? string.Empty : biome.stringValue);
                DrawBiomeDropdown(biome, biomeIds);

                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    result.RemoveEntryId = "region:" + i;
                }

                EditorGUILayout.EndHorizontal();

                SerializedProperty min = region.FindPropertyRelative("localMin");
                SerializedProperty max = region.FindPropertyRelative("localMaxExclusive");
                if (min != null && max != null)
                {
                    EditorGUILayout.LabelField(
                        min.vector2IntValue.x + "," + min.vector2IntValue.y + "  →  " +
                        max.vector2IntValue.x + "," + max.vector2IntValue.y,
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }
        }

        /// <summary>
        /// A biome picker that still shows an id the dimension no longer defines.
        /// </summary>
        /// <remarks>
        /// A plain popup over the known ids would silently rewrite a ring that points at a deleted or
        /// renamed biome to whatever happens to sit at index zero — turning a mistake the author could
        /// have fixed into a different world with no warning. The dangling id stays visible instead.
        /// </remarks>
        private void DrawBiomeDropdown(SerializedProperty biomeProp, string[] biomeIds)
        {
            if (biomeProp == null)
            {
                return;
            }

            string current = biomeProp.stringValue ?? string.Empty;
            List<string> options = new List<string>(biomeIds);
            int index = options.IndexOf(current);
            if (index < 0)
            {
                options.Insert(0, string.IsNullOrEmpty(current) ? "(none)" : current + "  (missing)");
                index = 0;
            }

            int chosen = EditorGUILayout.Popup(index, options.ToArray());
            if (chosen != index && chosen >= 0 && chosen < options.Count)
            {
                string picked = options[chosen];
                if (!picked.EndsWith("  (missing)", StringComparison.Ordinal) && picked != "(none)")
                {
                    biomeProp.stringValue = picked;
                }
            }
        }

        private void DrawBiomeSwatch(string biomeId)
        {
            Rect swatch = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f), GUILayout.Height(14f));
            swatch.y += 2f;
            EditorGUI.DrawRect(swatch, BiomeColor(biomeId));
        }

        private static void DrawCompactInt(SerializedProperty prop, string label)
        {
            if (prop == null)
            {
                return;
            }

            EditorGUILayout.LabelField(label, GUILayout.Width(32f));
            prop.intValue = EditorGUILayout.IntField(prop.intValue, GUILayout.Width(58f));
        }

        // ---- shared helpers ----

        private static string[] CollectBiomeIds(DimensionTemplateAsset template)
        {
            List<string> ids = new List<string>();
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes == null)
            {
                return ids.ToArray();
            }

            for (int i = 0; i < biomes.Length; i++)
            {
                if (biomes[i] != null && !string.IsNullOrEmpty(biomes[i].BiomeId))
                {
                    ids.Add(biomes[i].BiomeId);
                }
            }

            return ids.ToArray();
        }

        /// <summary>
        /// A stable colour for a biome id.
        /// </summary>
        /// <remarks>
        /// Derived from the id rather than assigned by index so a biome keeps its colour when another is
        /// added above it — otherwise the whole map re-colours on every insertion and the author has to
        /// re-learn the picture. Saturation and value are pinned so no biome comes out near-black on the
        /// dark canvas.
        /// </remarks>
        private static Color BiomeColor(string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId))
            {
                return new Color(0.35f, 0.37f, 0.40f);
            }

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < biomeId.Length; i++)
                {
                    hash ^= biomeId[i];
                    hash *= 16777619u;
                }

                float hue = (hash % 360u) / 360f;
                return Color.HSVToRGB(hue, 0.55f, 0.85f);
            }
        }

        private static float CanvasScale(Rect canvas, DimensionBounds view)
        {
            float width = view.MaxExclusive.x - view.Min.x;
            float height = view.MaxExclusive.y - view.Min.y;
            if (width <= 0f || height <= 0f)
            {
                return 1f;
            }

            return Mathf.Min(canvas.width / width, canvas.height / height);
        }

        private static Vector2 ToCanvas(Rect canvas, DimensionBounds view, Vector2 tile)
        {
            float scale = CanvasScale(canvas, view);
            float centerX = (view.Min.x + view.MaxExclusive.x) * 0.5f;
            float centerY = (view.Min.y + view.MaxExclusive.y) * 0.5f;
            return new Vector2(
                canvas.center.x + (tile.x - centerX) * scale,
                canvas.center.y - (tile.y - centerY) * scale);
        }

        private static Vector2Int ToTile(Rect canvas, DimensionBounds view, Vector2 point)
        {
            float scale = Mathf.Max(CanvasScale(canvas, view), 0.0001f);
            float centerX = (view.Min.x + view.MaxExclusive.x) * 0.5f;
            float centerY = (view.Min.y + view.MaxExclusive.y) * 0.5f;
            return new Vector2Int(
                Mathf.RoundToInt(centerX + (point.x - canvas.center.x) / scale),
                Mathf.RoundToInt(centerY - (point.y - canvas.center.y) / scale));
        }

        private static Rect ToCanvasRect(Rect canvas, DimensionBounds view, DimensionBounds bounds)
        {
            Vector2 min = ToCanvas(canvas, view, new Vector2(bounds.Min.x, bounds.Min.y));
            Vector2 max = ToCanvas(canvas, view, new Vector2(bounds.MaxExclusive.x, bounds.MaxExclusive.y));
            return Rect.MinMaxRect(
                Mathf.Min(min.x, max.x),
                Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y));
        }

        private static void DrawHandleDot(Vector2 position, Color color)
        {
            Rect dot = new Rect(position.x - 4f, position.y - 4f, 8f, 8f);
            EditorGUI.DrawRect(dot, Color.white);
            EditorGUI.DrawRect(new Rect(dot.x + 1f, dot.y + 1f, dot.width - 2f, dot.height - 2f), color);
        }

        private static void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        private void EnsureStyles()
        {
            if (headerTitle != null)
            {
                return;
            }

            headerTitle = new GUIStyle(EditorStyles.boldLabel);
            headerTitle.fontSize = 14;
            headerTitle.alignment = TextAnchor.MiddleLeft;

            panelTitle = new GUIStyle(EditorStyles.boldLabel);
            panelTitle.fontSize = 12;

            sub = new GUIStyle(EditorStyles.miniLabel);
            sub.alignment = TextAnchor.MiddleRight;
            sub.padding = new RectOffset(0, 10, 0, 0);
        }
    }
}
