namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What a boss does when a fight reaches one of its phases.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately a short list of things Core Keeper already does. A phase that could run arbitrary
    /// effects would let an author build something the game cannot render or explain — a status the UI
    /// has no icon for, a summon with no death animation. Restricting phases to the game's own verbs
    /// is what keeps an authored fight feeling like part of Core Keeper.
    /// </para>
    /// <para>
    /// Mirrored from the runtime enum rather than shared with it so the authoring asset does not
    /// depend on a runtime type whose numbering could shift; the two are checked against each other by
    /// a test.
    /// </para>
    /// </remarks>
    public enum DimensionBossPhaseActionKind
    {
        /// <summary>Nothing but the phase change itself, for a purely announced beat.</summary>
        None = 0,

        /// <summary>Spawn creatures around the boss.</summary>
        SummonAdds = 1,

        /// <summary>Apply one of the game's conditions to the boss.</summary>
        ApplyConditionToSelf = 2,

        /// <summary>Apply one of the game's conditions to everyone fighting it.</summary>
        ApplyConditionToPlayers = 3,

        /// <summary>Restore some of the boss's health.</summary>
        Heal = 4
    }
}
