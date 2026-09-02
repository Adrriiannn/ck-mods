using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentPackDefinition
    {
        public readonly string ContentPackId;
        public readonly string DisplayName;
        public readonly string Version;
        public readonly string Author;
        public readonly string Description;
        public readonly int MinimumApiVersion;
        public readonly IReadOnlyList<string> DependencyIds;
        public readonly bool Enabled;

        public DimensionContentPackDefinition(
            string contentPackId,
            string displayName,
            string version,
            string author,
            string description,
            int minimumApiVersion,
            IReadOnlyList<string> dependencyIds,
            bool enabled)
        {
            ContentPackId = contentPackId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Version = version ?? string.Empty;
            Author = author ?? string.Empty;
            Description = description ?? string.Empty;
            MinimumApiVersion = minimumApiVersion;
            DependencyIds = CopyDependencyIds(dependencyIds);
            Enabled = enabled;
        }

        private static IReadOnlyList<string> CopyDependencyIds(IReadOnlyList<string> dependencyIds)
        {
            List<string> copy = new List<string>();
            if (dependencyIds == null)
            {
                return copy;
            }

            for (int i = 0; i < dependencyIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(dependencyIds[i]))
                {
                    copy.Add(dependencyIds[i]);
                }
            }

            copy.Sort(System.StringComparer.Ordinal);
            return copy;
        }
    }
}
