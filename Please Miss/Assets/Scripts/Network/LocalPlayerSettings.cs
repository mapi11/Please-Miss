using System.Collections.Generic;
using UnityEngine;

public static class LocalPlayerSettings
{
    private const string DefaultProfileId = "Default";

    public static string ProfileId { get; private set; } = DefaultProfileId;
    public static string PlayerName { get; private set; } = "Player";
    public static event System.Action<Color32> ColorChanged;

    public static Color32 PlayerColor { get; private set; } = new Color32(255, 255, 255, 255);

    private static readonly Color32[] ColorPalette =
    {
        new Color32(255, 90, 90, 255),
        new Color32(90, 160, 255, 255),
        new Color32(90, 255, 140, 255),
        new Color32(255, 220, 90, 255),
        new Color32(210, 120, 255, 255),
        new Color32(255, 150, 80, 255),
        new Color32(90, 255, 240, 255),
        new Color32(255, 120, 190, 255),
    };

    private const string PlayerNamePrefix = "PlayerName";
    private const string PlayerColorPrefix = "PlayerColor";
    private const string PlayerPointsPrefix = "PlayerPoints";
    private const string InventoryItemsPrefix = "InventoryItems";
    private const string EquipmentItemsPrefix = "EquipmentItems";
    private const string EquipmentRunnerItemsPrefix = "EquipmentRunnerItems";
    private const string EquipmentSniperItemsPrefix = "EquipmentSniperItems";
    private const string SniperRiflePrefix = "SniperRifleItems";
    private const string OwnedSniperRiflesPrefix = "OwnedSniperRifleItems";

    private static string storageSuffix = "";

    private static string MakeKey(string prefix)
    {
        return $"{prefix}_{ProfileId}{storageSuffix}";
    }

    private static string MakeProfileKey(string prefix)
    {
        return $"{prefix}_{ProfileId}";
    }

    private static string PlayerNameKey => MakeKey(PlayerNamePrefix);
    private static string PlayerColorKey => MakeKey(PlayerColorPrefix);
    private static string PlayerPointsKey => MakeKey(PlayerPointsPrefix);

    /// <summary>
    /// Переводит хранилище в изолированное пространство ключей сетевой сессии:
    /// у каждого игрока своё хранилище, даже когда игроки запущены на одной машине.
    /// Данные профиля копируются в пространство сессии при первом входе.
    /// </summary>
    public static void EnterSession(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        string suffix = $"_{sessionId}";

        if (storageSuffix == suffix)
            return;

        storageSuffix = suffix;
        SeedFromProfile();
    }

    /// <summary>
    /// Гарантирует наличие сессионного пространства ключей: если его ещё нет,
    /// создаёт новое с уникальным id. Повторный вызов сохраняет текущую сессию
    /// (меню → лобби → игра используют одно пространство).
    /// </summary>
    public static void EnsureSession()
    {
        if (!string.IsNullOrEmpty(storageSuffix))
            return;

        EnterSession(System.Guid.NewGuid().ToString("N"));
    }

    public static void ExitSession()
    {
        MergeToProfile();
        storageSuffix = "";
    }

    private static void MergeToProfile()
    {
        CopySessionToProfile(PlayerNamePrefix, false);
        CopySessionToProfile(PlayerColorPrefix, true);
        CopySessionToProfile(PlayerPointsPrefix, true);
        CopySessionToProfile(InventoryItemsPrefix, false);
        CopySessionToProfile(EquipmentItemsPrefix, false);
        CopySessionToProfile(EquipmentRunnerItemsPrefix, false);
        CopySessionToProfile(EquipmentSniperItemsPrefix, false);
        CopySessionToProfile(SniperRiflePrefix, false);
        CopySessionToProfile(OwnedSniperRiflesPrefix, false);
    }

    private static void SeedFromProfile()
    {
        CopyProfileToSession(PlayerNamePrefix, false);
        CopyProfileToSession(PlayerColorPrefix, true);
        CopyProfileToSession(PlayerPointsPrefix, true);
        CopyProfileToSession(InventoryItemsPrefix, false);
        CopyProfileToSession(EquipmentItemsPrefix, false);
        CopyProfileToSession(EquipmentRunnerItemsPrefix, false);
        CopyProfileToSession(EquipmentSniperItemsPrefix, false);
        CopyProfileToSession(SniperRiflePrefix, false);
        CopyProfileToSession(OwnedSniperRiflesPrefix, false);
    }

    private static void CopyProfileToSession(string prefix, bool isInt)
    {
        string sessionKey = MakeKey(prefix);

        if (PlayerPrefs.HasKey(sessionKey))
            return;

        CopyKey(MakeProfileKey(prefix), sessionKey, isInt);
    }

