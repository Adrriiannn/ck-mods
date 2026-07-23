using Unity.Mathematics;
using UnityEngine;

public sealed class ChunkLoaderToolkitSnapshot
{
  private Texture2D _texture;
  private ChunkCoordinate _coordinate;
  private bool _hasMap;

  public void Render(
      UnityEngine.UIElements.Image target,
      ChunkLoaderDetailsData data,
      ChunkCoordinate? coordinate)
  {
    if (target == null)
    {
      return;
    }
    if (!coordinate.HasValue)
    {
      target.image = null;
      return;
    }

    target.image = BuildTexture(data, coordinate.Value);
  }

  public void ForceRefreshMap(
      UnityEngine.UIElements.Image target,
      ChunkCoordinate coordinate)
  {
    if (target != null)
    {
      target.image = BuildTexture(null, coordinate);
    }
  }

  public void Destroy()
  {
    if (_texture != null)
    {
      Object.Destroy(_texture);
    }
    _texture = null;
    _hasMap = false;
  }

  private void RebuildMap(ChunkCoordinate coordinate)
  {
    _coordinate = coordinate;
    _hasMap = true;

    Color32[] pixels = new Color32[
        ChunkLoaderConstants.ChunkSize *
        ChunkLoaderConstants.ChunkSize];
    Color32 fallback = new Color32(13, 28, 37, 255);
    for (int i = 0; i < pixels.Length; i++)
    {
      pixels[i] = fallback;
    }

    MapUI map = Manager.ui != null ? Manager.ui.mapUI : null;
    int2 origin = coordinate.Origin;
    if (map?.MapParts != null)
    {
      Vector2Int partIndex =
          MapUI.WorldPositionToMapPartIndex(
              new float2(origin.x, origin.y));
      if (map.MapParts.TryGetValue(
              partIndex,
              out MapPartSerialized serialized) &&
          serialized.png != null &&
          serialized.png.Length > 0)
      {
        Texture2D source = new Texture2D(
            2,
            2,
            TextureFormat.RGBA32,
            false,
            true);
        try
        {
          if (ImageConversion.LoadImage(source, serialized.png, false))
          {
            int2 localOrigin =
                MapUI.WorldPositionToMapPartPosition(origin);
            for (int y = 0; y < ChunkLoaderConstants.ChunkSize; y++)
            {
              for (int x = 0; x < ChunkLoaderConstants.ChunkSize; x++)
              {
                int sourceX = MathMod(localOrigin.x + x, source.width);
                int sourceY = MathMod(localOrigin.y + y, source.height);
                pixels[y * ChunkLoaderConstants.ChunkSize + x] =
                    source.GetPixel(sourceX, sourceY);
              }
            }
          }
        }
        finally
        {
          Object.Destroy(source);
        }
      }
    }

    if (_texture == null)
    {
      _texture = new Texture2D(
          ChunkLoaderConstants.ChunkSize,
          ChunkLoaderConstants.ChunkSize,
          TextureFormat.RGBA32,
          false,
          true)
      {
        name = "ChunkLoader Toolkit Snapshot",
        filterMode = FilterMode.Point,
        wrapMode = TextureWrapMode.Clamp
      };
    }
    _texture.SetPixels32(pixels);
    _texture.Apply(false, false);
  }

  private Texture2D BuildTexture(
      ChunkLoaderDetailsData data,
      ChunkCoordinate coordinate)
  {
    if (!_hasMap ||
        _texture == null ||
        _coordinate != coordinate ||
        data == null ||
        data.IncludesSamples)
    {
      RebuildMap(coordinate);
    }

    if (_texture != null && data != null && data.IncludesSamples)
    {
      DrawSamples(data);
    }
    return _texture;
  }

  private void DrawSamples(ChunkLoaderDetailsData data)
  {
    if (_texture == null)
    {
      return;
    }

    for (int i = 0; i < data.Samples.Count; i++)
    {
      ChunkLoaderSnapshotSample sample = data.Samples[i];
      if ((sample.Flags & 3) == 0)
      {
        continue;
      }
      Color color = (sample.Flags & 2) != 0
          ? new Color(0.30f, 0.90f, 1.0f, 1.0f)
          : (sample.Flags & 1) != 0
              ? new Color(0.98f, 0.25f, 0.20f, 1.0f)
              : new Color(1.0f, 0.85f, 0.36f, 1.0f);
      int radius = (sample.Flags & 2) != 0 ? 2 : 1;
      for (int y = -radius; y <= radius; y++)
      {
        for (int x = -radius; x <= radius; x++)
        {
          int px = Mathf.Clamp(
              sample.X + x,
              0,
              ChunkLoaderConstants.ChunkSize - 1);
          int py = Mathf.Clamp(
              sample.Y + y,
              0,
              ChunkLoaderConstants.ChunkSize - 1);
          _texture.SetPixel(px, py, color);
        }
      }
    }
    _texture.Apply(false, false);
  }

  private static int MathMod(int value, int divisor)
  {
    int result = value % divisor;
    return result < 0 ? result + divisor : result;
  }
}
