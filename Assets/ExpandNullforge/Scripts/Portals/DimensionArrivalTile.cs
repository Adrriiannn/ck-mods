using PugTilemap;
using Pug.UnityExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// Finds a tile a player can actually stand on near where a portal wants to drop them.
  /// </summary>
  /// <remarks>
  /// <para>
  /// WHY THIS EXISTS. Core Keeper never checks. A vanilla portal's arrival tile is just the map
  /// marker's transform plus a fixed offset, and <c>TriggerUIActionsSystem</c> writes whatever
  /// <c>float2</c> it is handed straight into <c>TeleportingStateCD.targetPosition</c> with no
  /// walkability test anywhere along the way. Vanilla gets away with it because its portals are
  /// hand-placed by a level designer or by the player. Ours are placed programmatically, into
  /// procedurally generated terrain, at coordinates a dimension author typed into a field — so the
  /// destination genuinely can be solid rock, and the player would arrive standing inside it.
  /// </para>
  /// <para>
  /// WHAT "STANDABLE" MEANS HERE. Two conditions, both required, because they catch different
  /// failures:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>
  ///     Some layer of the cell is a <c>TileTypeUtility.IsWalkableTile</c> — the 13 floor-like types.
  ///     This rejects the void: an ungenerated or emptied cell has no ground at all, and "not
  ///     blocked" is not the same as "has a floor".
  ///   </description></item>
  ///   <item><description>
  ///     No layer of the cell is a <c>TileTypeUtility.IsBlockingTile</c> (low colliders included).
  ///     This rejects walls, water, pits, fences and big roots. It is the load-bearing half: a wall
  ///     cell normally still carries ground underneath it, so checking only for a floor would happily
  ///     accept solid rock.
  ///   </description></item>
  /// </list>
  /// <para>
  /// UNKNOWN IS NOT FREE. <c>TileAccessor.IsInitialized</c> false means the chunk has not streamed in,
  /// not that the cell is empty. The framework's own item portal treats uninitialized as free, which
  /// is right for spawning a decoration but wrong for landing a player, so this reports "no answer"
  /// instead and the caller leaves the requested position alone. Travel retries every 1.5s, so a
  /// destination that is merely late to stream gets checked again on the next attempt.
  /// </para>
  /// <para>
  /// NEVER BLOCKS TRAVEL. If nothing standable is found the caller keeps the original position — the
  /// same place vanilla would have put them. Arriving inside a wall is recoverable (the player digs
  /// out); a travel that refuses to complete is a dead end.
  /// </para>
  /// <para>
  /// <c>RoundToInt2</c> is the game's own world-position-to-tile conversion (round, not floor), used
  /// everywhere from the pathfinder to entity behaviours; matching it means our idea of "which tile
  /// is the player on" is the same as the engine's.
  /// </para>
  /// </remarks>
  public static class DimensionArrivalTile
  {
    /// <summary>How far out to look, in tiles, before giving up.</summary>
    /// <remarks>
    /// Four rings is 80 candidate cells. Far enough to escape a wall a portal was placed against or a
    /// small rock formation, close enough that the player still arrives where the author meant. A
    /// destination buried deeper than this is an authoring mistake worth surfacing, not one worth
    /// silently teleporting several screens away from.
    /// </remarks>
    public const int SearchRadius = 4;

    /// <summary>
    /// Nudges <paramref name="desired"/> onto a standable tile, and reports whether it had to move.
    /// </summary>
    /// <param name="world">The server world (the only place tiles are authoritative).</param>
    /// <param name="desired">The position the portal asked for.</param>
    /// <param name="resolved">
    /// Where the player should actually land. Equal to <paramref name="desired"/> whenever the
    /// destination is already fine, or whenever no answer could be had.
    /// </param>
    /// <returns>True only when the position was moved.</returns>
    public static bool TryResolveStandable(World world, float2 desired, out float2 resolved)
    {
      resolved = desired;
      if (world == null || !world.IsCreated)
      {
        return false;
      }

      PugQuerySystem querySystem = world.GetExistingSystemManaged<PugQuerySystem>();
      if (querySystem == null)
      {
        return false;
      }

      TileAccessor tiles = new TileAccessor(querySystem);
      int2 origin = desired.RoundToInt2();

      // Already fine, or the chunk has not streamed in yet: either way, do not move the player.
      Standability here = Classify(tiles, origin);
      if (here != Standability.Blocked)
      {
        return false;
      }

      int2 replacement;
      if (!TryFindNearbyStandable(tiles, origin, out replacement))
      {
        return false;
      }

      // Sub-tile precision is meaningless once we have moved cells, and a tile's centre is the
      // safest point inside it.
      resolved = new float2(replacement.x, replacement.y);
      return true;
    }

    /// <summary>
    /// Every cell within <see cref="SearchRadius"/> of the origin, closest ring first, excluding the
    /// origin itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built once and read in order, so the search is allocation-free at runtime and — more
    /// importantly — the order is fixed data rather than an emergent property of a loop. The same
    /// destination in the same terrain resolves to the same tile on every machine and every run,
    /// which is what lets a host and a rejoining client agree on where a player is standing.
    /// </para>
    /// <para>
    /// Ring order is by Chebyshev distance, matching how the tiles are searched outward: every cell
    /// of ring 1 is considered before any cell of ring 2. Within a ring the order is arbitrary but
    /// fixed; no cell is closer than another by any measure that matters at this scale.
    /// </para>
    /// </remarks>
    public static readonly int2[] SearchOffsets = BuildSearchOffsets(SearchRadius);

    private static int2[] BuildSearchOffsets(int radius)
    {
      int span = radius * 2 + 1;
      int2[] offsets = new int2[span * span - 1];
      int count = 0;
      for (int ring = 1; ring <= radius; ring++)
      {
        for (int y = -ring; y <= ring; y++)
        {
          for (int x = -ring; x <= ring; x++)
          {
            // Only the ring's edge is new; its interior was covered by earlier, closer rings.
            if (math.abs(x) == ring || math.abs(y) == ring)
            {
              offsets[count++] = new int2(x, y);
            }
          }
        }
      }

      return offsets;
    }

    /// <summary>
    /// Returns the first standable tile in search order, so the player lands as close to the author's
    /// intent as the terrain allows.
    /// </summary>
    private static bool TryFindNearbyStandable(TileAccessor tiles, int2 origin, out int2 found)
    {
      for (int i = 0; i < SearchOffsets.Length; i++)
      {
        int2 candidate = origin + SearchOffsets[i];
        if (Classify(tiles, candidate) == Standability.Standable)
        {
          found = candidate;
          return true;
        }
      }

      found = origin;
      return false;
    }

    private enum Standability
    {
      /// <summary>The chunk is not loaded; the cell's contents are genuinely unknown.</summary>
      Unknown,

      /// <summary>Has a floor and nothing blocking it.</summary>
      Standable,

      /// <summary>A wall, water, a pit, a fence, a big root, or bare void with no floor at all.</summary>
      Blocked
    }

    private static Standability Classify(TileAccessor tiles, int2 position)
    {
      if (!tiles.IsInitialized(position))
      {
        return Standability.Unknown;
      }

      NativeArray<TileCD> layers = tiles.Get(position, Allocator.Temp);
      bool hasFloor = false;
      bool blocked = false;
      for (int i = 0; i < layers.Length; i++)
      {
        TileType type = layers[i].tileType;
        if (TileTypeUtility.IsBlockingTile(type))
        {
          blocked = true;
          break;
        }

        if (TileTypeUtility.IsWalkableTile(type))
        {
          hasFloor = true;
        }
      }

      layers.Dispose();
      return !blocked && hasFloor ? Standability.Standable : Standability.Blocked;
    }
  }
}
