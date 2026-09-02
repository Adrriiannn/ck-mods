using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Builds the words that hang in the air above a thing: the writing on a sign, the name on a
    /// chest, the name tag over an animal, and a boss's title.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FONT WAS NEVER THE PROBLEM, AND THAT IS THE WHOLE REASON THIS FILE EXISTS. Every earlier
    /// pass over this framework recorded floating text as unreachable because "a PugText needs a
    /// PugFont asset no mod ships". That is wrong at the field level: <c>PugText.font</c> is
    /// declared <c>[NonSerialized]</c> (<c>Pug.Other/PugText.cs:1000-1001</c>), so no prefab in the
    /// game carries one either. It is filled in at runtime — <c>PugText.Render</c> calls
    /// <c>SetFont(style.fontFace)</c> before it touches <c>font</c> (<c>PugText.cs:605</c>), and
    /// <c>SetFont</c> fetches the face from the game's own <c>TextManager</c> singleton
    /// (<c>PugText.cs:247</c>, <c>Manager.text.GetFont</c>). A mod supplies a style and a string;
    /// the game supplies the letters. Nothing has to be shipped, decoded or synthesised.
    /// </para>
    /// <para>
    /// THE REAL REQUIREMENT IS THE LAYER, AND IT IS EASY TO MISS. <c>PugFont.Render</c> reads
    /// <c>root.gameObject.layer</c> once and stamps it onto every glyph it pulls from the pool
    /// (<c>PugFont.cs:128</c>, "int layer = root.gameObject.layer"), where <c>root</c> is the PugText's
    /// own transform. Every world-space PugText the game ships sits on layer 18, <c>WorldUI</c> —
    /// measured on <c>Chest.prefab</c>, <c>SignText.prefab</c>, <c>Camel.prefab</c> and
    /// <c>BossLarva.prefab</c>. A PugText left on the default layer renders its glyphs onto the
    /// default layer, which is not where the game draws floating text.
    /// </para>
    /// <para>
    /// THE SECOND REQUIREMENT IS A PARENT. <c>WorldLabel.UpdateWorldText</c> writes
    /// <c>worldLabel.transform.parent.localPosition</c> every frame with no null check, and
    /// <c>ObjectNameTag.Awake</c> does the same. So the text can never be added straight onto the
    /// object: it always gets a container of its own, exactly as vanilla nests
    /// <c>WorldText &gt; text</c> and <c>ObjectName &gt; name</c>.
    /// </para>
    /// <para>
    /// THE CONTAINER HANGS OFF THE ROOT, NOT THE SCALER. <c>Chest.prefab</c> and
    /// <c>Camel.prefab</c> both parent their text container to the graphical prefab's root, beside
    /// <c>XScaler</c> rather than under it. Under the scaler the words would mirror themselves the
    /// first time the object faced left.
    /// </para>
    /// <para>
    /// WHY THE POSITIONS LOOK ABSURD. Core Keeper's camera is a 45 degree orthographic one, so
    /// moving something up in Y and toward the camera in Z both push it up the screen and the two
    /// largely cancel in depth. That is why vanilla parks a text container at y 5, z -5: the pair
    /// nets out to a small rise on screen while pulling the glyphs several units in front of
    /// everything they must not be hidden behind. Those numbers are copied, not invented.
    /// </para>
    /// </remarks>
    internal static class DimensionFloatingTextUtility
    {
        /// <summary>The layer every world-space PugField in the game sits on.</summary>
        /// <remarks>
        /// Looked up by name so the value follows the project, with the measured index as the
        /// fallback for the case where the name lookup fails. Both agree today: the SDK's
        /// <c>ProjectSettings/TagManager.asset</c> lists <c>WorldUI</c> at index 18, and so does the
        /// ripped game's.
        /// </remarks>
        private const string WorldUiLayerName = "WorldUI";

        /// <summary>The measured index of <see cref="WorldUiLayerName"/>.</summary>
        private const int WorldUiLayerIndexFallback = 18;

        /// <summary>The sorting layer a chest's and a sign's floating words are drawn on.</summary>
        private const string LabelSortingLayerName = "Front";

        /// <summary>The sorting layer a name tag and a boss title are drawn on.</summary>
        private const string NameSortingLayerName = "GUI";

        /// <summary>The name vanilla gives the container above a chest or a sign.</summary>
        private const string WorldTextContainerName = "WorldText";

        /// <summary>The name vanilla gives the PugText above a chest or a sign.</summary>
        private const string WorldTextChildName = "text";

        /// <summary>The name vanilla gives the container above an animal.</summary>
        private const string NameTagContainerName = "ObjectName";

        /// <summary>The name vanilla gives the PugText above an animal.</summary>
        private const string NameTagChildName = "name";

        /// <summary>The name this framework gives the container above a boss.</summary>
        private const string BossPlateContainerName = "NamePlate";

        /// <summary>The name this framework gives the PugText above a boss.</summary>
        private const string BossPlateChildName = "Name";

        /// <summary>
        /// How high above a chest its floating words sit, in world units, before the camera's angle
        /// is applied. <c>Chest.prefab</c>'s value.
        /// </summary>
        public const float DefaultHeightAboveAnObject = 0.375f;

        /// <summary>
        /// How high above an animal its name tag sits. <c>Camel.prefab</c>'s value.
        /// </summary>
        public const float DefaultHeightAboveAnAnimal = 2.0625f;

        /// <summary>
        /// Hangs the writing a player can read above the thing, and hands back the text component
        /// the game's <c>WorldLabel</c> wants pointed at it.
        /// </summary>
        /// <param name="root">The graphical prefab being built.</param>
        /// <param name="howHigh">
        /// How far above the object the words sit. See <see cref="DefaultHeightAboveAnObject"/>.
        /// </param>
        /// <remarks>
        /// Value for value from <c>Chest.prefab</c> and <c>SignText.prefab</c>, which carry
        /// identical text settings and differ only in that height. The outline is the visible half
        /// of it: the words are drawn over whatever the object is standing on, and an unoutlined
        /// light grey on a light floor is unreadable.
        /// </remarks>
        public static PugText AddWordsThatFloatAboveIt(GameObject root, float howHigh)
        {
            if (root == null)
            {
                return null;
            }

            Transform container = MakeContainer(root, WorldTextContainerName, new Vector3(0f, 0.125f, 0f));

            PugText text = MakeText(
                container,
                WorldTextChildName,
                // z is pulled a quarter unit toward the camera so the words sit in front of the
                // object's own sprite rather than inside it.
                new Vector3(0f, howHigh, -0.25f),
                TextManager.FontFace.thinSmall,
                PugTextStyle.VerticalAlignment.center,
                new Color(0.8584906f, 0.8584906f, 0.8584906f, 1f),
                LabelSortingLayerName);

            // -1 is every side. Vanilla's own value, and the reason PugFont widens each glyph rect
            // by a pixel when it builds the sprites.
            text.style.outline = (PugTextStyle.Outline)(-1);
            text.style.outlineColor = new Color(0f, 0f, 0f, 1f);

            // Not rendered on Start: WorldLabel.ManagedLateUpdate renders it every frame from the
            // entity's own DescriptionBuffer, and rendering the placeholder first would show one
            // frame of nothing useful. keepEnabledOnStart is what stops PugText.Start from
            // deactivating the object outright when renderOnStart is off (PugText.cs:126-133).
            text.renderOnStart = false;
            text.keepEnabledOnStart = true;
            text.alwaysUpdateDynamicTextPixelPos = true;

            // A player types these words, so the game's own filter applies.
            text.checkForProfanity = true;

            // Vanilla's placeholder. It is never shown: the first frame overwrites it.
            text.SetText("\n");

            return text;
        }

        /// <summary>
        /// Hangs a name tag above the thing and hands back the tag component the game's
        /// <c>Cattle</c> wants pointed at it.
        /// </summary>
        /// <param name="root">The graphical prefab being built.</param>
        /// <param name="howHigh">
        /// How far above the animal the tag sits. See <see cref="DefaultHeightAboveAnAnimal"/>.
        /// </param>
        /// <remarks>
        /// <para>
        /// Value for value from <c>Camel.prefab</c>. The tag is a plain <c>ObjectNameTag</c> —
        /// twenty lines whose only job is to hide the words while the player has the interface
        /// switched off (<c>ObjectNameTag.cs:22-31</c>) — wrapped around a PugText.
        /// </para>
        /// <para>
        /// <c>ObjectNameTag.container</c> is the OBJECT HOLDING THE TEXT, not the outer container.
        /// That reads backwards and it is what vanilla wires: on <c>Camel.prefab</c> the tag sits on
        /// <c>ObjectName</c> and its <c>container</c> field points at the <c>name</c> child beneath
        /// it. Wire it the other way round and hiding the interface hides the object the tag itself
        /// lives on, which stops <c>Awake</c> and <c>LateUpdate</c> from ever running again.
        /// </para>
        /// </remarks>
        public static ObjectNameTag AddNameTagAboveIt(GameObject root, float howHigh)
        {
            if (root == null)
            {
                return null;
            }

            Transform container = MakeContainer(root, NameTagContainerName, Vector3.zero);

            PugText text = MakeText(
                container,
                NameTagChildName,
                new Vector3(0f, howHigh, 0f),
                TextManager.FontFace.thinSmall,
                PugTextStyle.VerticalAlignment.bottom,
                new Color(1f, 1f, 1f, 0.29411766f),
                NameSortingLayerName);

            // Rendered on Start, unlike the chest label: an animal's tag is blank until somebody
            // names it, and Cattle.UpdateName re-renders it from NameCD every frame after that.
            text.renderOnStart = true;
            text.keepEnabledOnStart = true;
            text.alwaysUpdateDynamicTextPixelPos = false;
            text.checkForProfanity = true;
            text.SetText(string.Empty);

            ObjectNameTag tag = container.gameObject.AddComponent<ObjectNameTag>();
            tag.container = text.gameObject;
            tag.text = text;
            return tag;
        }

        /// <summary>
        /// Hangs a boss's title above it and hands back the text component the boss view renders
        /// its name into.
        /// </summary>
        /// <remarks>
        /// Value for value from <c>BossLarva.prefab</c>, whose nameplate is a PugText on a layer-18
        /// child called <c>Name</c> at y 0.5, under a container at y 8, z -4. The framework used to
        /// build this plate straight onto the root at y -0.5 on the default layer, which is both
        /// the wrong layer for the glyphs and roughly at the boss's feet.
        /// </remarks>
        public static PugText AddBossNameAboveIt(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            Transform container =
                MakeContainer(root, BossPlateContainerName, new Vector3(0f, 8f, -4f));

            PugText text = MakeText(
                container,
                BossPlateChildName,
                new Vector3(0f, 0.5f, 0f),
                TextManager.FontFace.boldSmall,
                PugTextStyle.VerticalAlignment.bottom,
                new Color(1f, 1f, 1f, 0.18039216f),
                NameSortingLayerName);

            text.renderOnStart = true;
            text.keepEnabledOnStart = true;
            text.alwaysUpdateDynamicTextPixelPos = false;

            // A boss's name is a localization term the author never types, so there is nothing for
            // the profanity filter to check and vanilla switches it off.
            text.checkForProfanity = false;
            text.localize = true;
            text.maxWidth = 5f;
            text.SetText(string.Empty);
            return text;
        }

        /// <summary>
        /// The empty object the text hangs under, because both of the game's floating-text
        /// components write to the text's PARENT transform without checking there is one.
        /// </summary>
        private static Transform MakeContainer(GameObject root, string name, Vector3 where)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(root.transform, false);
            container.transform.localPosition = where;
            return container.transform;
        }

        /// <summary>
        /// The PugText itself, on the layer the game draws world text on.
        /// </summary>
        /// <remarks>
        /// <c>usePooledResources</c> is on for every one of these in vanilla: the glyphs come from
        /// <c>Manager.text.glyphPool</c> rather than a fresh GameObject per letter, which is what
        /// keeps a room full of named chests from allocating. It also means the text must be
        /// rendered while the game is running — which is the only place any of this is ever
        /// rendered, so nothing is lost.
        /// </remarks>
        private static PugText MakeText(
            Transform container,
            string name,
            Vector3 where,
            TextManager.FontFace face,
            PugTextStyle.VerticalAlignment verticalAlignment,
            Color color,
            string sortingLayerName)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(container, false);
            textObject.transform.localPosition = where;
            textObject.layer = WorldUiLayer();

            PugText text = textObject.AddComponent<PugText>();

            // Explicit rather than left to the base class's field initialisers. These two gate
            // UIComponentMonoBehaviour.RenderUIComponent, and a component added by a generator has
            // no serialized prefab values behind it to fall back on.
            text.activeInPlatforms = (PlatformFlags)(-1);
            text.activeInStoreFronts = (StorefrontFlags)(-1);

            text.style.fontFace = face;
            text.style.horizontalAlignment = PugTextStyle.HorizontalAlignment.center;
            text.style.verticalAlignment = verticalAlignment;
            text.style.color = color;
            text.style.sortingLayer = SortingLayer.NameToID(sortingLayerName);
            text.style.orderInLayer = 9999;
            text.style.supportColorTags = false;
            text.style.maskInteraction = SpriteMaskInteraction.None;

            text.usePooledResources = true;
            text.freeResourcesOnDisable = false;
            text.localize = false;
            text.localizePlaceholders = false;
            text.maxWidth = 0f;

            return text;
        }

        /// <summary>
        /// The <c>WorldUI</c> layer, by name where the project has it and by measured index where
        /// the lookup fails.
        /// </summary>
        /// <remarks>
        /// A layer that does not resolve comes back as -1 from <c>LayerMask.NameToLayer</c>, and -1
        /// assigned to <c>GameObject.layer</c> throws. The fallback keeps a generator run from
        /// dying on a project setting rather than on anything the author did.
        /// </remarks>
        private static int WorldUiLayer()
        {
            int named = LayerMask.NameToLayer(WorldUiLayerName);
            return named >= 0 ? named : WorldUiLayerIndexFallback;
        }
    }
}
