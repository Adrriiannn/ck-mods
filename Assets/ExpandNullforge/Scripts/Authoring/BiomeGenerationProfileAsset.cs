using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Biome Generation Profile")]
    public sealed class BiomeGenerationProfileAsset : ScriptableObject
    {
        [SerializeField] private string profileId = "biome-profile";
        [SerializeField] private string displayName = "Biome Generation Profile";
        [SerializeField] private GenerationPassTemplateAsset[] generationPasses = new GenerationPassTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] terrainTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] floorTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] wallTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] liquidTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] oreTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] objectTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] sceneTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] spawnTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] resourceTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] worldEventTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] customTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private string notes = string.Empty;

        public string ProfileId
        {
            get { return profileId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void ConfigureIdentity(
            string newProfileId,
            string newDisplayName,
            string newNotes)
        {
            profileId = newProfileId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            notes = newNotes ?? string.Empty;
        }

        public void SetGenerationPasses(IReadOnlyList<GenerationPassTemplateAsset> passes)
        {
            generationPasses = CopyObjects(passes);
        }

        public void SetTerrainTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            terrainTables = CopyObjects(tables);
        }

        public void SetFloorTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            floorTables = CopyObjects(tables);
        }

        public void SetWallTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            wallTables = CopyObjects(tables);
        }

        public void SetLiquidTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            liquidTables = CopyObjects(tables);
        }

        public void SetOreTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            oreTables = CopyObjects(tables);
        }

        public void SetObjectTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            objectTables = CopyObjects(tables);
        }

        public void SetSceneTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            sceneTables = CopyObjects(tables);
        }

        public void SetSpawnTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            spawnTables = CopyObjects(tables);
        }

        public void SetResourceTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            resourceTables = CopyObjects(tables);
        }

        public void SetWorldEventTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            worldEventTables = CopyObjects(tables);
        }

        public void SetCustomTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            customTables = CopyObjects(tables);
        }

        public void ClearAll()
        {
            generationPasses = new GenerationPassTemplateAsset[0];
            terrainTables = new GenerationTableTemplateAsset[0];
            floorTables = new GenerationTableTemplateAsset[0];
            wallTables = new GenerationTableTemplateAsset[0];
            liquidTables = new GenerationTableTemplateAsset[0];
            oreTables = new GenerationTableTemplateAsset[0];
            objectTables = new GenerationTableTemplateAsset[0];
            sceneTables = new GenerationTableTemplateAsset[0];
            spawnTables = new GenerationTableTemplateAsset[0];
            resourceTables = new GenerationTableTemplateAsset[0];
            worldEventTables = new GenerationTableTemplateAsset[0];
            customTables = new GenerationTableTemplateAsset[0];
        }

        public void AddGenerationPassesTo(List<GenerationPassTemplateAsset> destination)
        {
            AddRange(generationPasses, destination);
        }

        public void AddGenerationTablesTo(List<GenerationTableTemplateAsset> destination)
        {
            AddRange(terrainTables, destination);
            AddRange(floorTables, destination);
            AddRange(wallTables, destination);
            AddRange(liquidTables, destination);
            AddRange(oreTables, destination);
            AddRange(objectTables, destination);
            AddRange(sceneTables, destination);
            AddRange(spawnTables, destination);
            AddRange(resourceTables, destination);
            AddRange(worldEventTables, destination);
            AddRange(customTables, destination);
        }

        private static void AddRange<T>(T[] source, List<T> destination)
            where T : Object
        {
            if (source == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                T item = source[i];
                if (item == null)
                {
                    continue;
                }

                destination.Add(item);
            }
        }

        private static T[] CopyObjects<T>(IReadOnlyList<T> source)
            where T : Object
        {
            if (source == null || source.Count == 0)
            {
                return new T[0];
            }

            List<T> destination = new List<T>();
            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                if (item != null)
                {
                    destination.Add(item);
                }
            }

            return destination.ToArray();
        }
    }
}
