using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class — Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Loot Table")]
    public sealed class DimensionLootTableAsset : ScriptableObject
    {
        [SerializeField] private string lootTableId = "mod:loot";
        [SerializeField] private string displayName = "Loot Table";
        [SerializeField] private bool allowEmptyRoll;
        [SerializeField] private bool enabled = true;
        [SerializeField] private DimensionLootEntryTemplate[] entries =
            new DimensionLootEntryTemplate[0];
        [SerializeField] private string notes = string.Empty;

        public string LootTableId
        {
            get { return lootTableId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public bool AllowEmptyRoll
        {
            get { return allowEmptyRoll; }
        }

        public DimensionLootEntryTemplate[] Entries
        {
            get { return entries ?? new DimensionLootEntryTemplate[0]; }
        }

        public int EnabledEntryCount
        {
            get
            {
                int count = 0;
                DimensionLootEntryTemplate[] values = Entries;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] != null && values[i].Enabled)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }
    }
}
