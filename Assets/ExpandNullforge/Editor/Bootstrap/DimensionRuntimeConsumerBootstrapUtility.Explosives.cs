using System.Text;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits the one link in a bomb that cannot exist until the game has handed out its numbers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A BOMB NEEDS ANY RUNTIME CODE AT ALL, given that everything else about it is prefab data.
    /// <c>ExplosiveAuthoring.explosionID</c> is a raw <c>ObjectID</c> enum field naming the blast,
    /// and a mod's object ids do not exist while its prefabs are being written. When the blast is
    /// one of the GAME's own objects the generator bakes the number and nothing is emitted here;
    /// when it is the blast the framework made alongside the bomb, this row is what turns the two
    /// names into the two numbers at load.
    /// </para>
    /// <para>
    /// Getting it wrong is invisible, which is why it is worth the machinery: an unresolved blast is
    /// <c>ObjectID.None</c>, <c>CreateExplosion</c> spawns nothing, and it returns without a log
    /// (<c>ck-db\Pug.Other\ExplosiveSystem.cs:88-91</c>). The bomb's fuse runs out, the bomb
    /// disappears, and nothing else happens.
    /// </para>
    /// <para>
    /// The registry name is written out in full rather than relying on a <c>using</c> in the
    /// generated file's header. One line that has to be added in two places is one line that will
    /// eventually only be added in one.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>Writes one row per bomb whose blast is one of this mod's own objects.</summary>
        internal static void AppendExplosiveRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; items != null && i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null || !item.Enabled || string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                DimensionExplosiveTemplate explosive = item.Explosive;
                if (!explosive.Explodes)
                {
                    continue;
                }

                string blastId;
                int variation;
                if (explosive.MakesItsOwnBlast)
                {
                    blastId = DimensionExplosiveBlast.BlastIdFor(item.ItemId);

                    // Always zero for a generated blast: the framework writes one object per bomb
                    // rather than one object with a variation per bomb.
                    variation = 0;
                }
                else if (NamesOneOfOurBlasts(template, explosive.ExplosionObjectId))
                {
                    blastId = explosive.ExplosionObjectId;
                    variation = explosive.ExplosionVariation;
                }
                else
                {
                    // The blast is one of the game's own and the number is already sitting on the
                    // prefab. Emitting a row would make the runtime look up something it does not
                    // need, and would bury the case where a name really cannot be resolved under a
                    // busy log.
                    continue;
                }

                builder.AppendLine(
                    "    ExpandNullforge.Explosives.DimensionExplosiveRegistry.Register(");
                builder.Append("        ")
                    .Append(ToCSharpString(DimensionObjectNamespace.Qualify(modName, item.ItemId)))
                    .AppendLine(",");
                builder.Append("        ")
                    .Append(ToCSharpString(DimensionObjectNamespace.Qualify(modName, blastId)))
                    .AppendLine(",");
                builder.Append("        ").Append(variation).AppendLine(");");
            }
        }

        /// <summary>
        /// Whether a blast id names one of this mod's own blast assets rather than one of the
        /// game's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Without this check a hand-named blast of the author's own was unreachable: the generator
        /// cannot turn the name into a number, so it baked <c>None</c> and the shared blast asset
        /// could be authored, generated, and never used by anything.
        /// </para>
        /// <para>
        /// IT ASKS THE SAME TWO QUESTIONS THE GENERATOR ASKS, and it did not. The item generator's
        /// <c>NamesOneOfOurBlasts</c> requires the game's enum to answer nothing and the blast to
        /// be ticked on; this one asked neither. So unticking a blast produced a generate that
        /// reported the bomb as pointing at a name nothing has and baked <c>None</c>, while this
        /// still wrote a registration for a blast that would never exist. Drift between these two
        /// answers is the one structural risk <c>DimensionObjectBinder</c>'s remark names.
        /// </para>
        /// </remarks>
        private static bool NamesOneOfOurBlasts(DimensionTemplateAsset template, string blastId)
        {
            if (template == null || string.IsNullOrEmpty(blastId) ||
                DimensionObjectBinder.Vanilla(blastId) != ObjectID.None)
            {
                return false;
            }

            string local = DimensionObjectNamespace.LocalIdOf(blastId);
            DimensionExplosionAsset[] blasts = template.GlobalExplosions;
            for (int i = 0; blasts != null && i < blasts.Length; i++)
            {
                if (blasts[i] != null && blasts[i].Enabled &&
                    string.Equals(blasts[i].ExplosionId, local, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
