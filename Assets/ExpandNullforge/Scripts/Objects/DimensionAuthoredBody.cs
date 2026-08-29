using UnityEngine;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// The one place that knows where a generated object's own picture lives and how to put it on a
    /// renderer, shared by every framework view that draws one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE PICTURE TRAVELS IN <c>ObjectInfo.additionalSprites</c>. That is not a spare field being
    /// borrowed: it is where Core Keeper already keeps an object's own picture for the case where
    /// something other than the world has to draw it — <c>PlayerController</c> reads
    /// <c>additionalSprites[0]</c>, falling back to <c>icon</c>, for the sprite of a carried object.
    /// Putting the world sprite there means one answer feeds both the thing standing on the ground
    /// and the thing held over a player's head, and the reference reaches the running game on the
    /// object's own record rather than through a side table that would have to be kept in step.
    /// </para>
    /// <para>
    /// IT IS A HELPER RATHER THAN A BASE CLASS BECAUSE THE VIEWS CANNOT SHARE ONE. Each behaviour has
    /// to derive from the game's own component — a cattle window will only accept a <c>Cattle</c>,
    /// a crafting window only a <c>CraftingBuilding</c> — so the six framework views have six
    /// different bases and nowhere to put shared code except here.
    /// </para>
    /// </remarks>
    public static class DimensionAuthoredBody
    {
        /// <summary>
        /// Shows the picture belonging to the object being drawn, or nothing at all when it has none.
        /// </summary>
        /// <remarks>
        /// Switching the renderer off rather than leaving the previous sprite is the point of the
        /// call. Every framework view is POOLED BY TYPE and therefore shared with every other object
        /// of its behaviour, so an instance that drew a bench a moment ago is handed straight to a
        /// shop; keeping the last picture would show a player the wrong object, which is worse than
        /// showing none.
        /// </remarks>
        public static void Point(SpriteRenderer body, ObjectID objectID, int variation)
        {
            if (body == null)
            {
                return;
            }

            Sprite sprite = Resolve(objectID, variation);
            body.sprite = sprite;
            body.enabled = sprite != null;
        }

        /// <summary>
        /// The object's own picture, or its inventory icon when it has none.
        /// </summary>
        /// <remarks>
        /// The icon fallback matches what the game does for a carried object, so an object authored
        /// with only an icon still shows something rather than nothing.
        /// </remarks>
        public static Sprite Resolve(ObjectID objectID, int variation)
        {
            ObjectInfo info = PugDatabase.GetObjectInfo(objectID, variation);
            if (info == null)
            {
                return null;
            }

            if (info.additionalSprites != null && info.additionalSprites.Count > 0 &&
                info.additionalSprites[0] != null)
            {
                return info.additionalSprites[0];
            }

            return info.icon;
        }

        /// <summary>
        /// The localization term the game will look up for an object's own name.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Copied from the route the game's own tooltips take — <c>InventorySlotUI</c> asks
        /// <c>API.Authoring.ObjectProperties</c> for the object's "name" property and falls back to
        /// the enum name — because a mod's object has no <c>ObjectID</c> the enum can spell, and
        /// nothing else in the running game knows the name a modder gave it.
        /// </para>
        /// <para>
        /// Core Keeper's patched I2 localization replaces every ':' with '_' before it searches, so
        /// the term has to be written the same way or a modder's namespaced object name silently
        /// misses every row they wrote. This mirrors <c>DimensionLocalizationCsv.ToLookupKeyName</c>,
        /// which writes those rows.
        /// </para>
        /// </remarks>
        public static string NameTerm(ObjectID objectID)
        {
            string name;
            if (!PugMod.API.Authoring.ObjectProperties.TryGetPropertyString(objectID, "name", out name) ||
                string.IsNullOrEmpty(name))
            {
                name = objectID.ToString();
            }

            return "Items/" + name.Replace(':', '_');
        }
    }

    /// <summary>
    /// A framework view that draws an authored picture, so the generator can wire the renderer
    /// without knowing which of the six behaviours it is looking at.
    /// </summary>
    /// <remarks>
    /// The property is backed by an ordinary public field on each view rather than being auto
    /// implemented, because Unity only serializes fields: the generator sets this through the
    /// interface and the value has to survive being written into a prefab asset.
    /// </remarks>
    public interface IDimensionAuthoredBody
    {
        /// <summary>The renderer that draws the object. Wired at generation.</summary>
        SpriteRenderer Body { get; set; }
    }
}