    private static void CopySessionToProfile(string prefix, bool isInt)
    {
        string sessionKey = MakeKey(prefix);

        if (!PlayerPrefs.HasKey(sessionKey))
            return;

        CopyKey(sessionKey, MakeProfileKey(prefix), isInt);
    }

    private static void CopyKey(string fromKey, string toKey, bool isInt)
    {
        if (!PlayerPrefs.HasKey(fromKey))
            return;

        // Копирование должно сохранять тип значения: int-ключи (цвет, очки)
        // нельзя переносить через SetString/GetString — Unity вернёт мусор при GetInt.
        if (isInt)
            PlayerPrefs.SetInt(toKey, PlayerPrefs.GetInt(fromKey));
        else
            PlayerPrefs.SetString(toKey, PlayerPrefs.GetString(fromKey));

        PlayerPrefs.Save();
    }

    public static void Load()
    {
        Load(ProfileId);
    }

    public static void Load(string profileId)
    {
        SetProfileId(profileId);

        PlayerName = PlayerPrefs.GetString(PlayerNameKey, $"Player_{ProfileId}");

        if (PlayerPrefs.HasKey(PlayerColorKey))
        {
            PlayerColor = UnpackColor(PlayerPrefs.GetInt(PlayerColorKey));

            // Лечение повреждённых данных от старых версий: строка-копия int-значения
            // читалась как 0 (чёрный с альфой 0) — перегенерируем случайный цвет.
            if (PlayerColor.a == 0)
            {
                GenerateAndSaveRandomColor();
            }
        }
        else
        {
            GenerateAndSaveRandomColor();
        }
    }

    public static void SetProfileId(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            profileId = DefaultProfileId;

        profileId = profileId.Trim();

        if (profileId.Length > 20)
            profileId = profileId.Substring(0, 20);

        ProfileId = profileId;
    }

    public static void SetPlayerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            value = $"Player_{ProfileId}";

        value = value.Trim();

        if (value.Length > 20)
            value = value.Substring(0, 20);

        PlayerName = value;

