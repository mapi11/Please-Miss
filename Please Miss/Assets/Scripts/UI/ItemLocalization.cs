using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Локализация имён и описаний предметов и оружия.
/// Предметы ищутся в Items_Table, оружие — в Weapon_Table (по rifleId).
/// Ключ имени — itemName/rifleId, ключ описания — ключ + "_Description".
/// Если записи нет ни в одной таблице, возвращается исходная строка (fallback).
/// </summary>
public static class ItemLocalization
{
    public const string TableName = "Items_Table";
    public const string WeaponTableName = "Weapon_Table";

    public static string GetName(string itemKey)
    {
        return GetLocalized(itemKey, itemKey);
    }

    public static string GetName(string itemKey, string fallback)
    {
        if (string.IsNullOrEmpty(itemKey))
            return fallback ?? "";

        return GetLocalized(itemKey, fallback ?? itemKey);
    }

    public static string GetDescription(string itemKey, string fallback)
    {
        if (string.IsNullOrEmpty(itemKey))
            return fallback ?? "";

        return GetLocalized(itemKey + "_Description", fallback ?? "");
    }

    private static string GetLocalized(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key))
            return fallback;

        var table = LocalizationSettings.StringDatabase.GetTable(TableName);
        if (table != null && table.GetEntry(key) != null)
            return LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key);

        var weaponTable = LocalizationSettings.StringDatabase.GetTable(WeaponTableName);
        if (weaponTable != null && weaponTable.GetEntry(key) != null)
            return LocalizationSettings.StringDatabase.GetLocalizedString(WeaponTableName, key);

        return fallback;
    }
}