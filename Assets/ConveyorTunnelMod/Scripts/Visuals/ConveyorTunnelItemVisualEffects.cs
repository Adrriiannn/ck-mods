using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class ConveyorTunnelItemVisualEffects
{
  private enum EffectKind : byte
  {
    Intake = 0,
    Exit = 1
  }

  private struct ItemEffect
  {
    public EffectKind Kind;
    public float2 Point;
    public float RequestTime;
    public float StartTime;
    public Entity AssignedEntity;
    public byte Started;
  }

  public const float IntakeTakeDuration = 0.12f;
  public const float IntakeVisualStartDelay = 0.07f;
  public const float IntakeVisualDuration = 0.22f;
  public const float ExitDuration = 0.20f;

  private const float MatchRadius = 0.38f;
  private const float PendingEffectTimeout = 2.0f;
  private const float StartedIntakeHardTimeout = 10.0f;
  private const int MaxEffects = 128;

  private static readonly List<ItemEffect> _effects = new List<ItemEffect>();
  private static readonly HashSet<Entity> _modifiedEntities = new HashSet<Entity>();

  public static void RequestIntake(float2 point)
  {
    AddEffect(EffectKind.Intake, point);
  }

  public static void RequestExit(float2 point)
  {
    AddEffect(EffectKind.Exit, point);
  }

  public static void ApplyToDroppedItem(DroppedItem droppedItem)
  {
    if (droppedItem == null ||
        droppedItem.SR == null)
    {
      return;
    }

    float now = Time.time;
    PruneExpired(now);

    Entity entity = droppedItem.entity;
    if (!droppedItem.entityExist ||
        droppedItem.isHidden)
    {
      if (_modifiedEntities.Remove(entity))
      {
        ResetSpriteTransform(droppedItem);
      }

      return;
    }

    float2 itemPoint = new float2(droppedItem.WorldPosition.x, droppedItem.WorldPosition.z);
    if (TryFindMatchingEffect(
            EffectKind.Intake,
            entity,
            itemPoint,
            now,
            out float intakeProgress))
    {
      ApplyIntakeEffect(droppedItem, intakeProgress);
      _modifiedEntities.Add(entity);
      return;
    }

    if (TryFindMatchingEffect(
            EffectKind.Exit,
            entity,
            itemPoint,
            now,
            out float exitProgress))
    {
      ApplyExitEffect(droppedItem, exitProgress);
      _modifiedEntities.Add(entity);
      return;
    }

    if (_modifiedEntities.Remove(entity))
    {
      ResetSpriteTransform(droppedItem);
    }
  }

  public static void Update()
  {
    PruneExpired(Time.time);
  }

  public static void Clear()
  {
    _effects.Clear();
    _modifiedEntities.Clear();
  }

  private static void AddEffect(EffectKind kind, float2 point)
  {
    float now = Time.time;
    PruneExpired(now);

    if (_effects.Count >= MaxEffects)
    {
      _effects.RemoveAt(0);
    }

    _effects.Add(new ItemEffect
    {
      Kind = kind,
      Point = point,
      RequestTime = now,
      StartTime = 0.0f,
      AssignedEntity = Entity.Null,
      Started = 0
    });
  }

  private static bool TryFindMatchingEffect(
      EffectKind kind,
      Entity entity,
      float2 itemPoint,
      float now,
      out float progress)
  {
    progress = 0.0f;

    for (int i = 0; i < _effects.Count; i++)
    {
      ItemEffect effect = _effects[i];
      if (effect.Kind != kind ||
          effect.Started == 0 ||
          effect.AssignedEntity != entity)
      {
        continue;
      }

      float effectProgress = GetProgress(effect, now);
      if (effectProgress > 1.0f)
      {
        if (kind == EffectKind.Intake)
        {
          if (math.distancesq(itemPoint, effect.Point) <= MatchRadius * MatchRadius)
          {
            progress = 1.0f;
            return true;
          }

          _effects.RemoveAt(i);
          i--;
        }

        continue;
      }

      progress = math.max(0.0f, effectProgress);
      return true;
    }

    float bestDistanceSq = MatchRadius * MatchRadius;
    int bestIndex = -1;

    for (int i = 0; i < _effects.Count; i++)
    {
      ItemEffect effect = _effects[i];
      if (effect.Kind != kind ||
          effect.Started != 0)
      {
        continue;
      }

      float distanceSq = math.distancesq(itemPoint, effect.Point);
      if (distanceSq > bestDistanceSq)
      {
        continue;
      }

      bestDistanceSq = distanceSq;
      bestIndex = i;
    }

    if (bestIndex < 0)
    {
      return false;
    }

    ItemEffect startedEffect = _effects[bestIndex];
    startedEffect.Started = 1;
    startedEffect.AssignedEntity = entity;
    startedEffect.StartTime = kind == EffectKind.Intake
        ? now + IntakeVisualStartDelay
        : now;
    _effects[bestIndex] = startedEffect;
    progress = 0.0f;
    return true;
  }

  private static void ApplyIntakeEffect(DroppedItem droppedItem, float progress)
  {
    Transform spriteTransform = droppedItem.SR.transform;
    Vector3 basePosition = spriteTransform.localPosition;

    EvaluateExitPose(1.0f - progress, out float yOffset, out Vector3 scale);

    spriteTransform.localPosition = basePosition + new Vector3(0.0f, yOffset, 0.0f);
    spriteTransform.localScale = scale;
  }

  private static void ApplyExitEffect(DroppedItem droppedItem, float progress)
  {
    Transform spriteTransform = droppedItem.SR.transform;
    Vector3 basePosition = spriteTransform.localPosition;
    EvaluateExitPose(progress, out float yOffset, out Vector3 scale);

    spriteTransform.localPosition = basePosition + new Vector3(0.0f, yOffset, 0.0f);
    spriteTransform.localScale = scale;
  }

  private static void EvaluateExitPose(float progress, out float yOffset, out Vector3 scale)
  {
    progress = Mathf.Clamp01(progress);
    float eased = SmoothStep(progress);
    yOffset = Mathf.Lerp(-0.14f, 0.0f, eased);
    float overshoot = progress < 0.72f
        ? Mathf.Lerp(0.18f, 1.12f, SmoothStep(progress / 0.72f))
        : Mathf.Lerp(1.12f, 1.0f, SmoothStep((progress - 0.72f) / 0.28f));
    scale = new Vector3(
        Mathf.Lerp(0.76f, 1.0f, eased),
        overshoot,
        1.0f);
  }

  private static void ResetSpriteTransform(DroppedItem droppedItem)
  {
    if (droppedItem?.SR == null)
    {
      return;
    }

    droppedItem.SR.transform.localScale = Vector3.one;
  }

  private static void PruneExpired(float now)
  {
    for (int i = _effects.Count - 1; i >= 0; i--)
    {
      ItemEffect effect = _effects[i];
      bool expired;
      if (effect.Started == 0)
      {
        expired = now - effect.RequestTime > PendingEffectTimeout;
      }
      else if (effect.Kind == EffectKind.Intake)
      {
        expired = now - effect.StartTime > StartedIntakeHardTimeout;
      }
      else
      {
        expired = GetProgress(effect, now) > 1.0f;
      }

      if (expired)
      {
        _effects.RemoveAt(i);
      }
    }
  }

  private static float GetProgress(ItemEffect effect, float now)
  {
    float duration = effect.Kind == EffectKind.Intake
        ? IntakeVisualDuration
        : ExitDuration;
    return (now - effect.StartTime) / math.max(0.001f, duration);
  }

  private static float SmoothStep(float value)
  {
    value = Mathf.Clamp01(value);
    return value * value * (3.0f - 2.0f * value);
  }
}
