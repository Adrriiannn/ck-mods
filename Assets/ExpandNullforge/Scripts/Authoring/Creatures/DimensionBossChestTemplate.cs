using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The chest a boss leaves behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>BossAuthoring</c> has been attached to every generated boss with <b>nothing on it set</b>,
    /// which meant a custom boss could not leave a treasure chest — the single most recognisable
    /// thing a Core Keeper boss does. The component's presence made it look handled.
    /// </para>
    /// <para>
    /// The chest is a real object the boss names, not loot: it is placed in the world where the boss
    /// died, and the player opens it. That is why it takes an object id and an offset rather than a
    /// loot table — what is <i>inside</i> it is the chest's own business, authored on the container.
    /// </para>
    /// <para>
    /// <c>spawnOptionalChest</c> and <c>optionalChestVersion</c> are set by <b>zero</b> vanilla
    /// prefabs. They are not offered, on the same principle as the projectile's
    /// <c>shatterOnCollision</c>: a whole sub-mechanism with no user anywhere in the game.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionBossChestTemplate
    {
        [Tooltip("The chest it leaves where it died. One of the game's chests, or one of yours. Empty means it leaves none.")]
        [SerializeField] private string chestObjectId = string.Empty;

        [Tooltip("Which variation of that chest.")]
        [Min(0)]
        [SerializeField] private int chestVariation;

        [Tooltip("How many.")]
        [Min(1)]
        [SerializeField] private int chestAmount = 1;

        [Tooltip("Where the chest appears, relative to where the boss stood.")]
        [SerializeField] private Vector3 chestOffset = Vector3.zero;

        [Tooltip("It is one of the game's main story bosses, which changes how progression treats it.")]
        [SerializeField] private bool isAMainStoryBoss;

        [Tooltip("It also leaves a second, optional chest.")]
        [SerializeField] private bool leavesASecondChest;

        [Tooltip("What that second chest is. One of the game's chests, or one of yours.")]
        [SerializeField] private string secondChestObjectId = string.Empty;

        [Tooltip("Which look that second chest uses.")]
        [Min(0)]
        [SerializeField] private int secondChestVariation;

        public bool LeavesASecondChest { get { return leavesASecondChest; } }

        public string SecondChestObjectId { get { return secondChestObjectId ?? string.Empty; } }

        public int SecondChestVariation { get { return secondChestVariation < 0 ? 0 : secondChestVariation; } }

        public string ChestObjectId
        {
            get { return chestObjectId ?? string.Empty; }
        }

        public bool LeavesAChest
        {
            get { return !string.IsNullOrEmpty(ChestObjectId); }
        }

        public int ChestVariation
        {
            get { return chestVariation < 0 ? 0 : chestVariation; }
        }

        public int ChestAmount
        {
            get { return chestAmount < 1 ? 1 : chestAmount; }
        }

        public Vector3 ChestOffset
        {
            get { return chestOffset; }
        }

        public bool IsAMainStoryBoss
        {
            get { return isAMainStoryBoss; }
        }
    }
}
