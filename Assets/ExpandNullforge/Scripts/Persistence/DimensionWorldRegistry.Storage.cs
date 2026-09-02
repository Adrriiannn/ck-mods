using System;
using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using Newtonsoft.Json;
using Pug.ECS.Components;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Persistence
{
  /// <summary>
  /// The two files on disk, which of them to trust, and the backup taken when neither reads.
  /// </summary>
  public static partial class DimensionWorldRegistry
  {
    private static RegistryEnvelope ChooseNewestValid(
        RegistryEnvelope a,
        RegistryEnvelope b,
        string worldKey)
    {
      bool validA = IsEnvelopeValid(a, worldKey);
      bool validB = IsEnvelopeValid(b, worldKey);
      if (validA && validB)
      {
        return a.generation >= b.generation ? a : b;
      }

      if (validA)
      {
        return a;
      }

      return validB ? b : null;
    }

    private static RegistryEnvelope TryReadEnvelope(string path, out byte[] raw)
    {
      raw = null;
      if (API.ConfigFilesystem == null || !API.ConfigFilesystem.FileExists(path))
      {
        return null;
      }

      raw = API.ConfigFilesystem.Read(path);
      if (raw == null || raw.Length == 0)
      {
        return null;
      }

      return JsonConvert.DeserializeObject<RegistryEnvelope>(Encoding.UTF8.GetString(raw));
    }

    private static bool IsEnvelopeValid(RegistryEnvelope envelope, string worldKey)
    {
      return envelope != null &&
             IsRegistrySchemaReadable(envelope.schemaVersion) &&
             envelope.schemaVersion <= DimensionRegistryConstants.RegistrySchemaVersion &&
             envelope.worldKey == worldKey &&
             !string.IsNullOrEmpty(envelope.payload) &&
             envelope.payloadChecksum == ComputeChecksum(envelope.payload);
    }

    private static bool TryReadPayloadFromChosenEnvelope(
        RegistryEnvelope chosen,
        RegistryEnvelope envelopeA,
        RegistryEnvelope envelopeB,
        string pathA,
        string pathB,
        byte[] rawA,
        byte[] rawB,
        string worldKey,
        out RegistryPayload payload,
        out long generation)
    {
      if (TryReadPayloadCandidate(
              chosen,
              ReferenceEquals(chosen, envelopeA) ? pathA : pathB,
              ReferenceEquals(chosen, envelopeA) ? rawA : rawB,
              worldKey,
              out payload,
              out generation))
      {
        return true;
      }

      RegistryEnvelope fallback = ReferenceEquals(chosen, envelopeA) ? envelopeB : envelopeA;
      if (fallback == null)
      {
        payload = null;
        generation = 0L;
        return false;
      }

      return TryReadPayloadCandidate(
          fallback,
          ReferenceEquals(fallback, envelopeA) ? pathA : pathB,
          ReferenceEquals(fallback, envelopeA) ? rawA : rawB,
          worldKey,
          out payload,
          out generation);
    }

    private static bool TryReadPayloadCandidate(
        RegistryEnvelope envelope,
        string path,
        byte[] raw,
        string worldKey,
        out RegistryPayload payload,
        out long generation)
    {
      payload = null;
      generation = 0L;
      if (!IsEnvelopeValid(envelope, worldKey))
      {
        return false;
      }

      try
      {
        RegistryPayload candidate =
            JsonConvert.DeserializeObject<RegistryPayload>(envelope.payload);
        if (candidate == null)
        {
          DimensionLog.Fatal(DimensionLogChannels.Persist, null, "Dimension registry payload was empty after deserialization.");
          TryWriteCorruptBackup(path, raw);
          return false;
        }

        if (!IsRegistrySchemaReadable(candidate.schemaVersion))
        {
          DimensionLog.Fatal(DimensionLogChannels.Persist, null, 
              "Dimension registry payload schema " +
              candidate.schemaVersion +
              " is not readable by this framework build. Current schema=" +
              DimensionRegistryConstants.RegistrySchemaVersion +
              ". Preserving the unreadable registry before starting clean.");
          TryWriteCorruptBackup(path, raw);
          return false;
        }

        payload = candidate;
        generation = envelope.generation;
        return true;
      }
      catch (Exception ex)
      {
        DimensionLog.Fatal(DimensionLogChannels.Persist, null, "Dimension registry payload failed to deserialize. " + ex);
        TryWriteCorruptBackup(path, raw);
        return false;
      }
    }

    private static bool IsRegistrySchemaReadable(int schemaVersion)
    {
      return schemaVersion >= DimensionRegistryConstants.RegistryMinimumReadableSchemaVersion &&
             schemaVersion <= DimensionRegistryConstants.RegistrySchemaVersion;
    }

    private static RegistryPayload NewPayload(string worldKey)
    {
      return new RegistryPayload
      {
        schemaVersion = DimensionRegistryConstants.RegistrySchemaVersion,
        worldKey = worldKey ?? string.Empty,
        registryRevision = 0,
        dimensions = new List<DimensionDefinitionRecord>(),
        dimensionSlots = new List<DimensionSlotPersistenceRecord>(),
        players = new List<DimensionPlayerStateRecord>(),
        playerVisits = new List<DimensionPlayerVisitStateRecord>(),
        portals = new List<DimensionPortalRecord>(),
        markers = new List<DimensionMarkerRecord>(),
        anchors = new List<DimensionAnchorRecord>(),
        scenes = new List<DimensionSceneRecord>(),
        progressFlags = new List<DimensionProgressFlagRecord>(),
        generatedAreas = new List<DimensionGeneratedAreaRecord>(),
        contentOwnership = new List<DimensionContentOwnershipRecord>()
      };
    }

    private static bool TryGetPaths(out string worldKey, out string pathA, out string pathB)
    {
      worldKey = null;
      pathA = null;
      pathB = null;

      if (API.ConfigFilesystem == null)
      {
        return false;
      }

      worldKey = GetCurrentStableWorldKey();
      if (string.IsNullOrWhiteSpace(worldKey))
      {
        return false;
      }

      GetPathsForWorldKey(worldKey, out pathA, out pathB);
      return true;
    }

    private static bool TryLoadSlotFallbackForStableWorldKey(
        string stableWorldKey,
        out RegistryPayload payload)
    {
      payload = null;
      if (string.IsNullOrWhiteSpace(stableWorldKey) ||
          !stableWorldKey.StartsWith("guid-", StringComparison.Ordinal) ||
          API.ConfigFilesystem == null)
      {
        return false;
      }

      string slotWorldKey = GetSlotWorldKey();
      if (string.IsNullOrWhiteSpace(slotWorldKey) ||
          string.Equals(slotWorldKey, stableWorldKey, StringComparison.Ordinal))
      {
        return false;
      }

      string fallbackPathA;
      string fallbackPathB;
      GetPathsForWorldKey(slotWorldKey, out fallbackPathA, out fallbackPathB);

      byte[] fallbackRawA;
      byte[] fallbackRawB;
      RegistryEnvelope fallbackEnvelopeA = TryReadEnvelope(fallbackPathA, out fallbackRawA);
      RegistryEnvelope fallbackEnvelopeB = TryReadEnvelope(fallbackPathB, out fallbackRawB);
      RegistryEnvelope fallbackChosen =
          ChooseNewestValid(fallbackEnvelopeA, fallbackEnvelopeB, slotWorldKey);
      if (fallbackChosen == null)
      {
        return false;
      }

      long fallbackGeneration;
      if (!TryReadPayloadFromChosenEnvelope(
              fallbackChosen,
              fallbackEnvelopeA,
              fallbackEnvelopeB,
              fallbackPathA,
              fallbackPathB,
              fallbackRawA,
              fallbackRawB,
              slotWorldKey,
              out payload,
              out fallbackGeneration))
      {
        payload = null;
        return false;
      }

      DimensionFrameworkLog.Verbose(
          "Migrating provisional dimension registry from " +
          slotWorldKey +
          " to stable world key " +
          stableWorldKey +
          ".");
      return true;
    }

    private static void GetPathsForWorldKey(string worldKey, out string pathA, out string pathB)
    {
      string safeWorldKey = SanitizeFilePart(worldKey);
      pathA = DimensionRegistryConstants.FilePrefix + safeWorldKey + DimensionRegistryConstants.FileSuffixA;
      pathB = DimensionRegistryConstants.FilePrefix + safeWorldKey + DimensionRegistryConstants.FileSuffixB;
    }

    private static string GetCurrentStableWorldKey()
    {
      World world = API.Server != null ? API.Server.World : null;
      if ((world == null || !world.IsCreated) && Manager.ecs != null)
      {
        world = Manager.ecs.ServerWorld;
      }

      if (world != null && world.IsCreated)
      {
        try
        {
          EntityManager entityManager = world.EntityManager;
          using (EntityQuery query =
              entityManager.CreateEntityQuery(ComponentType.ReadOnly<ServerGuidCD>()))
          {
            if (query.CalculateEntityCount() > 0)
            {
              ServerGuidCD guid = query.GetSingleton<ServerGuidCD>();
              string value = guid.Value.ToString();
              if (!string.IsNullOrWhiteSpace(value))
              {
                return "guid-" + value;
              }
            }
          }
        }
        catch (Exception ex)
        {
          DimensionLog.Problem(DimensionLogChannels.Persist, null, 
              "Could not read stable world GUID yet: " +
              ex.Message);
        }
      }

      return null;
    }

    private static string GetSlotWorldKey()
    {
      return Manager.saves != null
          ? "slot-" + Manager.saves.GetWorldId()
          : null;
    }

    private static string ComputeChecksum(string value)
    {
      const ulong offset = 14695981039346656037UL;
      const ulong prime = 1099511628211UL;
      ulong hash = offset;
      byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
      for (int i = 0; i < bytes.Length; i++)
      {
        hash ^= bytes[i];
        hash *= prime;
      }

      return hash.ToString("X16");
    }

    private static string SanitizeFilePart(string value)
    {
      if (string.IsNullOrEmpty(value))
      {
        return string.Empty;
      }

      StringBuilder builder = new StringBuilder(value.Length);
      for (int i = 0; i < value.Length; i++)
      {
        char c = value[i];
        builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
      }

      return builder.ToString();
    }

    private static string SanitizeName(string value, int maxCharacters, string fallback)
    {
      if (string.IsNullOrEmpty(value))
      {
        return fallback ?? string.Empty;
      }

      StringBuilder builder = new StringBuilder(Math.Min(value.Length, maxCharacters));
      for (int i = 0; i < value.Length && builder.Length < maxCharacters; i++)
      {
        char c = value[i];
        if (!char.IsControl(c))
        {
          builder.Append(c);
        }
      }

      string result = builder.ToString().Trim();
      return string.IsNullOrEmpty(result) ? fallback ?? string.Empty : result;
    }

    private static void TryWriteCorruptBackup(string path, byte[] raw)
    {
      if (raw == null || raw.Length == 0 || API.ConfigFilesystem == null)
      {
        return;
      }

      try
      {
        API.ConfigFilesystem.Write(path + ".corrupt-" + DateTime.UtcNow.Ticks, raw);
      }
      catch (Exception ex)
      {
        DimensionLog.Problem(DimensionLogChannels.Persist, null, "Could not preserve corrupt dimension registry: " + ex.Message);
      }
    }
  }
}
