using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryMenuUI : MonoBehaviour
{
    private enum InventoryMode
    {
        All,
        Runner,
        Sniper
    }

    [Header("References (assigned on the spawned prefab)")]
    [SerializeField] private RectTransform inventorySlotsContainer;
    [SerializeField] private GameObject slotPrefab;
    [Tooltip("Optional. If set, player slots are built from this prefab instead of slotPrefab")]
    [SerializeField] private GameObject playerSlotPrefab;

    [Header("Role Mode")]
    [Tooltip("TMP_Dropdown with options All / Runner / Sniper. Assigned in the Inspector")]
    [SerializeField] private TMP_Dropdown modeDropdown;
    [Tooltip("Optional. Darkens the player panel while 'All' is selected")]
    [SerializeField] private GameObject blockInventory;
    [Tooltip("Optional. Blocks the Runner container when another mode is active")]
    [SerializeField] private GameObject blockRunner;
    [Tooltip("Optional. Blocks the Sniper container when another mode is active")]
    [SerializeField] private GameObject blockSniper;
    [Tooltip("Optional. If not assigned, created at runtime next to the Runner container")]
    [SerializeField] private RectTransform playerRunnerInventoryContainer;
    [Tooltip("Optional. If not assigned, created at runtime next to the Runner container")]
    [SerializeField] private RectTransform playerSniperInventoryContainer;

    [Header("Window")]
    [SerializeField] private RectTransform windowRoot;
    [SerializeField] private Button closeButton1;
    [SerializeField] private Button closeButton2;

    [Header("Player")]
    [SerializeField] private Image colorPreview;

    [Header("Points")]
    [SerializeField] private TMP_Text pointsText;

    private Canvas canvas;
    private RectTransform canvasRect;
    private Color32 selectedColor;
    private InventoryMode mode = InventoryMode.Runner;

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
            canvasRect = canvas.transform as RectTransform;

        selectedColor = LocalPlayerSettings.PlayerColor;
        LocalPlayerSettings.ColorChanged += OnColorChanged;

        EnsureContainers();
        EnsureWindow();
        EnsureModeDropdown();
        InitModeDropdown();
        EnsureBlockInventory();
        EnsureRoleContainers();
        EnsureRoleBlocks();
        EnsureCloseButtons();
        EnsureColorRefs();
        Refresh();
        RefreshColor();
    }

    private void OnEnable()
    {
        LocalPlayerSettings.PointsChanged += OnPointsChanged;
        RefreshPoints();
        Refresh();
    }

    private void OnDisable()
    {
        LocalPlayerSettings.PointsChanged -= OnPointsChanged;
    }

    private void OnDestroy()
    {
        LocalPlayerSettings.ColorChanged -= OnColorChanged;
    }

    private void OnPointsChanged(int newPoints)
    {
        RefreshPoints();
    }

    private void RefreshPoints()
    {
        if (pointsText != null)
            pointsText.text = $"Points: {LocalPlayerSettings.PlayerPoints}";
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void Close()
    {
        if (windowRoot != null)
            windowRoot.gameObject.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void EnsureContainers()
    {
        if (inventorySlotsContainer == null)
            inventorySlotsContainer = transform.Find("InventorySlotsContainer") as RectTransform;
    }

    private void EnsureModeDropdown()
    {
        if (modeDropdown != null)
            return;

        Transform t = FindInChildren(windowRoot != null ? windowRoot : transform, "ModeDropdown");
        if (t != null)
            modeDropdown = t.GetComponent<TMP_Dropdown>();
    }

    private void InitModeDropdown()
    {
        if (modeDropdown == null)
            return;

        modeDropdown.ClearOptions();
        modeDropdown.AddOptions(new List<string> { "All", "Runner", "Sniper" });
        modeDropdown.SetValueWithoutNotify((int)mode);
        modeDropdown.onValueChanged.RemoveAllListeners();
        modeDropdown.onValueChanged.AddListener(OnModeOptionChanged);
    }

    private void EnsureBlockInventory()
    {
        EnsureBlock(ref blockInventory, "BlockInventory", playerRunnerInventoryContainer);
    }

    private void EnsureRoleBlocks()
    {
        EnsureBlock(ref blockRunner, "BlockRunner", playerRunnerInventoryContainer);
        EnsureBlock(ref blockSniper, "BlockSniper", playerSniperInventoryContainer);
    }

    private void EnsureBlock(ref GameObject block, string name, RectTransform container)
    {
        if (block != null)
            return;

        Transform parent = container != null ? container.parent : transform;

        Transform t = FindInChildren(parent, name);
        if (t != null)
        {
            block = t.gameObject;
            return;
        }

        if (container == null)
            return;

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);

        block = go;
    }

    private void EnsureRoleContainers()
    {
        EnsureRoleContainer(ref playerRunnerInventoryContainer, "PlayerRunnerInventoryContainer", "PlayerRunnerSlots");
        EnsureRoleContainer(ref playerSniperInventoryContainer, "PlayerSniperInventoryContainer", "PlayerSniperSlots");
    }

    private void EnsureRoleContainer(ref RectTransform container, string name, string altName)
    {
        if (container != null)
            return;

        RectTransform reference = playerRunnerInventoryContainer != null
            ? playerRunnerInventoryContainer
            : playerSniperInventoryContainer;

        Transform parent = reference != null ? reference.parent : transform;

        Transform t = FindInChildren(parent, name);
        if (t == null && altName != null)
            t = FindInChildren(parent, altName);

        if (t != null)
        {
            container = t as RectTransform;
            return;
        }

        if (reference == null)
            return;

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = reference.anchorMin;
        rect.anchorMax = reference.anchorMax;
        rect.pivot = reference.pivot;
        rect.anchoredPosition = reference.anchoredPosition;
        rect.sizeDelta = reference.sizeDelta;

        GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(140f, 140f);
        grid.spacing = new Vector2(10f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        container = rect;
    }

    private void OnModeOptionChanged(int index)
    {
        InventoryMode newMode;

        if (index == 1)
            newMode = InventoryMode.Runner;
        else if (index == 2)
            newMode = InventoryMode.Sniper;
        else
            newMode = InventoryMode.All;

        if (newMode == mode)
            return;

        mode = newMode;
        Refresh();
    }

    private void EnsureWindow()
    {
        if (windowRoot == null)
            windowRoot = transform as RectTransform;
    }

    private void EnsureCloseButtons()
    {
        if (closeButton1 != null && closeButton2 != null)
        {
            BindCloseButton(closeButton1);
            BindCloseButton(closeButton2);
            return;
        }

        Transform root = windowRoot != null ? windowRoot : transform;

        if (closeButton1 == null)
        {
            Transform t = root.Find("CloseButton");
            if (t != null)
                closeButton1 = t.GetComponent<Button>();
        }

        if (closeButton2 == null)
        {
            Transform t = root.Find("CloseButton2");
            if (t != null)
                closeButton2 = t.GetComponent<Button>();
        }

        if (closeButton1 == null)
            closeButton1 = CreateCloseButton("CloseButton", new Vector2(1f, 1f), new Vector2(-10f, -10f), "X", 40f, 40f);

        if (closeButton2 == null)
            closeButton2 = CreateCloseButton("CloseButton2", new Vector2(1f, 0f), new Vector2(-10f, 10f), "Close", 110f, 40f);

        BindCloseButton(closeButton1);
        BindCloseButton(closeButton2);
    }

    private Button CreateCloseButton(string name, Vector2 anchor, Vector2 position, string label, float width, float height)
    {
        RectTransform root = windowRoot != null ? windowRoot : transform as RectTransform;

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, height);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text text = labelGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.color = Color.white;
        text.font = FindFont();

        return button;
    }

    private void BindCloseButton(Button button)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(Close);
    }

    private void EnsureColorRefs()
    {
        if (colorPreview == null)
        {
            Transform t = transform.Find("ColorPreview");
            if (t != null)
                colorPreview = t.GetComponent<Image>();
        }
    }

    private void RefreshColor()
    {
        if (colorPreview != null)
            colorPreview.color = selectedColor;
    }

    private void OnColorChanged(Color32 newColor)
    {
        selectedColor = newColor;
        RefreshColor();
    }

    public void Refresh()
    {
        EnsureContainers();
        EnsureModeDropdown();
        EnsureBlockInventory();
        EnsureRoleContainers();
        EnsureRoleBlocks();

        if (inventorySlotsContainer == null)
            return;

        ApplyModeVisibility();

        ClearChildren(inventorySlotsContainer);
        if (playerRunnerInventoryContainer != null)
            ClearChildren(playerRunnerInventoryContainer);
        if (playerSniperInventoryContainer != null)
            ClearChildren(playerSniperInventoryContainer);

        BuildPlayerSlots(playerRunnerInventoryContainer, LocalPlayerSettings.GetRunnerEquipmentSlot);
        BuildPlayerSlots(playerSniperInventoryContainer, LocalPlayerSettings.GetSniperEquipmentSlot);

        bool playerFull = IsActivePlayerFull();

        List<string> inventory = LocalPlayerSettings.Inventory;

        for (int i = 0; i < inventory.Count; i++)
        {
            string itemId = inventory[i];

            if (string.IsNullOrEmpty(itemId))
                continue;

            ItemDefinition def = ResolveDef(itemId);

            if (def == null)
                continue;

            if (!MatchesMode(def))
                continue;

            CreateInventorySlot(itemId, def, playerFull);
        }
    }

    private void ApplyModeVisibility()
    {
        if (blockInventory != null)
            blockInventory.SetActive(mode == InventoryMode.All);

        if (blockRunner != null)
            blockRunner.SetActive(mode != InventoryMode.Runner);

        if (blockSniper != null)
            blockSniper.SetActive(mode != InventoryMode.Sniper);
    }

    private string GetActiveEquipmentSlot(int slotIndex)
    {
        switch (mode)
        {
            case InventoryMode.Runner:
                return LocalPlayerSettings.GetRunnerEquipmentSlot(slotIndex);
            case InventoryMode.Sniper:
                return LocalPlayerSettings.GetSniperEquipmentSlot(slotIndex);
            default:
                return "";
        }
    }

    private void SetActiveEquipmentSlot(int slotIndex, string itemId)
    {
        switch (mode)
        {
            case InventoryMode.Runner:
                LocalPlayerSettings.SetRunnerEquipmentSlot(slotIndex, itemId);
                break;
            case InventoryMode.Sniper:
                LocalPlayerSettings.SetSniperEquipmentSlot(slotIndex, itemId);
                break;
        }
    }

    private void ClearActiveEquipmentSlot(int slotIndex)
    {
        SetActiveEquipmentSlot(slotIndex, "");
    }

    private bool MatchesMode(ItemDefinition def)
    {
        switch (mode)
        {
            case InventoryMode.Runner:
                return def.Class == ItemClass.Universal || def.Class == ItemClass.Runner;
            case InventoryMode.Sniper:
                return def.Class == ItemClass.Universal || def.Class == ItemClass.Sniper;
            default:
                return true;
        }
    }

    private ItemDefinition ResolveDef(string itemId)
    {
        ItemDefinition def = ItemCatalog.Get(itemId);

        if (def != null)
            return def;

        ItemDefinition fallback = new ItemDefinition(itemId, itemId, ItemPurpose.Boost, new Color(0.6f, 0.6f, 0.65f, 1f));
        ItemCatalog.Register(fallback);
        return fallback;
    }

    private void BuildPlayerSlots(RectTransform container, System.Func<int, string> getSlot)
    {
        if (container == null)
            return;

        for (int i = 0; i < LocalPlayerSettings.EquipmentSlotsCount; i++)
        {
            string itemId = getSlot(i);
            ItemDefinition def = ItemCatalog.Get(itemId);
            CreatePlayerSlot(i, itemId, def, container);
        }
    }

    private bool IsActivePlayerFull()
    {
        int filled = 0;

        for (int i = 0; i < LocalPlayerSettings.EquipmentSlotsCount; i++)
        {
            if (!string.IsNullOrEmpty(GetActiveEquipmentSlot(i)))
                filled++;
        }

        return filled >= LocalPlayerSettings.EquipmentSlotsCount;
    }

    private void CreatePlayerSlot(int slotIndex, string itemId, ItemDefinition def, RectTransform container)
    {
        GameObject go = BuildSlot("PlayerSlot_" + slotIndex, container, 140f, 140f);

        if (def == null)
        {
            TMP_Text emptyName = FindText(go, "Name", "ItemNameTxt");
            if (emptyName != null)
                emptyName.text = "Empty";

            InventorySlot invSlot = go.GetComponent<InventorySlot>();

            if (invSlot != null && invSlot.LocationDropdown != null)
                invSlot.LocationDropdown.gameObject.SetActive(false);

            Transform drop = go.transform.Find("Dropdown");
            if (drop != null)
                drop.gameObject.SetActive(false);

            return;
        }

        ApplyVisuals(go, def);

        bool roleSlot = container == playerRunnerInventoryContainer || container == playerSniperInventoryContainer;

        string[] options = roleSlot
            ? new[] { "", "Take off", "Sell" }
            : new[] { "", "Inventory", "Sell" };

        int value = 0;

        CreateDropdown(go, options, value, option =>
        {
            if (option == "Take off" || option == "Inventory")
                MoveToInventory(itemId, slotIndex);
            else if (option == "Sell")
                SellItem(itemId, slotIndex, true);
        });
    }

    private void CreateInventorySlot(string itemId, ItemDefinition def, bool playerFull)
    {
        GameObject go = BuildSlot("Slot_" + itemId, inventorySlotsContainer, 140f, 140f);
        ApplyVisuals(go, def);

        bool canTake = mode != InventoryMode.All && !playerFull;

        string[] options = canTake
            ? new[] { "", "Take", "Sell" }
            : new[] { "", "Sell" };

        int value = 0;

        CreateDropdown(go, options, value, option =>
        {
            if (option == "Take")
                MoveToActiveEquipment(itemId);
            else if (option == "Sell")
                SellItem(itemId, -1, false);
        });
    }

    private GameObject BuildSlot(string name, RectTransform parent, float width, float height)
    {
        bool isPlayerContainer = parent == playerRunnerInventoryContainer ||
                                 parent == playerSniperInventoryContainer;

        GameObject prefab = isPlayerContainer && playerSlotPrefab != null ? playerSlotPrefab : slotPrefab;

        if (prefab != null)
        {
            GameObject go = Instantiate(prefab);
            go.name = name;
            go.transform.SetParent(parent, false);
            return go;
        }

        GameObject built = new GameObject(name, typeof(RectTransform));
        built.transform.SetParent(parent, false);

        LayoutElement element = built.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;

        Image bg = built.AddComponent<Image>();
        bg.color = new Color(0.16f, 0.16f, 0.2f, 0.9f);

        RectTransform iconRect = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
        iconRect.SetParent(built.transform, false);
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -8f);
        iconRect.sizeDelta = new Vector2(52f, 52f);
        iconRect.gameObject.AddComponent<Image>();

        RectTransform nameRect = new GameObject("Name", typeof(RectTransform)).GetComponent<RectTransform>();
        nameRect.SetParent(built.transform, false);
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -66f);
        nameRect.sizeDelta = new Vector2(-10f, 20f);

        TMP_Text nameText = nameRect.gameObject.AddComponent<TextMeshProUGUI>();
        nameText.font = FindFont();
        nameText.fontSize = 13f;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Center;

        return built;
    }

    private void ApplyVisuals(GameObject slot, ItemDefinition def)
    {
        Image icon = null;
        TMP_Text nameText = null;

        InventorySlot invSlot = slot.GetComponent<InventorySlot>();

        if (invSlot != null)
        {
            icon = invSlot.ObjectImg;
            nameText = invSlot.ItemNameTxt;
        }

        if (icon == null)
            icon = FindImage(slot, "Icon", "ObjectImg");

        if (nameText == null)
            nameText = FindText(slot, "Name", "ItemNameTxt");

        if (icon != null)
        {
            if (def.IconSprite != null)
            {
                icon.sprite = def.IconSprite;
                icon.color = Color.white;
            }
            else
            {
                icon.sprite = null;
                icon.color = def.IconColor;
            }
        }

        if (nameText != null)
            nameText.text = def.DisplayName;
    }

    private void CreateDropdown(GameObject slot, string[] options, int value, System.Action<string> onOptionSelected)
    {
        TMP_Dropdown tmp = null;

        InventorySlot invSlot = slot.GetComponent<InventorySlot>();
        if (invSlot != null)
            tmp = invSlot.LocationDropdown;

        if (tmp == null)
        {
            Transform existing = slot.transform.Find("Dropdown");
            if (existing != null)
                tmp = existing.GetComponent<TMP_Dropdown>();
        }

        if (tmp != null)
        {
            var texts = new List<string>(options);

            tmp.ClearOptions();
            tmp.AddOptions(texts);
            tmp.SetValueWithoutNotify(value);
            tmp.onValueChanged.RemoveAllListeners();
            tmp.onValueChanged.AddListener(index =>
            {
                if (index >= 0 && index < options.Length)
                    onOptionSelected(options[index]);
            });

            tmp.gameObject.SetActive(true);

            return;
        }

        GameObject dropdownGo;

        if (slot.transform.Find("Dropdown") is Transform existingDrop && existingDrop.GetComponent<InventorySlotDropdown>() != null)
        {
            dropdownGo = existingDrop.gameObject;
        }
        else
        {
            Transform existingChild = slot.transform.Find("Dropdown");
            if (existingChild != null)
                Destroy(existingChild.gameObject);

            dropdownGo = new GameObject("Dropdown", typeof(RectTransform));
            dropdownGo.transform.SetParent(slot.transform, false);

            RectTransform rect = dropdownGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 36f);
            rect.sizeDelta = new Vector2(118f, 28f);

            dropdownGo.AddComponent<InventorySlotDropdown>();
        }

        var custom = dropdownGo.GetComponent<InventorySlotDropdown>();
        custom.Init(canvas, new List<string>(options), value);
        custom.onOptionSelected = onOptionSelected;
    }

    private void MoveToActiveEquipment(string itemId)
    {
        int slot = -1;

        for (int i = 0; i < LocalPlayerSettings.EquipmentSlotsCount; i++)
        {
            if (string.IsNullOrEmpty(GetActiveEquipmentSlot(i)))
            {
                slot = i;
                break;
            }
        }

        if (slot < 0)
            return;

        LocalPlayerSettings.RemoveInventoryItem(itemId);
        SetActiveEquipmentSlot(slot, itemId);
        Refresh();
    }

    private void MoveToInventory(string itemId, int slotIndex)
    {
        ClearActiveEquipmentSlot(slotIndex);
        LocalPlayerSettings.AddInventoryItem(itemId);
        Refresh();
    }

    private void SellItem(string itemId, int slotIndex, bool fromEquipment)
    {
        if (fromEquipment)
        {
            if (slotIndex >= 0)
                ClearActiveEquipmentSlot(slotIndex);
        }
        else
        {
            LocalPlayerSettings.RemoveInventoryItem(itemId);
        }

        ItemDefinition def = ItemCatalog.Get(itemId);
        LocalPlayerSettings.AddPoints(def != null ? def.SellPrice : 0);

        Refresh();
    }

    private static Transform FindInChildren(Transform root, string name)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            if (child.name == name)
                return child;

            Transform nested = FindInChildren(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private Image FindImage(GameObject root, params string[] names)
    {
        foreach (string name in names)
        {
            Transform t = root.transform.Find(name);
            if (t != null)
            {
                Image img = t.GetComponent<Image>();
                if (img != null)
                    return img;
            }
        }

        return null;
    }

    private TMP_Text FindText(GameObject root, params string[] names)
    {
        foreach (string name in names)
        {
            Transform t = root.transform.Find(name);
            if (t != null)
            {
                TMP_Text text = t.GetComponent<TMP_Text>();
                if (text != null)
                    return text;
            }
        }

        return null;
    }

    private static void ClearChildren(RectTransform container)
    {
        if (container == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);

            if (child != null)
                Destroy(child.gameObject);
        }
    }

    private TMP_FontAsset FindFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        foreach (var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (text != null && text.font != null)
                return text.font;
        }

        return null;
    }
}

