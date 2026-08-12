using System.Collections.Generic;
using UnityEngine;

public static class RifleCatalog
{
    public sealed class RifleInfo
    {
        public string RifleId;
        public string DisplayName;
        public string Description;
        public Sprite Icon;
        public SniperRifleDefinition Definition;
        public SniperRifleHeldVisual HeldPrefab;
    }

    private static readonly List<RifleInfo> rifles = new List<RifleInfo>();
    private static readonly Dictionary<string, RifleInfo> byId = new Dictionary<string, RifleInfo>();

    public static void Register(SniperRifleHeldVisual heldPrefab, Sprite icon, string description)
    {
        if (heldPrefab == null || heldPrefab.Definition == null)
            return;

        string id = heldPrefab.Definition.RifleId;
        if (string.IsNullOrEmpty(id) || byId.ContainsKey(id))
            return;

        RifleInfo info = new RifleInfo
        {
            RifleId = id,
            DisplayName = heldPrefab.Definition.DisplayName,
            Description = description ?? "",
            Icon = icon,
            Definition = heldPrefab.Definition,
            HeldPrefab = heldPrefab
        };

        byId[id] = info;
        rifles.Add(info);
    }

    public static IReadOnlyList<RifleInfo> All => rifles;

    public static RifleInfo Get(string rifleId)
    {
        if (string.IsNullOrEmpty(rifleId))
            return null;

        return byId.TryGetValue(rifleId, out RifleInfo info) ? info : null;
    }
}
