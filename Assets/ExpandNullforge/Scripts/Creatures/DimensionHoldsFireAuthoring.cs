using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Marks a creature that will not start a fight but will finish one: the Defensive temper.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core Keeper decides "may I start chasing this?" against
    /// <c>ChaseStateCD.chaseAtDistanceSq</c>, EXCEPT for whoever hit the creature last, who is
    /// always measured against a forced 400 (twenty tiles squared) —
    /// <c>num9 = flag9 ? 400f : chaseStateCD.chaseAtDistanceSq;</c> then
    /// <c>if (flag10) num9 = 400f;</c> at
    /// <c>ck-db\Pug.Other\ChaseStateRequest.cs:224-228</c>, where <c>flag10</c> is the candidate
    /// that came out of <c>LastAttackerCD</c>. The chase, once entered, is held open against the
    /// same forced 400 (<c>ck-db\Pug.Other\ChaseStateSystem.cs:384-386</c>), so the retaliation
    /// does not evaporate on the next tick. A chase distance of zero therefore means exactly
    /// "never notices anyone, always answers the person who hit it", which is the temper.
    /// </para>
    /// <para>
    /// So why is this a RUNTIME component rather than a zero baked into the prefab? Because the
    /// authored <c>chaseAtDistance</c> feeds two different things through one converter, and only
    /// one of them should be zero. <c>ChaseStateConverter</c> passes it to
    /// <c>PathFindingConversion.CreatePathfindingEntity</c>, which sets
    /// <c>PathFindCD.searchRadius = ceil(chaseAtDistance)</c>
    /// (<c>ck-db\Pug.ECS.Conversion\PathFindingConversion.cs:12</c>). A search radius of zero
    /// makes the path search refuse to expand past the creature's own tile —
    /// <c>if (!math.any(math.abs(int2 - startPosition) &gt; searchRadius) ...)</c> at
    /// <c>ck-db\Pug.Other\PathFindSystem.cs:757</c> — so no path can ever exist. A defender that
    /// also asks for "needs a path to chase" then deadlocks outright: it cannot enter the chase
    /// without a path and cannot build a path without a chase, and every vanilla cattle prefab
    /// ships with exactly that flag set. Baking the zero bought a working aggro gate at the cost
    /// of a creature that could not walk.
    /// </para>
    /// <para>
    /// The prefab therefore keeps a real, sensible chase distance — the path search is sized from
    /// it correctly — and this component zeroes the aggro gate alone, once, on the server. Writing
    /// <c>chaseAtDistanceSq</c> at runtime is the game's own move, not an invention:
    /// <c>EnemySpawnerPlatformSystem.cs:278</c> does the same thing in the opposite direction to
    /// widen an arena spawn's aggro to twenty tiles.
    /// </para>
    /// </remarks>
    public sealed class DimensionHoldsFireAuthoring : MonoBehaviour
    {
    }
}
