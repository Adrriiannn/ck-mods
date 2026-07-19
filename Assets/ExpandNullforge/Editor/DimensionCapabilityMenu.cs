using System.Text;
using ExpandNullforge.Api;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Surfaces the honest capability-maturity registry so a creator can see, at a glance,
    /// which parts of the framework are proven versus preview/experimental — the antidote to
    /// the dashboard implying every exposed interface is production-ready.
    /// </summary>
    internal static class DimensionCapabilityMenu
    {
        [MenuItem("Dimensions API/Report Capability Maturity")]
        private static void ReportCapabilityMaturity()
        {
            DimensionCapability[] capabilities = DimensionCapabilityRegistry.All();
            StringBuilder builder = new StringBuilder();
            builder.Append("Dimensions API capability maturity (");
            builder.Append(capabilities.Length);
            builder.Append(" features):");
            for (int i = 0; i < capabilities.Length; i++)
            {
                DimensionCapability capability = capabilities[i];
                builder.Append('\n');
                builder.Append("  [");
                builder.Append(DimensionCapabilityRegistry.Describe(capability.Maturity));
                builder.Append("] ");
                builder.Append(capability.Title);
                builder.Append(" — ");
                builder.Append(capability.Note);
            }

            Debug.Log(builder.ToString());
        }
    }
}