        PlayerPrefs.SetString(PlayerNameKey, PlayerName);
        PlayerPrefs.Save();
    }

    public static void GenerateAndSaveRandomColor()
    {
        int index = Random.Range(0, ColorPalette.Length);
        SetPlayerColor(ColorPalette[index]);
        GameSessionData.SelectedColorIndex = index;
    }

    public static Color32 GetPaletteColor(int index)
    {
        return ColorPalette[Mathf.Clamp(index, 0, ColorPalette.Length - 1)];
    }

    public static int PaletteSize => ColorPalette.Length;

    public static void SetPlayerColor(Color32 color)
    {
        color.a = 255;
        PlayerColor = color;
        PlayerPrefs.SetInt(PlayerColorKey, PackColor(PlayerColor));
        PlayerPrefs.Save();
        ColorChanged?.Invoke(color);
    }

    public static int PackColor(Color32 color)
    {
        return color.r << 24 | color.g << 16 | color.b << 8 | color.a;
    }

    public static Color32 UnpackColor(int packed)
    {
        byte r = (byte)((packed >> 24) & 0xFF);
        byte g = (byte)((packed >> 16) & 0xFF);
        byte b = (byte)((packed >> 8) & 0xFF);
        byte a = (byte)(packed & 0xFF);
        return new Color32(r, g, b, a);
    }

    public const int DefaultPlayerPoints = 500;

    public static event System.Action<int> PointsChanged;

    public static int PlayerPoints => PlayerPrefs.GetInt(PlayerPointsKey, DefaultPlayerPoints);

    public static void SetPoints(int value)
    {
        PlayerPrefs.SetInt(PlayerPointsKey, Mathf.Max(0, value));
        PlayerPrefs.Save();
        PointsChanged?.Invoke(PlayerPoints);
    }

    public static void AddPoints(int value)
    {
        if (value <= 0)
            return;

        PlayerPrefs.SetInt(PlayerPointsKey, Mathf.Max(0, PlayerPoints + value));
        PlayerPrefs.Save();
        PointsChanged?.Invoke(PlayerPoints);
    }

    public const int EquipmentSlotsCount = 2;

    /// <summary>Сколько слотов снаряжения у бегуна в меню инвентаря.</summary>
    public const int RunnerEquipmentSlotsCount = 3;

    /// <summary>Сколько слотов снаряжения у снайпера в меню инвентаря (плюс отдельный слот винтовки).</summary>
    public const int SniperEquipmentSlotsCount = 2;

    public static int GetEquipmentSlotsCount(bool isRunner)
    {
        return isRunner ? RunnerEquipmentSlotsCount : SniperEquipmentSlotsCount;
    }

    private static string InventoryItemsKey => MakeKey(InventoryItemsPrefix);
    private static string EquipmentItemsKey => MakeKey(EquipmentItemsPrefix);

    public static List<string> Inventory => GetStringList(InventoryItemsKey);

    public static bool HasSavedInventory => PlayerPrefs.HasKey(InventoryItemsKey);

    public static List<string> Equipment => GetStringList(EquipmentItemsKey);

    public static void AddInventoryItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return;

        var items = GetStringList(InventoryItemsKey);
        items.Add(itemId);
        SaveStringList(InventoryItemsKey, items);
    }

    public static bool RemoveInventoryItem(string itemId)
    {
        var items = GetStringList(InventoryItemsKey);
        bool removed = items.Remove(itemId);

        if (removed)
            SaveStringList(InventoryItemsKey, items);

        return removed;
    }

    public static void SetEquipmentSlot(int slotIndex, string itemId)
    {
        if (slotIndex < 0 || slotIndex >= EquipmentSlotsCount)
            return;

        var items = GetStringList(EquipmentItemsKey);

        while (items.Count <= slotIndex)
            items.Add("");

        items[slotIndex] = itemId ?? "";
        SaveStringList(EquipmentItemsKey, items);
    }

    public static string GetEquipmentSlot(int slotIndex)
    {
        var items = GetStringList(EquipmentItemsKey);

        if (slotIndex < 0 || slotIndex >= items.Count)
            return "";

        return items[slotIndex];
    }

    public static void ClearEquipment()
    {
        var items = GetStringList(EquipmentItemsKey);

        for (int i = 0; i < items.Count; i++)
            items[i] = "";

        SaveStringList(EquipmentItemsKey, items);
    }

    private static string EquipmentRunnerItemsKey => MakeKey(EquipmentRunnerItemsPrefix);
    private static string EquipmentSniperItemsKey => MakeKey(EquipmentSniperItemsPrefix);

    public static List<string> RunnerEquipment => GetStringList(EquipmentRunnerItemsKey);
    public static List<string> SniperEquipment => GetStringList(EquipmentSniperItemsKey);

    public static void SetRunnerEquipmentSlot(int slotIndex, string itemId)
    {
        SetRoleEquipmentSlot(EquipmentRunnerItemsKey, RunnerEquipmentSlotsCount, slotIndex, itemId);
    }

    public static string GetRunnerEquipmentSlot(int slotIndex)
    {
        return GetRoleEquipmentSlot(EquipmentRunnerItemsKey, slotIndex);
    }

    public static void SetSniperEquipmentSlot(int slotIndex, string itemId)
    {
        SetRoleEquipmentSlot(EquipmentSniperItemsKey, SniperEquipmentSlotsCount, slotIndex, itemId);
    }

    public static string GetSniperEquipmentSlot(int slotIndex)
    {
        return GetRoleEquipmentSlot(EquipmentSniperItemsKey, slotIndex);
    }

    private static void SetRoleEquipmentSlot(string key, int maxSlots, int slotIndex, string itemId)
    {
        if (slotIndex < 0 || slotIndex >= maxSlots)
            return;

        var items = GetStringList(key);

        while (items.Count <= slotIndex)
            items.Add("");

        items[slotIndex] = itemId ?? "";
        SaveStringList(key, items);
    }

    private static string GetRoleEquipmentSlot(string key, int slotIndex)
    {
        var items = GetStringList(key);

        if (slotIndex < 0 || slotIndex >= items.Count)
            return "";

        return items[slotIndex];
    }

    private static string SniperRifleKey => MakeKey(SniperRiflePrefix);

    public static string SniperRifle => PlayerPrefs.GetString(SniperRifleKey, "");

    public static void SetSniperRifle(string itemId)
    {
        PlayerPrefs.SetString(SniperRifleKey, itemId ?? "");
        PlayerPrefs.Save();
    }

    private static string OwnedSniperRiflesKey => MakeKey(OwnedSniperRiflesPrefix);

    public static List<string> OwnedSniperRifles => GetStringList(OwnedSniperRiflesKey);

    public static bool IsSniperRifleOwned(string rifleId)
    {
        if (string.IsNullOrEmpty(rifleId))
            return false;

        return OwnedSniperRifles.Contains(rifleId);
    }

    public static void AddOwnedSniperRifle(string rifleId)
    {
        if (string.IsNullOrEmpty(rifleId))
            return;

        var owned = GetStringList(OwnedSniperRiflesKey);

        if (owned.Contains(rifleId))
            return;

        owned.Add(rifleId);
        SaveStringList(OwnedSniperRiflesKey, owned);
    }

    private static List<string> GetStringList(string key)
    {
        string raw = PlayerPrefs.GetString(key, "");
        var list = new List<string>();

        if (string.IsNullOrEmpty(raw))
            return list;

        string[] parts = raw.Split('|');

        foreach (string part in parts)
            list.Add(part);

        return list;
    }

    private static void SaveStringList(string key, List<string> items)
    {
        PlayerPrefs.SetString(key, string.Join("|", items.ToArray()));
        PlayerPrefs.Save();
    }
}
