using System.Collections.Generic;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// Every scene the installed mods have declared, gathered before the world exists so the table can
    /// be rebuilt in one pass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain static registry, matching how the framework's other generated content announces itself:
    /// a mod's generated bootstrap calls <see cref="Register"/> during load, long before any world is
    /// created, and the injector reads the whole set at the one moment the engine will accept a new
    /// scene table.
    /// </para>
    /// <para>
    /// Registration validates rather than trusts. A scene whose name cannot survive being queued, or
    /// that collides with one already registered, is refused with a reason — both failures are
    /// otherwise completely silent at runtime, and a silently-absent structure is very hard to trace
    /// back from "my dungeon room never appears".
    /// </para>
    /// </remarks>
    public static class DimensionCustomSceneRegistry
    {
        private static readonly List<DimensionCustomSceneDefinition> scenes =
            new List<DimensionCustomSceneDefinition>();

        private static readonly Dictionary<string, DimensionCustomSceneDefinition> byName =
            new Dictionary<string, DimensionCustomSceneDefinition>();

        /// <summary>Every registered scene, in registration order.</summary>
        public static IReadOnlyList<DimensionCustomSceneDefinition> All
        {
            get { return scenes; }
        }

        public static int Count
        {
            get { return scenes.Count; }
        }

        /// <summary>
        /// Adds a scene, or explains why it cannot be added.
        /// </summary>
        public static bool Register(DimensionCustomSceneDefinition definition, out string error)
        {
            if (definition == null)
            {
                error = "No scene was supplied.";
                return false;
            }

            if (!DimensionCustomSceneNames.IsValid(definition.SceneName, out error))
            {
                return false;
            }

            if (byName.ContainsKey(definition.SceneName))
            {
                error =
                    "A scene named '" + definition.SceneName + "' is already registered. Scenes are " +
                    "resolved by name, so two with the same name means one of them can never be placed; " +
                    "namespace it with your mod id.";
                return false;
            }

            if (definition.Tiles.Count == 0)
            {
                error =
                    "Scene '" + definition.SceneName + "' has no tiles. It would place nothing, and " +
                    "would still consume one of the world's scene slots.";
                return false;
            }

            scenes.Add(definition);
            byName.Add(definition.SceneName, definition);
            error = null;
            return true;
        }

        /// <summary>Registers a scene and logs the reason if it is refused.</summary>
        public static bool Register(DimensionCustomSceneDefinition definition)
        {
            string error;
            if (Register(definition, out error))
            {
                return true;
            }

            DimensionFrameworkLog.Warning("Scene not registered: " + error);
            return false;
        }

        public static bool TryGet(string sceneName, out DimensionCustomSceneDefinition definition)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                definition = null;
                return false;
            }

            return byName.TryGetValue(sceneName, out definition);
        }

        /// <summary>
        /// Forgets everything. Exists for tests; the runtime registers once per process.
        /// </summary>
        /// <summary>
        /// How many tiles across a registered scene is.
        /// </summary>
        /// <remarks>
        /// Measured from the tiles rather than stored, so it can never disagree with what the scene
        /// actually stamps. The dungeon generator needs it to reserve a room big enough — a size that
        /// under-reported would let the generator overlap two rooms and cut one of them in half.
        /// </remarks>
        public static bool TryGetSize(string sceneName, out Unity.Mathematics.int2 size)
        {
            size = default(Unity.Mathematics.int2);

            DimensionCustomSceneDefinition definition;
            if (!TryGet(sceneName, out definition) || definition.Tiles.Count == 0)
            {
                return false;
            }

            Unity.Mathematics.int2 min = definition.Tiles[0].LocalPosition;
            Unity.Mathematics.int2 max = min;

            for (int i = 1; i < definition.Tiles.Count; i++)
            {
                Unity.Mathematics.int2 position = definition.Tiles[i].LocalPosition;
                min = Unity.Mathematics.math.min(min, position);
                max = Unity.Mathematics.math.max(max, position);
            }

            // Inclusive extent: a scene occupying x 0..3 is four tiles wide, not three.
            size = max - min + new Unity.Mathematics.int2(1, 1);
            return true;
        }

        /// <summary>
        /// The smallest room radius that holds the whole scene around its own pivot.
        /// </summary>
        /// <remarks>
        /// Measured from the tiles against the scene's registered centre, so it is exact for
        /// L-shapes and off-centre pivots where a bounding-box diagonal would lie in either
        /// direction. This is the number a dungeon room must roll at or above for the scene to
        /// stay inside the room's spawn-block circle, spacing checks and corridor clearance.
        /// </remarks>
        public static bool TryGetFitRadius(string sceneName, out int fitRadius)
        {
            fitRadius = 0;

            DimensionCustomSceneDefinition definition;
            if (!TryGet(sceneName, out definition) || definition.Tiles.Count == 0)
            {
                return false;
            }

            fitRadius = DimensionSceneGeometry.FitRadius(definition.Tiles, definition.CenterPosition);
            return true;
        }

        public static void Clear()
        {
            scenes.Clear();
            byName.Clear();
        }
    }
}
