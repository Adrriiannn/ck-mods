using Pug.Sprite;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Puts the subtree Core Keeper uses for a placed light onto a graphical prefab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A PLACED LIGHT IS NOT A COMPONENT, IT IS FIVE NODES. Measured across the game's own
    /// <c>Torch</c>, <c>Lamp</c>, <c>Campfire</c>, <c>CrystalLamp</c>, <c>ChineseLantern</c> and
    /// <c>LampPost</c> prefabs, every one of them carries the identical shape: a node with a
    /// <c>ManagedLight</c>, a container under it, a point light under that carrying a Unity
    /// <c>Light</c>, a <c>PugLight</c> and a <c>LightFlickerEffect</c>, and an
    /// <c>IndirectLightSprite</c> beside it. The entity half of a torch carries no light at all.
    /// Nothing here can be replaced by adding a component.
    /// </para>
    /// <para>
    /// WHICH IS WHY IT IS CLONED RATHER THAN BUILT. <c>PugLight</c> is
    /// <c>[RequireComponent(typeof(Light))]</c> and <c>ManagedLight.SetOptimized</c> dereferences
    /// the <c>PugLight</c> it found in <c>Awake</c> with no guard, so a hand-built <c>Light</c>
    /// without one beside it is a null reference on every optimisation pass. The donor prefab that
    /// ships with this framework already holds the whole correct subtree, so copying it is the only
    /// way to be sure every node the game dereferences is present.
    /// </para>
    /// <para>
    /// EXTRACTED, NOT COPIED. This is the body of
    /// <c>DimensionRuntimeConsumerBootstrapUtility.EnsurePortalManagedLight</c>, which built this
    /// subtree for the dimension portal and is the version that has been seen working in game. That
    /// method now calls this one and supplies the portal's own numbers, so there is one builder and
    /// not two to drift apart.
    /// </para>
    /// <para>
    /// WHAT THE CALLER STILL HAS TO DO. When the graphical prefab has a view on it, assign the
    /// returned light to <c>EntityMonoBehaviour.optionalLightOptimizer</c> — that field is what
    /// switches the light back on when the object is drawn and off again while it is dying
    /// (<c>EntityMonoBehaviour.cs:591</c>, <c>:625</c>, <c>:1117</c>). Vanilla's <c>Lamp.prefab</c>
    /// assigns exactly that.
    /// </para>
    /// </remarks>
    internal static class DimensionEmittedLightBuilder
    {
        /// <summary>
        /// The prefab the whole subtree is copied out of. It ships inside this framework, at an
        /// <c>AssetDatabase</c> path, so any generator can reach it.
        /// </summary>
        internal const string TemplatePath =
            "Assets/ExpandNullforge/Editor/VanillaPortalReference/Prefabs/DimensionPortalVisualTemplate.prefab";

        /// <summary>The name of the node that carries the <c>ManagedLight</c>.</summary>
        /// <remarks>
        /// The game's own prefabs call it <c>LightOptimizer</c> and the donor calls it
        /// <c>PugLight</c>. The name is not read by anything at runtime — <c>ManagedLight</c> finds
        /// its parts through its own three serialized references — so the donor's name is kept,
        /// because changing it would make the portal's generated prefab differ from the one already
        /// tested.
        /// </remarks>
        internal const string LightObjectName = "PugLight";

        /// <summary>The name of the sprite <c>ManagedLight</c> falls back to when it dims a light.</summary>
        internal const string IndirectLightSpriteName = "IndirectLightSprite";

        /// <summary>Where the donor prefab keeps its own light node, in the donor's local space.</summary>
        /// <remarks>
        /// The portal's light is placed relative to this rather than absolutely, which is how it was
        /// written and how it has been seen working; reading it back out of the prefab keeps that
        /// true without copying the number into code, where it would go stale the day the donor
        /// moves.
        /// </remarks>
        internal static Vector3 TemplateLightLocalPosition()
        {
            Transform source = FindTheDonorLightNode();
            return source == null ? Vector3.zero : source.localPosition;
        }

        /// <summary>
        /// Gives <paramref name="parent"/> a light, replacing any this builder left there before.
        /// </summary>
        /// <param name="parent">The node the light hangs under.</param>
        /// <param name="localPosition">Where the light sits, in that node's local space.</param>
        /// <param name="colour">The colour of the light on the floor.</param>
        /// <param name="intensity">
        /// How bright the light is. When the flicker bounds differ, the game overwrites this on the
        /// first frame with their midpoint — see <c>LightFlickerEffect.Awake</c> — so a caller that
        /// wants the prefab to read true passes that midpoint itself.
        /// </param>
        /// <param name="range">How far it reaches, in tiles.</param>
        /// <param name="castsShadows">Whether things near it throw shadows.</param>
        /// <param name="dimmest">The bottom of the flicker. Equal to <paramref name="brightest"/> for a steady light.</param>
        /// <param name="brightest">The top of the flicker.</param>
        /// <param name="flameMoves">Whether the light shifts slightly as it flickers.</param>
        /// <param name="enabled">
        /// Whether the light is on. When it is off the subtree is left inactive and this returns
        /// null, which is deliberate: <c>EntityMonoBehaviour</c> switches an assigned
        /// <c>optionalLightOptimizer</c> back on the moment the object is drawn, so an author's
        /// "no light" answer only survives if the field is left unassigned.
        /// </param>
        /// <returns>The light, or null when it is switched off.</returns>
        internal static ManagedLight Ensure(
            Transform parent,
            Vector3 localPosition,
            Color colour,
            float intensity,
            float range,
            bool castsShadows,
            float dimmest,
            float brightest,
            bool flameMoves,
            bool enabled)
        {
            if (parent == null)
            {
                return null;
            }

            // A REGENERATE OVER AN EXISTING PREFAB WOULD OTHERWISE STACK A SECOND LIGHT. The portal
            // path reloads its prefab and builds on top of it, so without this every pass adds one
            // more light to the same object. It costs nothing on a prefab built from scratch.
            Transform existing = parent.Find(LightObjectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject, true);
            }

            Transform source = FindTheDonorLightNode();
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    "Could not find the vanilla portal PugLight subtree at " +
                    TemplatePath +
                    ".");
            }

            GameObject clone = Object.Instantiate(source.gameObject);
            clone.name = LightObjectName;
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = localPosition;
            clone.transform.localRotation = source.localRotation;
            clone.transform.localScale = source.localScale;

            ManagedLight managedLight = clone.GetComponent<ManagedLight>();
            Light light = clone.GetComponentInChildren<Light>(true);
            SpriteObject fallback = FindTheFallbackSprite(clone);
            if (managedLight == null || light == null || fallback == null)
            {
                throw new System.InvalidOperationException(
                    "The generated portal PugLight subtree is incomplete. " +
                    "Expected ManagedLight, Point Light, and IndirectLightSprite SpriteObject.");
            }

            managedLight.lightContainer = light.transform.parent != null
                ? light.transform.parent.gameObject
                : light.gameObject;
            managedLight.lightToOptimize = light;
            managedLight.fallbackRenderer = fallback;
            fallback.gameObject.SetActive(false);

            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.shadows = castsShadows ? LightShadows.Hard : LightShadows.None;

            LightFlickerEffect flicker = clone.GetComponentInChildren<LightFlickerEffect>(true);
            if (flicker != null)
            {
                flicker.flickeringLight = light;
                flicker.enableMovement = flameMoves;
                flicker.SetIntensityRange(dimmest, brightest);
            }

            clone.SetActive(enabled);

            // EntityMonoBehaviour automatically re-enables an assigned optional light
            // when the visual is hydrated. Leaving the disabled subtree unassigned is
            // therefore required for an authored "no ground light" preset to persist.
            return enabled ? managedLight : null;
        }

        /// <summary>Finds the donor's light node by the component it carries, not by walking names.</summary>
        private static Transform FindTheDonorLightNode()
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
            if (template == null)
            {
                return null;
            }

            ManagedLight[] lights = template.GetComponentsInChildren<ManagedLight>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].gameObject.name == LightObjectName)
                {
                    return lights[i].transform;
                }
            }

            return null;
        }

        /// <summary>Finds the sprite the light is swapped for when the game dims it.</summary>
        private static SpriteObject FindTheFallbackSprite(GameObject clone)
        {
            SpriteObject[] sprites = clone.GetComponentsInChildren<SpriteObject>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].gameObject.name == IndirectLightSpriteName)
                {
                    return sprites[i];
                }
            }

            return null;
        }
    }
}
