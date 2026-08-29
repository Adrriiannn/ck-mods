using ExpandNullforge.Api;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// Answers item travel requirements from the portal's own offering slots.
  /// </summary>
  /// <remarks>
  /// <para>
  /// THIS CLOSES A HOLE THAT USED TO REFUSE EVERYONE. Item requirements could be authored and were
  /// registered faithfully, but nothing shipped that could answer them — so the access provider
  /// denied every travel through an item-gated portal, silently. The rule now: an item requirement
  /// is met when the portal's offering slot for that item holds the required amount.
  /// </para>
  /// <para>
  /// The evaluator never touches entities. The server system keeps the ledger current every tick;
  /// this reads it on the same main thread the travel API runs on.
  /// </para>
  /// </remarks>
  public sealed class DimensionPortalOfferingRequirementEvaluator :
      IDimensionTravelRequirementEvaluator
  {
    public const string EvaluatorId = "expandnullforge:portal-offering-requirements";

    public string ProviderId
    {
      get { return EvaluatorId; }
    }

    public int Priority
    {
      get { return -1000; }
    }

    public bool TryEvaluateTravelRequirement(
        DimensionTravelRequirementEvaluationContext context,
        out DimensionTravelRequirementEvaluationResult result)
    {
      DimensionTravelRequirementDefinition requirement = context.Requirement;
      if (requirement.Kind != DimensionTravelRequirementKind.Item)
      {
        result = default(DimensionTravelRequirementEvaluationResult);
        return false;
      }

      string portalId = context.TravelContext.PortalId;
      DimensionPortalOfferingLedger.ItemState state;
      if (!DimensionPortalOfferingLedger.TryGetItem(portalId, requirement.SubjectId, out state))
      {
        // The portal has no offering slot for this item — either it was generated before offering
        // slots existed, or the requirement names an item the slots do not. Refusing with a plain
        // reason beats the old silent refusal; Review also flags this at build time.
        result = DimensionTravelRequirementEvaluationResult.Unmet(
            requirement,
            ProviderId,
            "portal-offering-slot-missing",
            "This portal has no offering slot for what it requires. Rebuild the dimension so the " +
            "portal gains its offering window.");
        return true;
      }

      int required = state.Required < 1 ? 1 : state.Required;
      if (state.Offered >= required)
      {
        result = DimensionTravelRequirementEvaluationResult.Met(
            requirement,
            ProviderId,
            "The portal's offering slot holds what it asks for.");
        return true;
      }

      string itemName = string.IsNullOrEmpty(requirement.DisplayName)
          ? requirement.SubjectId
          : requirement.DisplayName;
      result = DimensionTravelRequirementEvaluationResult.Unmet(
          requirement,
          ProviderId,
          "portal-offering-incomplete",
          string.IsNullOrEmpty(requirement.FailureMessage)
              ? "The portal asks for " + required + "× " + itemName + " (" +
                state.Offered + " offered)."
              : requirement.FailureMessage);
      return true;
    }
  }
}
