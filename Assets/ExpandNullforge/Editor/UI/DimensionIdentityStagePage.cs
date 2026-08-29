using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The Dimension stage: the dimension itself rather than anything inside it. Its name, the
    /// mod it ships in, and the patch of world it reserves.
    /// </summary>
    /// <remarks>
    /// This is the first page a creator meets after Home, so it opens with the two things they
    /// came to write (the name and the description) and keeps the coordinate arithmetic further
    /// down, where it belongs for something the framework already filled in correctly.
    /// </remarks>
    internal sealed class DimensionIdentityStagePage
    {
        /// <summary>A value the creator should see but never type into.</summary>
        private static VisualElement ReadOnlyLine(string label, string value, string tooltip)
        {
            Label shown = new Label(string.IsNullOrEmpty(value) ? "not set yet" : value);
            shown.AddToClassList("dim-readonly-value");
            return DimensionsApiControls.Field(label, tooltip, shown);
        }

        private VisualElement root;
        private VisualElement body;
        private DimensionTemplateAsset template;

        internal VisualElement Build()
        {
            root = new VisualElement();
            root.AddToClassList("dim-stage-page-root");

            ScrollView scroller = new ScrollView(ScrollViewMode.Vertical);
            scroller.AddToClassList("dim-fill");
            body = new VisualElement();
            body.AddToClassList("dim-single-detail");
            scroller.Add(body);
            root.Add(scroller);
            return root;
        }

        internal void Refresh(DimensionTemplateAsset dimensionTemplate)
        {
            template = dimensionTemplate;
            if (body == null)
            {
                return;
            }

            body.Clear();
            if (template == null)
            {
                body.Add(DimensionsApiControls.EmptyState(
                    "No dimension open",
                    "Open a dimension from Home and its details appear here.",
                    null));
                return;
            }

            SerializedObject serialized = new SerializedObject(template);

            // Packaging (mod name, version, author) and compatibility (generation version,
            // framework version, dependencies) are deliberately absent: the ModSDK's own build
            // window owns those, and this framework does not reimplement the ModSDK. The values
            // still live on the asset; nothing here is the only door to them.
            VisualElement what = DimensionsApiControls.Group("Basics", null);
            VisualElement whatBody = DimensionsApiControls.BodyOf(what);
            whatBody.Add(DimensionsApiControls.Bound(
                serialized,
                "displayName",
                "Name",
                "What this dimension is called. Players see this on the portal and on the title card when they arrive."));
            whatBody.Add(DimensionsApiControls.Bound(
                serialized,
                "description",
                "Description",
                "A line about the place, for you and for anyone reading your mod's page. Not shown in the game."));
            // The id is generated and load-bearing: saved worlds and portals point at it, and
            // every item and mob this dimension adds is namespaced under it. It is shown because
            // hiding it would be dishonest, and locked because editing it would silently orphan
            // every world already built on this dimension.
            whatBody.Add(ReadOnlyLine(
                "Dimension ID",
                serialized.FindProperty("dimensionId") == null
                    ? string.Empty
                    : serialized.FindProperty("dimensionId").stringValue,
                "The id everything in this dimension is filed under. An item named Zanium in a " +
                "dimension with the id Nullforge becomes Nullforge:Zanium. The framework " +
                "generates it from the name and keeps it steady, because saved worlds point at " +
                "the dimension by this id."));
            whatBody.Add(DimensionsApiControls.Bound(
                serialized,
                "dimensionType",
                "Type",
                "What sort of place this is, and the behavior that comes with it. A World is " +
                "yours to shape. A Dungeon keeps wandering creatures out and plays its own " +
                "music. An Arena resets between visits and opens its exit when its boss falls. " +
                "A Room stays exactly as you built it."));
            body.Add(what);

            // Axis facts below are measured from the game, not assumed: +X is east (right on
            // screen), +Y is north (up on screen), one unit is one tile, world (0,0) is the
            // Core, and the game's own compass code is the authority for all four.
            VisualElement space = DimensionsApiControls.Group("Coordinates", null);
            VisualElement spaceBody = DimensionsApiControls.BodyOf(space);
            spaceBody.Add(DimensionsApiControls.Bound(
                serialized,
                "absoluteOrigin",
                "Origin",
                "The world tile this dimension is anchored to — its own zero. Every coordinate " +
                "below counts from here, in tiles: X grows east (right on screen), Y grows " +
                "north (up). Portals drop players on this tile unless a portal says otherwise. " +
                "The default clears the game's own world; if yours collides, the game moves " +
                "the whole dimension further north when it loads and says so in the log."));
            spaceBody.Add(DimensionsApiControls.Bound(
                serialized,
                "reservedLocalMin",
                "Reaches From",
                "The south west corner of the ground this dimension claims: the smallest X and " +
                "the smallest Y, counted in tiles from the origin. This corner tile is inside " +
                "the claim."));
            spaceBody.Add(DimensionsApiControls.Bound(
                serialized,
                "reservedLocalMaxExclusive",
                "Reaches To",
                "The north east corner: the first tile past the claim on each axis, so it is " +
                "not itself included. Width and height are To minus From. If your map " +
                "outgrows this box, the framework reserves a bigger one and warns you."));
            spaceBody.Add(BuildCoordinateValidationNote(serialized));
            body.Add(space);

            // The music belongs here rather than on a page of its own because it is a fact about
            // the whole place, like its name and its size, and because there was nowhere at all to
            // say it before: every dimension played the game's mould dungeon music whatever it was.
            VisualElement sound = DimensionsApiControls.Group("Sound", null);
            VisualElement soundBody = DimensionsApiControls.BodyOf(sound);
            soundBody.Add(DimensionsApiControls.Bound(
                serialized,
                "music",
                "Music",
                "What this whole place sounds like. Type one of the game's own music names — " +
                "MOLD_DUNGEON, MYSTERY, HOME_BASE and the rest — or a name of your own and list " +
                "your tracks below. A boss's own fight music still wins while the fight is on. " +
                "Leave it empty and the music is decided by the ground underfoot, the way it is " +
                "everywhere else in the game."));
            soundBody.Add(DimensionsApiControls.Bound(
                serialized,
                "musicTracks",
                "Your Tracks",
                "The clips your own music is made of, named the way sounds are named. Only used " +
                "when the music above is a name of your own; one of the game's names brings its " +
                "own tracks."));
            body.Add(sound);
        }

        /// <summary>
        /// The three coordinate failures a creator can type, caught while they type them.
        /// </summary>
        /// <remarks>
        /// Each of these is otherwise discovered much later and much worse: an inverted box is
        /// a compile error, a claim overlapping the game's own world is silently relocated
        /// north at load, and a box that excludes the origin breaks every portal still aimed at
        /// its default arrival tile. The check runs once when the page builds and again only
        /// when a coordinate field actually changes — never on a clock.
        /// </remarks>
        private static VisualElement BuildCoordinateValidationNote(SerializedObject serialized)
        {
            Label note = new Label(string.Empty);
            note.AddToClassList("dim-check-detail");
            note.style.whiteSpace = WhiteSpace.Normal;
            System.Action recompute = () =>
            {
                if (serialized == null || serialized.targetObject == null)
                {
                    return;
                }

                serialized.Update();
                SerializedProperty originProperty = serialized.FindProperty("absoluteOrigin");
                SerializedProperty minProperty = serialized.FindProperty("reservedLocalMin");
                SerializedProperty maxProperty =
                    serialized.FindProperty("reservedLocalMaxExclusive");
                if (originProperty == null || minProperty == null || maxProperty == null)
                {
                    return;
                }

                UnityEngine.Vector2Int origin = originProperty.vector2IntValue;
                UnityEngine.Vector2Int min = minProperty.vector2IntValue;
                UnityEngine.Vector2Int max = maxProperty.vector2IntValue;

                string text = string.Empty;
                if (max.x <= min.x || max.y <= min.y)
                {
                    text = "Reaches To must be greater than Reaches From on both axes, or " +
                           "the dimension has no ground and cannot build.";
                }
                else if (origin.x + max.x > -5000 && origin.x + min.x < 5000 &&
                         origin.y + max.y > -5000 && origin.y + min.y < 5000)
                {
                    // The protected band is the same ±5000 the runtime allocator enforces.
                    text = "This claim overlaps the game's own world (the 5000 tiles around " +
                           "the Core). The game will move the whole dimension further north " +
                           "when it loads.";
                }
                else if (min.x > 0 || min.y > 0 || max.x <= 0 || max.y <= 0)
                {
                    text = "The claim does not contain the origin tile. Portals aim there " +
                           "unless told otherwise, and a portal aimed outside the claim " +
                           "refuses to register.";
                }

                if (note.text != text)
                {
                    note.text = text;
                    note.style.display = string.IsNullOrEmpty(text)
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
                }
            };

            // Once now, then only when a coordinate actually changes — never on a clock. The
            // page must cost nothing while nobody is typing in it.
            note.schedule.Execute(() => recompute()).StartingIn(80);
            note.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                VisualElement column = note.parent;
                column?.RegisterCallback<ChangeEvent<UnityEngine.Vector2Int>>(
                    changed => recompute());
            });
            return note;
        }
    }
}
