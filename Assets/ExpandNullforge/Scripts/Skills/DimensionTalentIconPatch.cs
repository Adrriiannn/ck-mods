using HarmonyLib;
using UnityEngine;

namespace ExpandNullforge.Skills
{
    /// <summary>
    /// Puts a mod's own picture on a talent square as the talent window draws it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE OBVIOUS PLACE TO DO THIS IS THE WRONG PLACE. The tempting move is to write the picture
    /// into <c>SkillTalentsTable</c> after <c>PugMods.Talents.Init</c> has run. That is a
    /// read-modify-write of a struct inside a list, on a table the game rebuilds from JSON on every
    /// mod reload, at a moment nobody can point to: <c>Talents.Init</c> is called once from
    /// <c>ModManager</c>, and whether a mod's sprites have loaded by then is not something to
    /// assume. Get the moment wrong and the icons are silently absent.
    /// </para>
    /// <para>
    /// SO THE PICTURE IS SUPPLIED WHERE IT IS USED. <c>SkillTalentUIElement.UpdateTalent</c> is
    /// handed the whole talent by value and the very next thing it does is
    /// <c>this.icon.sprite = skillTalentInfo.icon</c> (<c>ck-db\Pug.Other\SkillTalentUIElement.cs:30</c>).
    /// Filling the picture in on the way past has no timing question in it at all: the window is
    /// open, the bundle is loaded, and the game's own table is never touched — so nothing this mod
    /// does can be undone by the next thing that rebuilds it, and a talent the mod did not claim
    /// keeps whatever picture the game gave it.
    /// </para>
    /// <para>
    /// The copy handed in is also what the element keeps (<c>this.info = skillTalentInfo</c>), so
    /// the hover panel sees the same thing the square does.
    /// </para>
    /// <para>
    /// COST WHEN UNUSED IS ONE BOOLEAN, and only while the talent window is open.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(SkillTalentUIElement), "UpdateTalent")]
    internal static class DimensionTalentIconPatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            SkillID skillTree,
            int index,
            ref SkillTalentsTable.SkillTalentInfo skillTalentInfo)
        {
            if (!DimensionTalentIconRegistry.HasAny)
            {
                return;
            }

            Sprite icon;
            if (DimensionTalentIconRegistry.TryGet(skillTree, index, out icon) && icon != null)
            {
                skillTalentInfo.icon = icon;
            }
        }
    }
}
