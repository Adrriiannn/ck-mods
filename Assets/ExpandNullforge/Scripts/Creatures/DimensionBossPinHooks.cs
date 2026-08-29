using HarmonyLib;
using PugMod;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Gives the map screen a row for every custom boss pin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The map resolves a UniqueBoss marker's icon and hover text by looking its ObjectID up in
    /// <c>MapUI.uniqueMarkerInfo</c> — an inspector-authored list. A custom id it has never
    /// heard of logs an error and renders an icon-less pin. The list is public and searched
    /// linearly by value, so appending rows at <c>Awake</c> is the whole integration; vanilla
    /// pins are untouched because mod ObjectIDs never collide with vanilla values.
    /// </para>
    /// <para>
    /// <c>Awake</c> is verified to exist on <c>MapUI</c> — the GameMusicHandler lesson: naming a
    /// method Harmony cannot find throws at patch time and silently costs every later patch.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(MapUI), "Awake")]
    public static class DimensionBossPinHook
    {
        private static void Postfix(MapUI __instance)
        {
            if (__instance == null || __instance.uniqueMarkerInfo == null)
            {
                return;
            }

            System.Collections.Generic.IReadOnlyList<DimensionBossPresentationDefinition> definitions =
                DimensionBossPresentationRegistry.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                DimensionBossPresentationDefinition definition = definitions[i];
                if (!definition.ShowsOnTheMap)
                {
                    continue;
                }

                ObjectID id = API.Authoring.GetObjectID(definition.BossObjectName);
                if (id == ObjectID.None)
                {
                    continue;
                }

                __instance.uniqueMarkerInfo.Add(new MapUI.UniqueMarkerInfo
                {
                    id = id,
                    hoverString = new I2.Loc.LocalizedString { mTerm = definition.HoverTerm },
                    largeMapIcon = definition.LargeMapIcon,
                    miniMapIcon = definition.MiniMapIcon
                });
            }
        }
    }
}
