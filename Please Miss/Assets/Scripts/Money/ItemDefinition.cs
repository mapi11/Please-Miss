using System.Collections.Generic;
using UnityEngine;

public enum ItemPurpose : byte
{
    Boost,
    Heal,
    Fake
}

public enum ItemClass : byte
{
    Universal,
    Runner,
    Sniper
}

[System.Serializable]
public sealed class ItemDefinition
{
    public string ItemId;
    public string DisplayName;
    public ItemPurpose Purpose;
    public ItemClass Class = ItemClass.Universal;
    public Color IconColor = Color.white;
    public Sprite IconSprite;
    public int SellPrice = 10;

    public ItemDefinition()
    {
    }

    public ItemDefinition(string itemId, string displayName, ItemPurpose purpose, Color iconColor)
    {
        ItemId = itemId;
        DisplayName = displayName;
        Purpose = purpose;
        IconColor = iconColor;
    }

    public string PurposeText
    {
        get
        {
            switch (Purpose)
            {
                case ItemPurpose.Boost:
                    return "Boost";
                case ItemPurpose.Heal:
                    return "Heal";
                case ItemPurpose.Fake:
                    return "TP";
                default:
                    return "";
            }
        }
    }

    public string ClassText
    {
        get
        {
            switch (Class)
            {
                case ItemClass.Universal:
                    return "Universal";
                case ItemClass.Runner:
                    return "Runner";
                case ItemClass.Sniper:
                    return "Sniper";
                default:
                    return "";
            }
        }
    }
}

public static class ItemCatalog
{
    private static readonly ItemDefinition[] items =
    {
        new ItemDefinition("boost", "Boost", ItemPurpose.Boost, new Color(1f, 0.55f, 0.1f)),
        new ItemDefinition("heal", "Heal", ItemPurpose.Heal, new Color(0.25f, 0.9f, 0.35f)),
        new ItemDefinition("fake", "Fake", ItemPurpose.Fake, new Color(0.6f, 0.35f, 1f))
    };

    static ItemCatalog()
    {
        items[0].SellPrice = 50;
        items[1].SellPrice = 100;
        items[2].SellPrice = 200;
    }
    private static readonly System.Collections.Generic.Dictionary<string, ItemDefinition> extraItems =
        new System.Collections.Generic.Dictionary<string, ItemDefinition>();

    public static IReadOnlyList<ItemDefinition> All => items;

    public static void Register(ItemDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.ItemId))
            return;

        extraItems[def.ItemId] = def;
    }

    public static ItemDefinition Get(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        foreach (var item in items)
        {
            if (item != null && item.ItemId == itemId)
                return item;
        }

        if (extraItems.TryGetValue(itemId, out ItemDefinition extra))
            return extra;

        return null;
    }
}
