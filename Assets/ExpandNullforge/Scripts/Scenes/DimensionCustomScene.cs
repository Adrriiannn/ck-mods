using System;
using System.Collections.Generic;
using ExpandNullforge.Zones;
using PugTilemap;
using Unity.Collections;
using Unity.Mathematics;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// A patch of a scene's floor that does something when stepped on, in the scene's own local
    /// coordinates.
    /// </summary>
    /// <remarks>
    /// Scene-local on purpose: the scene does not know where it will land. The placement pass turns
    /// the patch into per-tile <see cref="DimensionTriggeredTileRegistry"/> registrations at the
    /// moment it stamps the scene and therefore knows the anchor. Which is also the known limit —
    /// a scene stamped into a generated dungeon room goes through Core Keeper's own machinery,
    /// which reports no position, so its triggers never arm.
    /// </remarks>
    public readonly struct DimensionSceneTrigger
    {
        public DimensionSceneTrigger(
            string triggerId,
            int2 localMin,
            int2 localMaxExclusive,
            DimensionTileTrigger kind,
            string carriedItemName,
            DimensionTileAction action,
            string actionTarget,
            int amount,
            float conditionSeconds,
            float cooldownSeconds,
            bool onceOnly)
        {
            TriggerId = triggerId ?? string.Empty;
            LocalMin = localMin;
            LocalMaxExclusive = math.max(localMaxExclusive, localMin + new int2(1, 1));
            Kind = kind;
            CarriedItemName = carriedItemName ?? string.Empty;
            Action = action;
            ActionTarget = actionTarget ?? string.Empty;
            Amount = amount < 1 ? 1 : amount;
            ConditionSeconds = conditionSeconds < 0f ? 0f : conditionSeconds;
            CooldownSeconds = cooldownSeconds < 0f ? 0f : cooldownSeconds;
            OnceOnly = onceOnly;
        }

        public readonly string TriggerId;

        /// <summary>The covered patch, on the same local grid the scene's tiles use.</summary>
        public readonly int2 LocalMin;

        public readonly int2 LocalMaxExclusive;

        public readonly DimensionTileTrigger Kind;

        /// <summary>The item a carrying trigger looks for, by name.</summary>
        public readonly string CarriedItemName;

        public readonly DimensionTileAction Action;

        /// <summary>The creature to summon or the condition to apply, by name.</summary>
        public readonly string ActionTarget;

        /// <summary>Creatures summoned, condition stacks, or damage dealt.</summary>
        public readonly int Amount;

        /// <summary>How long an applied condition lasts, in seconds.</summary>
        public readonly float ConditionSeconds;

        public readonly float CooldownSeconds;

        public readonly bool OnceOnly;
    }

    /// <summary>One tile a scene places, in the scene's own local coordinates.</summary>
    public readonly struct DimensionSceneTile
    {
        public DimensionSceneTile(int2 localPosition, int tileset, TileType tileType)
        {
            LocalPosition = localPosition;
            Tileset = tileset;
            TileType = tileType;
        }

        public readonly int2 LocalPosition;

        /// <summary>
        /// Full-width tileset id, custom ids included.
        /// </summary>
        /// <remarks>
        /// This is <c>int</c> rather than <c>ushort</c> on purpose, and it is the whole reason scenes
        /// can carry custom blocks at all. Core Keeper's authoring container narrows a tile to
        /// <c>PugmapTileData</c>'s 16-bit tileset, but the runtime never sees that type — the scene
        /// blob holds <c>TileCD</c>, which is int-width, and the placement call writes the whole
        /// <c>TileCD</c> through. Building the blob ourselves skips the authoring container entirely
        /// and custom ids ride through untouched.
        /// </remarks>
        public readonly int Tileset;

        public readonly TileType TileType;
    }

    /// <summary>
    /// A structure a dimension can place: a named grid of tiles Core Keeper's own scene machinery
    /// knows how to stamp into the world.
    /// </summary>
    public sealed class DimensionCustomSceneDefinition
    {
        public DimensionCustomSceneDefinition(
            string sceneName,
            IReadOnlyList<DimensionSceneTile> tiles,
            int2 centerPosition = default,
            bool canFlipX = false,
            bool canFlipY = false,
            int maxOccurrences = 0,
            IReadOnlyList<DimensionSceneObject> objects = null,
            IReadOnlyList<Biome> overworldBiomes = null,
            int minDistanceFromCoreInClassicWorlds = 0,
            IReadOnlyList<DimensionSceneTrigger> triggers = null)
        {
            SceneName = sceneName ?? string.Empty;
            Tiles = tiles ?? Array.Empty<DimensionSceneTile>();
            CenterPosition = centerPosition;
            CanFlipX = canFlipX;
            CanFlipY = canFlipY;
            MaxOccurrences = maxOccurrences;
            Objects = objects ?? Array.Empty<DimensionSceneObject>();
            OverworldBiomes = overworldBiomes ?? Array.Empty<Biome>();
            MinDistanceFromCoreInClassicWorlds = minDistanceFromCoreInClassicWorlds;
            Triggers = triggers ?? Array.Empty<DimensionSceneTrigger>();
        }

        /// <summary>
        /// The name everything resolves this scene by.
        /// </summary>
        /// <remarks>
        /// Every lookup in the engine is by name, never by index — which is what makes appending our
        /// scenes to the table safe. It also means the name is the whole contract, so it has to be
        /// unique across installed mods and short enough to survive the queue (see
        /// <see cref="DimensionCustomSceneNames"/>).
        /// </remarks>
        public readonly string SceneName;

        public readonly IReadOnlyList<DimensionSceneTile> Tiles;

        /// <summary>
        /// The chests, statues, torches and other placed things this scene brings with it.
        /// </summary>
        /// <remarks>
        /// Tiles make a room; objects make it worth entering. Held by NAME rather than id, because a
        /// name can only be resolved to a prefab once a world exists — see the injector.
        /// </remarks>
        public readonly IReadOnlyList<DimensionSceneObject> Objects;

        /// <summary>Where the scene considers its own origin, in local coordinates.</summary>
        public readonly int2 CenterPosition;

        /// <summary>Whether world generation may mirror the scene when it places it.</summary>
        public readonly bool CanFlipX;

        public readonly bool CanFlipY;

        /// <summary>
        /// How many times the game's OWN placer may grow this scene per world.
        /// </summary>
        /// <remarks>
        /// Zero does not mean "no cap" — it means invisible: the game's availability list only
        /// admits scenes with a positive count, so a zero here keeps the scene out of natural
        /// Overworld spawning entirely. Explicit placement and dungeon rooms ignore this.
        /// </remarks>
        public readonly int MaxOccurrences;

        /// <summary>
        /// The vanilla Overworld biomes this scene may naturally grow in, when it opts in.
        /// </summary>
        /// <remarks>
        /// VANILLA biomes only, on purpose: the game's biome sampler only ever produces vanilla
        /// values in the Overworld, so a custom biome id written here would be dead weight
        /// masquerading as configuration. Custom-dimension placement never reads this — the
        /// framework's own scene pass places scenes there.
        /// </remarks>
        public readonly IReadOnlyList<Biome> OverworldBiomes;

        /// <summary>How close to the Core this may naturally spawn, in classic worlds.</summary>
        public readonly int MinDistanceFromCoreInClassicWorlds;

        /// <summary>
        /// The stepped-on patches this scene arms where it lands, in scene-local coordinates.
        /// </summary>
        /// <remarks>
        /// Carried here rather than in a registry of their own so a trigger travels with the scene
        /// the same way its objects do — the placement pass reads them off the definition at stamp
        /// time, when the anchor is finally known.
        /// </remarks>
        public readonly IReadOnlyList<DimensionSceneTrigger> Triggers;
    }

    /// <summary>
    /// The one rule a scene name has to satisfy, and why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE TRAP. A scene blob stores its name in a <c>FixedString64Bytes</c>, but the component that
    /// queues a spawn stores it in a <c>FixedString32Bytes</c>, and Core Keeper narrows 64 to 32 when
    /// it queues. A name that fits the blob but not the queue therefore registers fine, appears in
    /// the table, and then simply never spawns — no error, no log line, nothing to search for. The
    /// smaller of the two is the real limit, so it is the one enforced here, at registration, where
    /// the author can still do something about it.
    /// </para>
    /// <para>
    /// The limit is measured in UTF-8 bytes, not characters: a name that fits in 29 characters can
    /// still overflow if any of them are non-ASCII.
    /// </para>
    /// </remarks>
    public static class DimensionCustomSceneNames
    {
        /// <summary>The usable payload of the queue's <c>FixedString32Bytes</c>, in UTF-8 bytes.</summary>
        public static int MaxNameBytes
        {
            get { return FixedString32Bytes.UTF8MaxLengthInBytes; }
        }

        /// <summary>
        /// Whether <paramref name="sceneName"/> can survive being queued for a spawn.
        /// </summary>
        public static bool IsValid(string sceneName, out string error)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                error = "A scene needs a name; everything that places it resolves it by name.";
                return false;
            }

            int bytes = Utf8ByteCount(sceneName);
            if (bytes > MaxNameBytes)
            {
                error =
                    "Scene name '" + sceneName + "' is " + bytes + " bytes, over the " + MaxNameBytes +
                    " a spawn request can hold. It would register and then never spawn, with nothing " +
                    "logged. Shorten it.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// UTF-8 length without allocating an encoder, so this is cheap enough to call per scene during
        /// registration.
        /// </summary>
        public static int Utf8ByteCount(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int bytes = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c < 0x80)
                {
                    bytes += 1;
                }
                else if (c < 0x800)
                {
                    bytes += 2;
                }
                else if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    // One code point spread over two chars encodes to four bytes, not two lots of three.
                    bytes += 4;
                    i++;
                }
                else
                {
                    bytes += 3;
                }
            }

            return bytes;
        }
    }
}
