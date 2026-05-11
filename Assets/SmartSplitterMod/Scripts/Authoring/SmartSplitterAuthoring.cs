using Unity.Entities;
using UnityEngine;

public class SmartSplitterAuthoring : MonoBehaviour
{
}

public class SmartSplitterBaker : Baker<SmartSplitterAuthoring>
{
  public override void Bake(SmartSplitterAuthoring authoring)
  {
    Entity entity = GetEntity(TransformUsageFlags.Dynamic);
    AddComponent<SmartSplitterTag>(entity);
  }
}
