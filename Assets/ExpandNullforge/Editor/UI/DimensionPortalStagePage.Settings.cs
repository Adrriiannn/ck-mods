using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The settings beside the canvas: looks, placement, behaviour, shadow, palettes.
    /// </summary>
    internal sealed partial class DimensionPortalStagePage
    {
        private void RebuildSettings()
        {
            if (settingsHost == null)
            {
                return;
            }

            DetachEditNotificationsDuringRebuild();
            settingsHost.Clear();
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null || template == null)
            {
                settingsHost.Add(DimensionsApiControls.EmptyState(
                    "No dimension open",
                    "Open a dimension from Home and its portal appears here.",
                    null));
                return;
            }

            if (profile == null)
            {
                settingsHost.Add(DimensionsApiControls.EmptyState(
                    "This dimension has no portal yet",
                    "A portal is the way in. The framework prepares one that looks like the game's own, and everything about it can then be changed here.",
                    null));
                return;
            }

            SerializedObject serialized = studio.GetProfileSerializedObject(profile);
            if (serialized == null)
            {
                return;
            }

            DimensionPortalAppearanceStudio.StudioLayer layer = studio.SelectedLayer;
            settingsHost.Add(BuildLooksGroup(studio, serialized, layer));

            VisualElement placement = BuildPlacementGroup(studio, serialized, layer);
            if (placement != null)
            {
                settingsHost.Add(placement);
            }

            VisualElement behaviour = BuildBehaviourGroup(studio, serialized, layer);
            if (behaviour != null)
            {
                settingsHost.Add(behaviour);
            }

            if (layer == DimensionPortalAppearanceStudio.StudioLayer.GroundLight)
            {
                settingsHost.Add(BuildShadowGroup(serialized));
            }

            // Portal-wide settings live on the dimension, not on the look, so they sit under
            // the layer cards and never change when a different layer is picked. Both cards
            // were only reachable from the legacy panels before this.
            SerializedObject serializedTemplate = studio.GetTemplateSerializedObject(template);
            if (serializedTemplate != null)
            {
                settingsHost.Add(BuildSoundCard(studio, serializedTemplate));
            }

            AddAccessCards(studio);
        }

        private VisualElement BuildLooksGroup(
            DimensionPortalAppearanceStudio studio,
            SerializedObject serialized,
            DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            VisualElement group = DimensionsApiControls.Group("Appearance", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            switch (layer)
            {
                case DimensionPortalAppearanceStudio.StudioLayer.Frame:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "frameVisible",
                        "Visible",
                        "Turn this off for a portal with nothing around its middle, the way the one an item opens is drawn."));
                    body.Add(Tint(serialized, "frameTint", "Tint", "A colour laid over the frame artwork. Leave it white to keep the art exactly as it was drawn."));
                    body.Add(Glow(serialized, "frameEmissiveColor", "Glow", "How brightly the frame shines in a dark cave. Brighter than white makes it give off light of its own."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "chargeWaveVisible",
                        "Visible",
                        "The band of light that travels around the portal while it charges up."));
                    AddPalette(
                        body,
                        studio,
                        serialized,
                        layer,
                        new[] { "chargeWaveDarkColor", "chargeWaveDeepColor", "chargeWaveMidColor", "chargeWaveBrightColor", "chargeWaveCoreColor" },
                        new[] { "Shadow", "Dark", "Base", "Bright", "Core" });
                    body.Add(Tint(serialized, "chargeWaveTint", "Tint", "A colour laid over the whole sweep, on top of the five colours above."));
                    body.Add(Glow(serialized, "chargeWaveEmissiveColor", "Glow", "How brightly the sweep shines in the dark."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.Milestones:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "milestonesVisible",
                        "Visible",
                        "The pairs of marks that light up one by one and stay lit as the portal charges."));
                    AddPalette(
                        body,
                        studio,
                        serialized,
                        layer,
                        new[] { "milestoneDarkColor", "milestoneDeepColor", "milestoneMidColor", "milestoneBrightColor", "milestoneCoreColor" },
                        new[] { "Shadow", "Dark", "Base", "Bright", "Core" });
                    body.Add(Tint(serialized, "milestoneTint", "Tint", "A colour laid over every mark, on top of the five colours above."));
                    body.Add(Glow(serialized, "milestoneEmissiveColor", "Glow", "How brightly the marks shine in the dark."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.Center:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerVisible",
                        "Visible",
                        "The ring a player actually steps into. Almost every portal wants this."));
                    AddPalette(
                        body,
                        studio,
                        serialized,
                        layer,
                        new[] { "centerDarkColor", "centerDeepColor", "centerMidColor", "centerBrightColor", "centerCoreColor", "centerHighlightColor" },
                        new[] { "Shadow", "Dark", "Base", "Bright", "Core", "Highlight" });
                    body.Add(Tint(serialized, "centerTint", "Tint", "A colour laid over the whole middle, on top of the colours above."));
                    body.Add(Glow(serialized, "centerEmissiveColor", "Glow", "How brightly the middle shines in the dark."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerSwirlVisible",
                        "Visible",
                        "The specks of light that drift around inside an open portal."));
                    body.Add(BuildSwirlOverrideField(serialized));
                    body.Add(Tint(serialized, "centerParticleTint", "Tint", "The colour of the drifting specks. It follows the middle's colours unless you change it."));
                    body.Add(Glow(serialized, "centerSwirlEmissiveColor", "Glow", "How brightly the specks shine in the dark."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "playReadyFlash",
                        "Enabled",
                        "The burst of light that goes off the instant the portal finishes charging."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "readyFlashFollowsCenterPalette",
                        "Match Inner Portal",
                        "Take the flash colour from the middle of the portal, so the two always agree. Turn it off to pick a colour of your own."));
                    body.Add(Tint(serialized, "readyFlashTint", "Tint", "The colour of the burst, used when it is not matching the middle."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "readyFlashSprites",
                        "Flash Frames",
                        "The pictures the burst plays through, in order. Leave this alone to use the framework's own burst."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.GroundLight:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightEnabled",
                        "Enabled",
                        "Whether the portal casts a pool of light onto the ground around it. The picture draws the pool exactly as the game lights it, and the dashed ring while this layer is selected marks where it ends."));
                    body.Add(Tint(serialized, "groundLightColor", "Colour", "The colour of the pool of light on the floor."));
                    body.Add(BuildGroundLightBrightnessRow(serialized));
                    break;
            }

            return group;
        }

        /// <summary>
        /// The swirls have two settings that decide whether the rest of the page applies at all,
        /// so changing this one redraws the column beneath it.
        /// </summary>
        private VisualElement BuildSwirlOverrideField(SerializedObject serialized)
        {
            VisualElement field = DimensionsApiControls.Bound(
                serialized,
                "centerSwirlOverrideVanilla",
                "Custom Artwork",
                "Off, the portal drifts the game's own specks. On, it uses your artwork, and you can move and time them yourself.");
            Toggle toggle = field.Q<Toggle>();
            if (toggle != null)
            {
                AfterBinding(toggle, () =>
                    toggle.RegisterValueChangedCallback(evt => DeferredRefresh()));
            }

            return field;
        }

        /// <summary>
        /// The steady brightness the game settles on: the middle of the dimmest and brightest it
        /// is allowed to go. Read only, because it is worked out rather than chosen.
        /// </summary>
        private VisualElement BuildGroundLightBrightnessRow(SerializedObject serialized)
        {
            Label value = new Label(string.Empty);
            value.AddToClassList("dim-readonly-value");
            SerializedProperty minimum = serialized.FindProperty("groundLightMinimumIntensity");
            SerializedProperty maximum = serialized.FindProperty("groundLightMaximumIntensity");
            System.Action recompute = () =>
            {
                if (minimum == null || maximum == null)
                {
                    return;
                }

                serialized.Update();
                float low = minimum.floatValue;
                float high = Mathf.Max(low, maximum.floatValue);
                value.text = ((low + high) * 0.5f).ToString("0.00");
            };
            recompute();
            // Recomputes only when either bound moves — the page costs nothing at rest.
            if (minimum != null)
            {
                value.TrackPropertyValue(minimum, changed => recompute());
            }

            if (maximum != null)
            {
                value.TrackPropertyValue(maximum, changed => recompute());
            }

            return DimensionsApiControls.Field(
                "Effective Brightness",
                "The steady brightness the flicker settles around, halfway between the dimmest and the brightest below. The game works this out itself, so there is nothing to type.",
                value);
        }

        private VisualElement BuildPlacementGroup(
            DimensionPortalAppearanceStudio studio,
            SerializedObject serialized,
            DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            if (layer == DimensionPortalAppearanceStudio.StudioLayer.GroundLight)
            {
                return BuildGroundLightPlacementGroup(serialized);
            }

            string offset;
            string scale;
            string rotation;
            if (!TryGetPlacementProperties(layer, out offset, out scale, out rotation))
            {
                return null;
            }

            if (layer == DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks &&
                !IsSwirlOverrideOn(serialized))
            {
                return null;
            }

            VisualElement group = DimensionsApiControls.Group(
                "Position", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(DimensionsApiControls.Bound(
                serialized,
                offset,
                "Offset",
                "How far this part sits from the middle of the portal, counted in single pixels. You can also drag it around on the picture."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                scale,
                "Scale",
                "Width and height, where one means the size it was drawn at. Two makes it twice as wide or twice as tall."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                rotation,
                "Rotation",
                "How far round to turn this part, in degrees. Most portal art is drawn facing the player, so this usually stays at zero."));

            if (studio.LayerHasLayout(layer))
            {
                DimensionPortalAppearanceStudio.StudioLayer resetLayer = layer;
                body.Add(DimensionsApiControls.GhostButton(
                    "Revert",
                    () => ResetPlacement(resetLayer)));
            }

            return group;
        }

        /// <summary>The pool of light has a place and a reach, but no size and no turning.</summary>
        private VisualElement BuildGroundLightPlacementGroup(SerializedObject serialized)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Position", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(DimensionsApiControls.Bound(
                serialized,
                "groundLightOffsetPixels",
                "Offset",
                "How far the pool of light sits from the middle of the portal, counted in single pixels."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "groundLightRange",
                "Radius",
                "How many tiles out the light spreads across the floor. The dashed ring on the picture shows exactly where it stops."));
            return group;
        }

        private void ResetPlacement(DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null || profile == null)
            {
                return;
            }

            SerializedObject serialized = studio.GetProfileSerializedObject(profile);
            if (serialized == null)
            {
                return;
            }

            studio.ResetLayerLayout(serialized, layer);
            serialized.ApplyModifiedProperties();
            studio.NotifyProfileEdited();
            DeferredRefresh();
        }

        private VisualElement BuildBehaviourGroup(
            DimensionPortalAppearanceStudio studio,
            SerializedObject serialized,
            DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Behaviour", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            switch (layer)
            {
                case DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "chargeWaveSpeed",
                        "Sweep Speed",
                        "One is the speed the animation was drawn at. Two runs it twice as fast."));
                    VisualElement chargeTime = BuildChargeDurationField(studio);
                    if (chargeTime != null)
                    {
                        body.Add(chargeTime);
                    }

                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.Milestones:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "firstMilestone",
                        "First Node",
                        "How far through charging the bottom pair of marks lights up. A quarter of the way is 0.25."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "secondMilestone",
                        "Second Node",
                        "How far through charging the middle pair lights up. Half way is 0.5."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "thirdMilestone",
                        "Third Node",
                        "How far through charging the top pair lights up. Three quarters of the way is 0.75."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.Center:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerGlowIntensity",
                        "Highlight Intensity",
                        "How strongly the white highlight burns through the middle of the portal."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks:
                    if (!IsSwirlOverrideOn(serialized))
                    {
                        return null;
                    }

                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerSwirlPlaybackSpeed",
                        "Swirl Speed",
                        "One is the speed the animation was drawn at. Two runs it twice as fast."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerParticleEmissionMultiplier",
                        "Density",
                        "One is as many specks as the game's own portal drifts. Two is twice as many."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "readyFlashEmissionMultiplier",
                        "Intensity",
                        "One is the brightness the burst was drawn at. Higher makes the whole cave flare."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "readyFlashSizeMultiplier",
                        "Size",
                        "One is the size the burst was drawn at. Two makes it twice as wide."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.GroundLight:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightMinimumIntensity",
                        "Minimum Intensity",
                        "The lowest the pool of light drops to as it flickers."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightMaximumIntensity",
                        "Maximum Intensity",
                        "The highest the pool of light rises to as it flickers."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightMovement",
                        "Drift",
                        "Give the pool of light the same restless drift the game's own portals have."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightCastsShadows",
                        "Cast Shadows",
                        "Let the light throw shadows from whatever stands near the portal. Costs more to draw."));
                    break;

                default:
                    return null;
            }

            return group;
        }

        /// <summary>
        /// How long the placed portal takes to charge lives on the dimension, not on the look, so
        /// it is told to the studio separately and never counted as a change to the artwork.
        /// </summary>
        private VisualElement BuildChargeDurationField(DimensionPortalAppearanceStudio studio)
        {
            if (studio.InstantPortalMode || template == null)
            {
                return null;
            }

            SerializedObject serializedTemplate = studio.GetTemplateSerializedObject(template);
            if (serializedTemplate == null ||
                serializedTemplate.FindProperty("portalActivationChargeSeconds") == null)
            {
                return null;
            }

            VisualElement host = new VisualElement();
            host.Add(DimensionsApiControls.Bound(
                serializedTemplate,
                "portalActivationChargeSeconds",
                "Charge Time",
                "How many seconds a placed portal takes to become ready, counted from the moment it is put down."));
            AfterBinding(host, () => host.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                DimensionPortalAppearanceStudio target = Studio;
                if (target != null)
                {
                    target.NotifyTemplateEdited();
                }

                evt.StopPropagation();
            }));

            return host;
        }

        private VisualElement BuildShadowGroup(SerializedObject serialized)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Shadow", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(DimensionsApiControls.Bound(
                serialized,
                "portalShadowEnabled",
                "Enabled",
                "Whether a shadow is drawn on the floor under the portal."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "portalShadowSprite",
                "Sprite",
                "The picture used for the shadow lying flat on the floor. Leave it empty to use the framework's own."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "portalShadowCasterSprite",
                "Caster Sprite",
                "The shape used when the shadow has to move with a nearby light. Leave it empty to use the framework's own."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "portalShadowScale",
                "Scale",
                "Width and height of the shadow, where one means the size it was drawn at."));
            return group;
        }

        // ------------------------------------------------------------------- ingredients --

        private VisualElement Tint(
            SerializedObject serialized,
            string propertyPath,
            string label,
            string tooltip)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return DimensionsApiControls.Bound(serialized, propertyPath, label, tooltip);
            }

            ColorField field = new ColorField();
            field.BindProperty(property);
            return DimensionsApiControls.Field(label, tooltip, field);
        }

        /// <summary>A glow can be brighter than white, so its picker has to allow that.</summary>
        private VisualElement Glow(
            SerializedObject serialized,
            string propertyPath,
            string label,
            string tooltip)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return DimensionsApiControls.Bound(serialized, propertyPath, label, tooltip);
            }

            ColorField field = new ColorField { hdr = true };
            field.BindProperty(property);
            return DimensionsApiControls.Field(label, tooltip, field);
        }

        /// <summary>
        /// The colours that live inside the artwork itself. Changing one has to be painted back
        /// into this portal's own copy of the picture, which is what the studio is told to do.
        /// </summary>
        private void AddPalette(
            VisualElement body,
            DimensionPortalAppearanceStudio studio,
            SerializedObject serialized,
            DimensionPortalAppearanceStudio.StudioLayer layer,
            string[] propertyPaths,
            string[] labels)
        {
            for (int i = 0; i < propertyPaths.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(propertyPaths[i]);
                if (property == null)
                {
                    continue;
                }

                ColorField field = new ColorField();
                field.BindProperty(property);
                DimensionPortalAppearanceStudio.StudioLayer bakeLayer = layer;
                AfterBinding(field, () => field.RegisterValueChangedCallback(evt =>
                {
                    DimensionPortalAppearanceStudio target = Studio;
                    if (target != null)
                    {
                        target.NotifyPaletteColorEdited(bakeLayer);
                    }
                }));
                body.Add(DimensionsApiControls.Field(
                    labels[i],
                    "One of the colours the artwork itself is painted in. Change it and this portal's own copy of the picture is repainted to match.",
                    field));
            }
        }

        private static bool IsSwirlOverrideOn(SerializedObject serialized)
        {
            SerializedProperty property = serialized.FindProperty("centerSwirlOverrideVanilla");
            return property != null && property.boolValue;
        }

        private static bool TryGetPlacementProperties(
            DimensionPortalAppearanceStudio.StudioLayer layer,
            out string offset,
            out string scale,
            out string rotation)
        {
            switch (layer)
            {
                case DimensionPortalAppearanceStudio.StudioLayer.Frame:
                    offset = "frameOffsetPixels";
                    scale = "frameScale";
                    rotation = "frameRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep:
                    offset = "chargeWaveOffsetPixels";
                    scale = "chargeWaveScale";
                    rotation = "chargeWaveRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.Milestones:
                    offset = "milestoneOffsetPixels";
                    scale = "milestoneScale";
                    rotation = "milestoneRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.Center:
                    offset = "centerOffsetPixels";
                    scale = "centerScale";
                    rotation = "centerRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks:
                    offset = "centerParticleOffsetPixels";
                    scale = "centerParticleScale";
                    rotation = "centerParticleRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst:
                    offset = "readyFlashOffsetPixels";
                    scale = "readyFlashScale";
                    rotation = "readyFlashRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.GroundLight:
                    offset = "groundLightOffsetPixels";
                    scale = string.Empty;
                    rotation = string.Empty;
                    return false;
                default:
                    offset = string.Empty;
                    scale = string.Empty;
                    rotation = string.Empty;
                    return false;
            }
        }
    }
}
