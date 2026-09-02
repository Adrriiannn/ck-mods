using System.Globalization;
using System.Text;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits the half of a placed light that only exists once the game is running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THE GENERATOR CANNOT JUST BAKE THIS INTO THE PREFAB. It does bake it — and for an object
    /// nothing uses, the baked light is the whole answer, because that prefab is instantiated once
    /// per entity. But as soon as an object can be USED it carries one of the framework's six
    /// views, and Core Keeper pools graphical objects by component TYPE: one prefab is instantiated
    /// and every object of that behaviour is drawn with instances of it. The baked light would be
    /// one light for every crafting bench, or every chest, in the mod. The view re-derives it per
    /// entity instead, it is handed nothing but an object id, and object ids do not exist until the
    /// mod is loaded — so the bootstrap is the only place the id and the numbers can be introduced.
    /// </para>
    /// <para>
    /// It is the same shape as the crop bodies and the bench looks next door, for the same reason,
    /// and the row is deliberately written for EVERY lit object rather than only for usable ones.
    /// An object's use can change in the dashboard after the fact, and a row nothing reads costs a
    /// dictionary entry, while a missing row is a dark forge.
    /// </para>
    /// <para>
    /// NOTHING IS EMITTED FOR AN OBJECT WITH NO LIGHT, and that is the answer rather than a gap:
    /// <c>DimensionAuthoredLight.Point</c> reads "no row" as "put the shared instance's light out",
    /// which is what keeps a generated chest from wearing a brazier's light.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>
        /// Registers the light every lit world object gives off where it stands.
        /// </summary>
        /// <remarks>
        /// World objects are the only assets that answer the light question today. A container and
        /// a workbench have their own generators that never ask it, so they register nothing — and
        /// because "no row" means "no light", they come out correct without either generator being
        /// touched.
        /// </remarks>
        internal static void AppendEmittedLightRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionWorldObjectAsset[] worldObjects =
                template == null ? null : template.GlobalWorldObjects;
            if (worldObjects == null)
            {
                return;
            }

            for (int i = 0; i < worldObjects.Length; i++)
            {
                DimensionWorldObjectAsset worldObject = worldObjects[i];
                if (worldObject == null || !worldObject.Enabled ||
                    string.IsNullOrEmpty(worldObject.ObjectIdentifier) ||
                    !worldObject.EmittedLight.GivesOffLight)
                {
                    continue;
                }

                AppendOneEmittedLight(
                    builder,
                    DimensionObjectNamespace.Qualify(modName, worldObject.ObjectIdentifier),
                    worldObject.EmittedLight);
            }
        }

        /// <summary>Writes one object's light.</summary>
        /// <remarks>
        /// <para>
        /// THE NUMBERS ARE THE ONES THE GAME RUNS WITH, not the raw answers, and they are asked for
        /// by the same call the prefab was built from — so the light baked into the prefab and the
        /// light re-applied per entity are the same numbers by construction rather than by two
        /// pieces of code agreeing.
        /// </para>
        /// <para>
        /// Written out in full rather than short: the generated file's using list is fixed by the
        /// writer in the main partial, which this domain must not edit, and it does not carry
        /// ExpandNullforge.Objects. A qualified call needs nothing from it. The colour goes over as
        /// three floats for the same reason — naming <c>Color</c> would need a using this file
        /// cannot add.
        /// </para>
        /// </remarks>
        private static void AppendOneEmittedLight(
            StringBuilder builder,
            string objectName,
            DimensionEmittedLightTemplate emittedLight)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return;
            }

            ExpandNullforge.Objects.DimensionEmittedLightNumbers numbers =
                emittedLight.TheNumbersTheGameRunsWith();

            builder.AppendLine("    ExpandNullforge.Objects.DimensionEmittedLightRegistry.Register(");
            builder.Append("        ").Append(ToCSharpString(objectName)).AppendLine(",");
            builder.Append("        ").Append(LightFloat(numbers.Colour.r)).AppendLine(",");
            builder.Append("        ").Append(LightFloat(numbers.Colour.g)).AppendLine(",");
            builder.Append("        ").Append(LightFloat(numbers.Colour.b)).AppendLine(",");
            builder.Append("        ").Append(LightFloat(numbers.HowBright)).AppendLine(",");
            builder.Append("        ").Append(LightFloat(numbers.HowFarItReaches)).AppendLine(",");
            builder.Append("        ").Append(numbers.ItCastsShadows ? "true" : "false").AppendLine(",");
            builder.Append("        ").Append(LightFloat(numbers.AtItsDimmest)).AppendLine(",");
            builder.Append("        ").Append(LightFloat(numbers.AtItsBrightest)).AppendLine(",");
            builder.Append("        ").Append(numbers.TheFlameMoves ? "true" : "false").AppendLine(",");
            builder.Append("        ").Append(LightFloat(numbers.HowHighAboveTheFloor)).AppendLine(",");
            builder.Append("        ").Append(LightFloat(numbers.HowFarFrontToBack)).AppendLine(");");
        }

        /// <summary>A float written so it reads back as the same number.</summary>
        /// <remarks>
        /// "R" rather than the default, because a colour channel measured off a vanilla prefab is
        /// a value like 0.81942016 and the default form would round it.
        /// </remarks>
        private static string LightFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }
    }
}