public class InventorySlotDropdown : MonoBehaviour
{
    public System.Action<string> onOptionSelected;

    private Canvas canvas;
    private RectTransform canvasRect;
    private TMP_Text caption;
    private List<string> options = new List<string>();
    private int value;
    private GameObject popup;
    private GameObject blocker;

    public void Init(Canvas canvas, List<string> options, int value)
    {
        this.canvas = canvas;

        if (canvas != null)
            canvasRect = canvas.transform as RectTransform;

        this.options = new List<string>(options);
        this.value = Mathf.Clamp(value, 0, this.options.Count - 1);

        EnsureVisuals();
        UpdateCaption();
    }

    public void SetValue(int newValue)
    {
        value = Mathf.Clamp(newValue, 0, this.options.Count - 1);
        UpdateCaption();
    }

    private void EnsureVisuals()
    {
        Image bg = GetComponent<Image>();

        if (bg == null)
        {
            bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        }

        Button button = GetComponent<Button>();

        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OpenPopup);

        Transform capT = transform.Find("Caption");

        if (capT == null)
        {
            GameObject capGo = new GameObject("Caption", typeof(RectTransform));
            capGo.transform.SetParent(transform, false);

            RectTransform capRect = capGo.GetComponent<RectTransform>();
            capRect.anchorMin = Vector2.zero;
            capRect.anchorMax = Vector2.one;
            capRect.offsetMin = Vector2.zero;
            capRect.offsetMax = Vector2.zero;

            caption = capGo.AddComponent<TextMeshProUGUI>();
            caption.font = FindFont();
            caption.fontSize = 13f;
            caption.color = Color.white;
            caption.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            caption = capT.GetComponent<TMP_Text>();
        }
    }

    private void OpenPopup()
    {
        ClosePopup();

        if (canvasRect == null)
            return;

        RectTransform myRect = transform as RectTransform;

        GameObject blockerGo = new GameObject("DropdownBlocker", typeof(RectTransform));
        blockerGo.transform.SetParent(canvasRect, false);

        RectTransform blockerRect = blockerGo.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;

        Image blockerImg = blockerGo.AddComponent<Image>();
        blockerImg.color = new Color(0f, 0f, 0f, 0.001f);

        Button blockerButton = blockerGo.AddComponent<Button>();
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.onClick.AddListener(ClosePopup);
        blocker = blockerGo;

        GameObject pop = new GameObject("DropdownList", typeof(RectTransform));
        pop.transform.SetParent(canvasRect, false);
        pop.transform.SetAsLastSibling();

        RectTransform popRect = pop.GetComponent<RectTransform>();
        float optionHeight = 30f;
        popRect.sizeDelta = new Vector2(140f, options.Count * optionHeight + 8f);
        popRect.pivot = new Vector2(0.5f, 1f);

        Image popBg = pop.AddComponent<Image>();
        popBg.color = new Color(0.13f, 0.13f, 0.17f, 1f);

        VerticalLayoutGroup layout = pop.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 2f;

        if (myRect != null)
        {
            Vector2 screen = new Vector2(myRect.position.x, myRect.position.y);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                popRect.anchoredPosition = local + new Vector2(0f, -6f);
        }

        for (int i = 0; i < options.Count; i++)
        {
            int index = i;
            string optionText = options[i];

            GameObject opt = new GameObject(optionText, typeof(RectTransform));
            opt.transform.SetParent(pop.transform, false);

            LayoutElement optElement = opt.AddComponent<LayoutElement>();
            optElement.preferredHeight = optionHeight;

            Image optBg = opt.AddComponent<Image>();
            optBg.color = index == value
                ? new Color(0.35f, 0.42f, 0.55f, 1f)
                : new Color(0.2f, 0.2f, 0.25f, 1f);

            Button optButton = opt.AddComponent<Button>();
            optButton.targetGraphic = optBg;

            TMP_Text optText = CreateOptionText(opt.transform, optionText);
            optText.color = index == value ? Color.white : new Color(0.75f, 0.8f, 1f);

            optButton.onClick.AddListener(() =>
            {
                value = index;
                UpdateCaption();
                ClosePopup();

                if (onOptionSelected != null)
                    onOptionSelected(optionText);
            });
        }

        popup = pop;
    }

    private TMP_Text CreateOptionText(Transform parent, string text)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 0f);
        rect.offsetMax = new Vector2(-8f, 0f);

        TMP_Text label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = FindFont();
        label.fontSize = 14f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;

        return label;
    }

    private void UpdateCaption()
    {
        if (caption == null)
            return;

        if (value >= 0 && value < options.Count)
            caption.text = options[value];
    }

    private void ClosePopup()
    {
        if (popup != null)
            Destroy(popup);

        if (blocker != null)
            Destroy(blocker);

        popup = null;
        blocker = null;
    }

    private void OnDisable()
    {
        ClosePopup();
    }

    private static TMP_FontAsset FindFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        foreach (var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (text != null && text.font != null)
                return text.font;
        }

        return null;
    }
}