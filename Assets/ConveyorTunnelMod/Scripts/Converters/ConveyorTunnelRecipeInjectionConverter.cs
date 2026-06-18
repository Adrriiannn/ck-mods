using Pug.Conversion;
using PugMod;
using UnityEngine.Scripting;

[Preserve]
public class ConveyorTunnelRecipeInjectionConverter : SingleAuthoringComponentConverter<CraftingAuthoring>
{
  private static readonly ObjectID AutomationTableObjectID = (ObjectID)4022;

  protected override void Convert(CraftingAuthoring authoring)
  {
    if ((ObjectID)ObjectIndex != AutomationTableObjectID)
    {
      return;
    }

    ObjectID tunnelObjectID = API.Authoring.GetObjectID(ConveyorTunnelIds.ObjectName);
    if (tunnelObjectID == ObjectID.None)
    {
      return;
    }

    EnsureHasBuffer<CanCraftObjectsBuffer>();
    AddToBuffer<CanCraftObjectsBuffer>(new CanCraftObjectsBuffer
    {
      objectID = tunnelObjectID,
      amount = 2,
      craftingTimeOverride = 2f
    });
  }
}
