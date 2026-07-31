using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SniperWeaponController : NetworkBehaviour
{
    [Header("Player references")]
    [SerializeField] private NetworkInventorySync inventorySync;
    [SerializeField] private PlayerRoleState roleState;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private SniperScopeUI scopeUI;
    [SerializeField] private WeaponContentDatabase contentDatabase;

    [Header("Rules")]
    [SerializeField] private bool requireSniperRole = true;
    [SerializeField] private bool allowUnassignedRoleForTesting = false;
    [SerializeField] private bool requireScopeToShoot = true;

    [Header("Diagnostics")]
    [SerializeField] private bool logSetupWarnings = true;

    [Header("Aim and laser")]
    [SerializeField] private LayerMask aimCollisionMask = ~0;
    [Min(1f)] [SerializeField] private float maximumAimDistance = 500f;
    [Min(1f)] [SerializeField] private float aimUpdatesPerSecond = 20f;
    [Min(0.1f)] [SerializeField] private float maximumCameraDistanceFromPlayer = 20f;
    [Min(0f)] [SerializeField] private float projectileSpawnOffset = 0.08f;

    private readonly NetworkVariable<int> currentAmmo = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString64Bytes> loadedBulletId = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<bool> networkAiming = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<Vector3> networkLaserEnd = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString4096Bytes> magazineBulletIds = new NetworkVariable<FixedString4096Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private sealed class MagazineState
    {
        public Queue<string> BulletIds = new Queue<string>();
    }

    private readonly Dictionary<string, MagazineState> serverMagazineCache = new Dictionary<string, MagazineState>();
    private readonly RaycastHit[] aimHitBuffer = new RaycastHit[32];

    private SniperRifleHeldVisual currentRifleVisual;
    private SniperRifleDefinition currentRifleDefinition;
    private BulletDefinition serverLoadedBullet;
    private string serverEquippedKey;
    private double serverNextAllowedShotTime;

    private bool localAiming;
    private float normalCameraFov;
    private float currentMagnification;
    private float nextAimSendTime;
    private Vector3 predictedLaserEnd;

    private Quaternion aimCameraNeutralLocalRotation;
    private bool aimCameraRotationCaptured;
    private float swayNoiseSeedX;
    private float swayNoiseSeedY;
    private float swayNoiseTime;
    private Vector2 swayOffset;
    private float currentRecoil;
    private float breathAmount;
    private bool isHoldingBreath;
    private bool breathDepleted;
    private float breathRecoveryTimer;
    private float breathPunishmentTimer;
    private double nextAllowedLocalShotTime;

    private float defSwayAmplitude = 0.1f;
    private float defSwayFrequency = 0.5f;
    private float defSwaySmoothTime = 0.3f;
    private float defMaxBreath = 5f;
    private float defBreathDepletionRate = 1f;
    private float defBreathRecoveryRate = 0.5f;
    private float defBreathRecoveryDelay = 1f;
    private float defBreathRecoveryThreshold = 0.3f;
    private float defBreathPunishmentDelay = 1f;
    private float defBreathPunishmentMultiplier = 3f;
    private float defBreathStabilityMultiplier = 0.05f;
    private float defRecoilPitchAmount = 0.15f;
    private float defRecoilRecoverySpeed = 3f;

    public int CurrentAmmo => currentAmmo.Value;
    public bool HasRifleEquipped => currentRifleDefinition != null;
    public float CurrentZoomFactor => currentRifleDefinition != null
        ? currentMagnification / Mathf.Max(currentRifleDefinition.MinimumMagnification, 0.001f)
        : 1f;
    public bool IsAiming => IsOwner ? localAiming : networkAiming.Value;
    public BulletDefinition LoadedBullet => ResolveLoadedBullet();
    public SniperRifleHeldVisual CurrentRifleVisual => currentRifleVisual;
    public PlayerRole CurrentRole => roleState != null ? roleState.CurrentRole : PlayerRole.None;

    public event System.Action<bool> OnLocalAimChanged;

    private void Awake()
    {
        if (inventorySync == null)
            inventorySync = GetComponent<NetworkInventorySync>();

        if (roleState == null)
            roleState = GetComponent<PlayerRoleState>();

        if (aimCamera == null)
            aimCamera = GetComponentInChildren<Camera>(true);

        if (scopeUI == null)
            scopeUI = GetComponentInChildren<SniperScopeUI>(true);

        if (inventorySync != null)
            inventorySync.OnHeldVisualChanged += HandleHeldVisualChanged;
    }

    private void OnDestroy()
    {
        if (inventorySync != null)
            inventorySync.OnHeldVisualChanged -= HandleHeldVisualChanged;
    }

    public override void OnNetworkSpawn()
    {
        currentAmmo.OnValueChanged += OnAmmoChanged;
        loadedBulletId.OnValueChanged += OnLoadedBulletChanged;
        networkAiming.OnValueChanged += OnNetworkAimingChanged;
        magazineBulletIds.OnValueChanged += OnMagazineBulletIdsChanged;

        if (IsOwner)
        {
            if (aimCamera == null || !aimCamera.gameObject.activeInHierarchy)
                aimCamera = Camera.main != null ? Camera.main : GetComponentInChildren<Camera>(true);

            if (scopeUI == null)
                scopeUI = FindFirstObjectByType<SniperScopeUI>(FindObjectsInactive.Include);

            if (scopeUI != null)
                scopeUI.Show(false);
        }

        HandleHeldVisualChanged(inventorySync != null ? inventorySync.CurrentHeldVisual : null);
        RefreshScopeUi();
    }

    public override void OnNetworkDespawn()
    {
        currentAmmo.OnValueChanged -= OnAmmoChanged;
        loadedBulletId.OnValueChanged -= OnLoadedBulletChanged;
        networkAiming.OnValueChanged -= OnNetworkAimingChanged;
        magazineBulletIds.OnValueChanged -= OnMagazineBulletIdsChanged;

        if (IsOwner)
        {
            EndLocalAim(false);
            if (scopeUI != null)
                scopeUI.Show(false);
        }
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
            return;

        EnsureCurrentWeaponDetected();
        HandleOwnerInput();
        HandleBreath();
    }

    private void LateUpdate()
    {
        UpdateScopeSway();

        if (aimCamera != null && aimCameraRotationCaptured)
        {
            if (localAiming)
            {
                Vector3 swayEuler = new Vector3(swayOffset.y, swayOffset.x, 0f);
                Vector3 recoilDown = new Vector3(-currentRecoil, 0f, 0f);
                aimCamera.transform.localRotation = aimCameraNeutralLocalRotation * Quaternion.Euler(swayEuler + recoilDown);
            }
            else
            {
                aimCamera.transform.localRotation = aimCameraNeutralLocalRotation;
            }
        }

        if (scopeUI != null)
        {
            if (localAiming)
                scopeUI.SetBreath(breathAmount / Mathf.Max(defMaxBreath, 0.001f));
            else
                scopeUI.HideBreathBar();
        }

        if (currentRifleVisual == null)
            return;

        bool visible = IsOwner ? false : networkAiming.Value;
        Vector3 endPoint = IsOwner && localAiming ? predictedLaserEnd : networkLaserEnd.Value;
        currentRifleVisual.SetLaser(visible, endPoint);
    }

    private void HandleOwnerInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        bool aimPressed = mouse.rightButton.wasPressedThisFrame;

        if (!CanUseRifleLocally())
        {
            if (aimPressed && logSetupWarnings)
                Debug.LogWarning(BuildLocalBlockReason(), this);

            if (localAiming)
                EndLocalAim(true);
            return;
        }

        if (aimPressed)
        {
            if (localAiming)
                EndLocalAim(true);
            else
                BeginLocalAim();
        }

        if (!localAiming)
            return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float direction = Mathf.Sign(scroll);
            currentMagnification = Mathf.Clamp(
                currentMagnification + direction * currentRifleDefinition.ZoomStep,
                currentRifleDefinition.MinimumMagnification,
                currentRifleDefinition.MaximumMagnification
            );

            ApplyMagnification();
        }

        GetCameraAim(out Vector3 cameraOrigin, out Vector3 cameraDirection);
        predictedLaserEnd = ComputeLaserEndLocally(cameraOrigin, cameraDirection);

        if (Time.unscaledTime >= nextAimSendTime)
        {
            nextAimSendTime = Time.unscaledTime + 1f / Mathf.Max(1f, aimUpdatesPerSecond);
            UpdateAimRpc(cameraOrigin, cameraDirection);
        }

        if (mouse.leftButton.wasPressedThisFrame && NetworkManager.ServerTime.Time >= nextAllowedLocalShotTime && currentAmmo.Value > 0)
        {
            currentRecoil = defRecoilPitchAmount;
            nextAllowedLocalShotTime = NetworkManager.ServerTime.Time + currentRifleDefinition.SecondsBetweenShots;
            FireRpc(cameraOrigin, cameraDirection);
        }
    }

    private void BeginLocalAim()
    {
        if (localAiming || currentRifleDefinition == null || aimCamera == null)
            return;

        localAiming = true;
        normalCameraFov = aimCamera.fieldOfView;
        currentMagnification = currentRifleDefinition.MinimumMagnification;
        ApplyMagnification();

        if (aimCamera != null)
        {
            aimCameraNeutralLocalRotation = aimCamera.transform.localRotation;
            aimCameraRotationCaptured = true;
        }

        swayOffset = Vector2.zero;
        currentRecoil = 0f;
        breathAmount = defMaxBreath;
        isHoldingBreath = false;
        breathDepleted = false;
        breathRecoveryTimer = 0f;
        breathPunishmentTimer = 0f;
        swayNoiseTime = 0f;
        swayNoiseSeedX = Random.Range(0f, 1000f);
        swayNoiseSeedY = Random.Range(0f, 1000f);

        if (scopeUI != null)
        {
            scopeUI.Show(true);
            RefreshScopeUi();
        }

        GetCameraAim(out Vector3 origin, out Vector3 direction);
        predictedLaserEnd = ComputeLaserEndLocally(origin, direction);
        SetAimingRpc(true, origin, direction);

        OnLocalAimChanged?.Invoke(true);
    }

    private void EndLocalAim(bool notifyServer)
    {
        if (!localAiming)
            return;

        localAiming = false;

        if (aimCamera != null)
        {
            aimCamera.fieldOfView = normalCameraFov;
            aimCamera.transform.localRotation = aimCameraNeutralLocalRotation;
        }

        if (scopeUI != null)
        {
            scopeUI.Show(false);
            scopeUI.HideBreathBar();
        }

        if (currentRifleVisual != null)
            currentRifleVisual.SetLaser(false, currentRifleVisual.LaserOrigin.position);

        if (notifyServer && IsSpawned)
            SetAimingRpc(false, Vector3.zero, Vector3.forward);

        OnLocalAimChanged?.Invoke(false);
    }

    private void HandleBreath()
    {
        if (!localAiming)
        {
            isHoldingBreath = false;
            return;
        }

        bool altHeld = Keyboard.current != null && Keyboard.current.leftAltKey.isPressed;

        if (altHeld && breathAmount > 0f && !breathDepleted)
        {
            isHoldingBreath = true;
            breathAmount -= Time.deltaTime * defBreathDepletionRate;
            breathRecoveryTimer = 0f;

            if (breathAmount <= 0f)
            {
                breathAmount = 0f;
                breathDepleted = true;
                breathPunishmentTimer = 0f;
            }
        }
        else
        {
            isHoldingBreath = false;

            if (breathDepleted)
            {
                breathPunishmentTimer += Time.deltaTime;

                if (breathAmount >= defMaxBreath * defBreathRecoveryThreshold)
                {
                    breathDepleted = false;
                    breathPunishmentTimer = 0f;
                }

                breathRecoveryTimer += Time.deltaTime;
                if (breathRecoveryTimer >= defBreathPunishmentDelay)
                    breathAmount = Mathf.Min(breathAmount + Time.deltaTime * defBreathRecoveryRate, defMaxBreath);
            }
            else
            {
                breathRecoveryTimer += Time.deltaTime;
                if (breathRecoveryTimer >= defBreathRecoveryDelay)
                    breathAmount = Mathf.Min(breathAmount + Time.deltaTime * defBreathRecoveryRate, defMaxBreath);
            }
        }
    }

    private void UpdateScopeSway()
    {
        if (!localAiming)
        {
            currentRecoil = Mathf.MoveTowards(currentRecoil, 0f, Time.deltaTime * defRecoilRecoverySpeed);
            swayOffset = Vector2.Lerp(swayOffset, Vector2.zero, Time.deltaTime / defSwaySmoothTime);
            return;
        }

        swayNoiseTime += Time.deltaTime * defSwayFrequency;

        float sampleX = Mathf.PerlinNoise(swayNoiseSeedX, swayNoiseTime);
        float sampleY = Mathf.PerlinNoise(swayNoiseSeedY, swayNoiseTime + 100f);
        Vector2 targetSway = new Vector2((sampleX - 0.5f) * 2f, (sampleY - 0.5f) * 2f);

        float amplitude = defSwayAmplitude;
        if (isHoldingBreath)
            amplitude *= defBreathStabilityMultiplier;
        else if (breathDepleted)
            amplitude *= defBreathPunishmentMultiplier;

        targetSway *= amplitude;

        swayOffset = Vector2.Lerp(swayOffset, targetSway, Time.deltaTime / defSwaySmoothTime);

        currentRecoil = Mathf.MoveTowards(currentRecoil, 0f, Time.deltaTime * defRecoilRecoverySpeed);
    }

    private void ApplyMagnification()
    {
        if (aimCamera == null || currentRifleDefinition == null)
            return;

        float halfFovRadians = normalCameraFov * 0.5f * Mathf.Deg2Rad;
        float zoomedHalfFov = Mathf.Atan(Mathf.Tan(halfFovRadians) / currentMagnification);
        aimCamera.fieldOfView = zoomedHalfFov * 2f * Mathf.Rad2Deg;

        if (scopeUI != null)
        {
            scopeUI.SetZoom(
                currentMagnification,
                currentRifleDefinition.MinimumMagnification,
                currentRifleDefinition.MaximumMagnification
            );
        }
    }

    private void HandleHeldVisualChanged(GameObject heldVisual)
    {
        if (currentRifleVisual != null)
            currentRifleVisual.SetLaser(false, currentRifleVisual.LaserOrigin.position);

        currentRifleVisual = heldVisual != null
            ? heldVisual.GetComponentInChildren<SniperRifleHeldVisual>(true)
            : null;

        currentRifleDefinition = currentRifleVisual != null
            ? currentRifleVisual.Definition
            : null;

        CacheRifleStats();

        if (heldVisual != null && currentRifleVisual == null && logSetupWarnings)
        {
            Debug.LogWarning(
                $"The held object '{heldVisual.name}' does not contain SniperRifleHeldVisual. " +
                "Add SniperRifleHeldVisual to the Held Visual Prefab, not only to the world pickup prefab.",
                heldVisual
            );
        }
        else if (currentRifleVisual != null && currentRifleDefinition == null && logSetupWarnings)
        {
            Debug.LogWarning(
                $"SniperRifleHeldVisual on '{heldVisual.name}' has no Sniper Rifle Definition assigned.",
                currentRifleVisual
            );
        }

        if (IsServer)
            LoadServerMagazineForEquippedWeapon();

        if (IsOwner)
        {
            if (currentRifleDefinition == null && localAiming)
                EndLocalAim(true);

            RefreshScopeUi();
        }
    }

    private void CacheRifleStats()
    {
        if (currentRifleDefinition != null)
        {
            defSwayAmplitude = currentRifleDefinition.SwayAmplitude;
            defSwayFrequency = currentRifleDefinition.SwayFrequency;
            defSwaySmoothTime = currentRifleDefinition.SwaySmoothTime;
            defMaxBreath = currentRifleDefinition.MaxBreath;
            defBreathDepletionRate = currentRifleDefinition.BreathDepletionRate;
            defBreathRecoveryRate = currentRifleDefinition.BreathRecoveryRate;
            defBreathRecoveryDelay = currentRifleDefinition.BreathRecoveryDelay;
            defBreathRecoveryThreshold = currentRifleDefinition.BreathRecoveryThreshold;
            defBreathPunishmentDelay = currentRifleDefinition.BreathPunishmentDelay;
            defBreathPunishmentMultiplier = currentRifleDefinition.BreathPunishmentMultiplier;
            defBreathStabilityMultiplier = currentRifleDefinition.BreathStabilityMultiplier;
            defRecoilPitchAmount = currentRifleDefinition.RecoilPitchAmount;
            defRecoilRecoverySpeed = currentRifleDefinition.RecoilRecoverySpeed;
        }
    }

    private void LoadServerMagazineForEquippedWeapon()
    {
        SaveCurrentServerMagazine();

        if (currentRifleDefinition == null || inventorySync == null)
        {
            serverEquippedKey = null;
            serverLoadedBullet = null;
            currentAmmo.Value = 0;
            loadedBulletId.Value = default;
            networkAiming.Value = false;
            return;
        }

        string itemName = inventorySync.NetworkActiveItemName;
        int slot = inventorySync.NetworkActiveSlot;
        string newKey = $"{slot}|{itemName}|{currentRifleDefinition.RifleId}";

        serverEquippedKey = newKey;

        if (!serverMagazineCache.TryGetValue(newKey, out MagazineState state))
        {
            state = new MagazineState();
            for (int i = 0; i < currentRifleDefinition.MagazineSize; i++)
                state.BulletIds.Enqueue(currentRifleDefinition.DefaultBullet.BulletId);
            serverMagazineCache[newKey] = state;
        }

        if (state.BulletIds.Count > 0)
        {
            string frontId = state.BulletIds.Peek();
            serverLoadedBullet = ResolveBulletDefinition(frontId);
            loadedBulletId.Value = new FixedString64Bytes(frontId);
        }
        else
        {
            serverLoadedBullet = null;
            loadedBulletId.Value = default;
        }

        currentAmmo.Value = state.BulletIds.Count;
        SyncMagazineBulletIds();
    }

    private void SaveCurrentServerMagazine()
    {
    }

    private void SyncMagazineBulletIds()
    {
        if (!IsServer)
            return;

        if (!serverMagazineCache.TryGetValue(serverEquippedKey, out MagazineState state))
        {
            magazineBulletIds.Value = default;
            return;
        }

        string joined = string.Join("|", state.BulletIds.ToArray());
        magazineBulletIds.Value = new FixedString4096Bytes(joined);
        PushMagazineStateClientRpc(joined);
    }

    [Rpc(SendTo.Owner)]
    private void PushMagazineStateClientRpc(string bulletsStr)
    {
        RefreshScopeUi();
    }

    [Rpc(SendTo.Server)]
    private void SetAimingRpc(
        bool aiming,
        Vector3 cameraOrigin,
        Vector3 cameraDirection,
        RpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams))
            return;

        if (!aiming)
        {
            networkAiming.Value = false;
            return;
        }

        if (!CanUseRifleOnServer() || !IsAimOriginValid(cameraOrigin))
        {
            networkAiming.Value = false;
            return;
        }

        networkAiming.Value = true;
        networkLaserEnd.Value = ComputeLaserEndOnServer(cameraOrigin, cameraDirection);
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
    private void UpdateAimRpc(
        Vector3 cameraOrigin,
        Vector3 cameraDirection,
        RpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams) || !networkAiming.Value)
            return;

        if (!CanUseRifleOnServer() || !IsAimOriginValid(cameraOrigin))
        {
            networkAiming.Value = false;
            return;
        }

        networkLaserEnd.Value = ComputeLaserEndOnServer(cameraOrigin, cameraDirection);
    }

    [Rpc(SendTo.Server)]
    private void FireRpc(
        Vector3 cameraOrigin,
        Vector3 cameraDirection,
        RpcParams rpcParams = default)
    {
        if (!IsRequestFromOwner(rpcParams) || !CanUseRifleOnServer())
            return;

        if (requireScopeToShoot && !networkAiming.Value)
            return;

        if (!IsAimOriginValid(cameraOrigin))
            return;

        if (currentAmmo.Value <= 0 || serverLoadedBullet == null)
            return;

        double serverTime = NetworkManager.ServerTime.Time;
        if (serverTime < serverNextAllowedShotTime)
            return;

        if (currentRifleDefinition.ProjectilePrefab == null || currentRifleVisual == null)
            return;

        Transform muzzle = currentRifleVisual.Muzzle;
        Vector3 laserEnd = ComputeLaserEndOnServer(cameraOrigin, cameraDirection);
        Vector3 shotDirection = laserEnd - muzzle.position;

        if (shotDirection.sqrMagnitude < 0.0001f)
            shotDirection = muzzle.forward;
        else
            shotDirection.Normalize();

        Vector3 spawnPosition = muzzle.position + shotDirection * projectileSpawnOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(shotDirection, Vector3.up);

        GameObject projectileObject = Instantiate(
            currentRifleDefinition.ProjectilePrefab,
            spawnPosition,
            spawnRotation
        );

        NetworkObject projectileNetworkObject = projectileObject.GetComponent<NetworkObject>();
        NetworkProjectile projectile = projectileObject.GetComponent<NetworkProjectile>();

        if (projectileNetworkObject == null || projectile == null)
        {
            Debug.LogError(
                "Projectile Prefab must contain both NetworkObject and NetworkProjectile.",
                projectileObject
            );
            Destroy(projectileObject);
            return;
        }

        projectileNetworkObject.Spawn(true);
        projectile.InitializeServer(
            serverLoadedBullet,
            currentRifleDefinition.MuzzleVelocity,
            OwnerClientId,
            currentRifleDefinition.DeathTorque
        );

        if (serverMagazineCache.TryGetValue(serverEquippedKey, out MagazineState fireState))
        {
            if (fireState.BulletIds.Count > 0)
                fireState.BulletIds.Dequeue();

            if (fireState.BulletIds.Count > 0)
            {
                string nextId = fireState.BulletIds.Peek();
                serverLoadedBullet = ResolveBulletDefinition(nextId);
                loadedBulletId.Value = new FixedString64Bytes(nextId);
            }
            else
            {
                serverLoadedBullet = null;
                loadedBulletId.Value = default;
            }

            currentAmmo.Value = fireState.BulletIds.Count;
        }

        SyncMagazineBulletIds();
        serverNextAllowedShotTime = serverTime + currentRifleDefinition.SecondsBetweenShots;
        networkLaserEnd.Value = laserEnd;
    }

    public bool ServerRefill(BulletDefinition bullet)
    {
        if (!IsServer || bullet == null || !CanUseRifleOnServer() || currentRifleDefinition == null)
            return false;

        if (!serverMagazineCache.TryGetValue(serverEquippedKey, out MagazineState state))
        {
            state = new MagazineState();
            serverMagazineCache[serverEquippedKey] = state;
        }

        if (state.BulletIds.Count >= currentRifleDefinition.MagazineSize)
            return false;

        state.BulletIds.Enqueue(bullet.BulletId);

        if (state.BulletIds.Count == 1)
        {
            serverLoadedBullet = bullet;
            loadedBulletId.Value = new FixedString64Bytes(bullet.BulletId);
        }

        currentAmmo.Value = state.BulletIds.Count;
        SyncMagazineBulletIds();
        return true;
    }

    [Rpc(SendTo.Server)]
    public void RefillFromAmmoBoxServerRpc(FixedString64Bytes bulletId)
    {
        ServerRefillFromAmmoBox(bulletId);
    }

    public bool ServerRefillFromAmmoBox(FixedString64Bytes bulletId)
    {
        BulletDefinition bullet = ResolveBulletDefinition(bulletId.ToString());
        return bullet != null && ServerRefill(bullet);
    }

    private bool CanUseRifleLocally()
    {
        if (currentRifleDefinition == null || currentRifleVisual == null || aimCamera == null)
            return false;

        if (!requireSniperRole)
            return true;

        if (roleState == null)
            return false;

        return roleState.IsSniper ||
               (allowUnassignedRoleForTesting && roleState.CurrentRole == PlayerRole.None);
    }

    private bool CanUseRifleOnServer()
    {
        if (!IsServer || currentRifleDefinition == null || currentRifleVisual == null)
            return false;

        if (!requireSniperRole)
            return true;

        if (roleState == null)
            return false;

        return roleState.IsSniper ||
               (allowUnassignedRoleForTesting && roleState.CurrentRole == PlayerRole.None);
    }

    private void EnsureCurrentWeaponDetected()
    {
        if (inventorySync == null)
            return;

        GameObject heldVisual = inventorySync.CurrentHeldVisual;
        SniperRifleHeldVisual detectedRifle = heldVisual != null
            ? heldVisual.GetComponentInChildren<SniperRifleHeldVisual>(true)
            : null;

        if (detectedRifle != currentRifleVisual)
            HandleHeldVisualChanged(heldVisual);
    }

    private string BuildLocalBlockReason()
    {
        if (!IsSpawned)
            return "Sniper input is blocked because the player NetworkObject is not spawned.";

        if (!IsOwner)
            return "Sniper input is blocked because this player object is not owned by the local client.";

        if (inventorySync == null)
            return "Sniper input is blocked: NetworkInventorySync is not assigned or missing on the player.";

        if (inventorySync.CurrentHeldVisual == null)
            return "Sniper input is blocked: there is no active Held Visual. Select the rifle inventory slot.";

        if (currentRifleVisual == null)
            return "Sniper input is blocked: the Held Visual Prefab has no SniperRifleHeldVisual component.";

        if (currentRifleDefinition == null)
            return "Sniper input is blocked: SniperRifleHeldVisual has no Definition assigned.";

        if (aimCamera == null)
            return "Sniper input is blocked: Aim Camera is not assigned and no child/Main Camera was found.";

        if (requireSniperRole && roleState == null)
            return "Sniper input is blocked: PlayerRoleState is missing while Require Sniper Role is enabled.";

        if (requireSniperRole && roleState != null && !roleState.IsSniper &&
            !(allowUnassignedRoleForTesting && roleState.CurrentRole == PlayerRole.None))
        {
            return $"Sniper input is blocked: current role is {roleState.CurrentRole}, but Sniper is required.";
        }

        return "Sniper input is blocked by an unknown setup problem.";
    }

    [ContextMenu("Print Sniper Runtime State")]
    private void PrintSniperRuntimeState()
    {
        string heldName = inventorySync != null && inventorySync.CurrentHeldVisual != null
            ? inventorySync.CurrentHeldVisual.name
            : "NULL";

        Debug.Log(
            $"Sniper state | Spawned={IsSpawned}, Owner={IsOwner}, Server={IsServer}, " +
            $"Role={CurrentRole}, HeldVisual={heldName}, RifleVisual={(currentRifleVisual != null)}, " +
            $"Definition={(currentRifleDefinition != null)}, Camera={(aimCamera != null)}, " +
            $"ScopeUI={(scopeUI != null)}, Ammo={currentAmmo.Value}, CanUseLocal={CanUseRifleLocally()}",
            this
        );
    }

    private bool IsRequestFromOwner(RpcParams rpcParams)
    {
        return rpcParams.Receive.SenderClientId == OwnerClientId;
    }

    private bool IsAimOriginValid(Vector3 cameraOrigin)
    {
        return Vector3.Distance(transform.position, cameraOrigin) <= maximumCameraDistanceFromPlayer;
    }

    private Vector3 ComputeLaserEndOnServer(Vector3 cameraOrigin, Vector3 cameraDirection)
    {
        cameraDirection = cameraDirection.sqrMagnitude > 0.0001f
            ? cameraDirection.normalized
            : transform.forward;

        Vector3 cameraTarget = cameraOrigin + cameraDirection * maximumAimDistance;
        if (TryRaycastIgnoringSelf(cameraOrigin, cameraDirection, maximumAimDistance, out RaycastHit cameraHit))
            cameraTarget = cameraHit.point;

        Transform laserOrigin = currentRifleVisual != null
            ? currentRifleVisual.LaserOrigin
            : transform;

        Vector3 muzzleDirection = cameraTarget - laserOrigin.position;
        if (muzzleDirection.sqrMagnitude < 0.0001f)
            muzzleDirection = laserOrigin.forward;
        else
            muzzleDirection.Normalize();

        if (TryRaycastIgnoringSelf(laserOrigin.position, muzzleDirection, maximumAimDistance, out RaycastHit muzzleHit))
            return muzzleHit.point;

        return laserOrigin.position + muzzleDirection * maximumAimDistance;
    }

    private Vector3 ComputeLaserEndLocally(Vector3 cameraOrigin, Vector3 cameraDirection)
    {
        if (currentRifleVisual == null)
            return cameraOrigin + cameraDirection * maximumAimDistance;

        Vector3 cameraTarget = cameraOrigin + cameraDirection * maximumAimDistance;
        if (TryRaycastIgnoringSelf(cameraOrigin, cameraDirection, maximumAimDistance, out RaycastHit cameraHit))
            cameraTarget = cameraHit.point;

        Transform laserOrigin = currentRifleVisual.LaserOrigin;
        Vector3 muzzleDirection = cameraTarget - laserOrigin.position;
        if (muzzleDirection.sqrMagnitude < 0.0001f)
            muzzleDirection = laserOrigin.forward;
        else
            muzzleDirection.Normalize();

        if (TryRaycastIgnoringSelf(laserOrigin.position, muzzleDirection, maximumAimDistance, out RaycastHit muzzleHit))
            return muzzleHit.point;

        return laserOrigin.position + muzzleDirection * maximumAimDistance;
    }

    private bool TryRaycastIgnoringSelf(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit nearestHit)
    {
        int count = Physics.RaycastNonAlloc(
            origin,
            direction,
            aimHitBuffer,
            distance,
            aimCollisionMask,
            QueryTriggerInteraction.Ignore
        );

        nearestHit = default;
        float nearestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = aimHitBuffer[i];
            if (candidate.collider == null)
                continue;

            Transform hitTransform = candidate.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (candidate.distance >= nearestDistance)
                continue;

            nearestHit = candidate;
            nearestDistance = candidate.distance;
            found = true;
        }

        return found;
    }

    private void GetCameraAim(out Vector3 origin, out Vector3 direction)
    {
        if (aimCamera != null)
        {
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            origin = ray.origin;
            direction = ray.direction.normalized;
            return;
        }

        origin = transform.position + Vector3.up * 1.5f;
        direction = transform.forward;
    }

    private BulletDefinition ResolveLoadedBullet()
    {
        string id = loadedBulletId.Value.ToString();
        return ResolveBulletDefinition(id);
    }

    private BulletDefinition ResolveBulletDefinition(string bulletId)
    {
        if (string.IsNullOrEmpty(bulletId))
            return null;

        if (currentRifleDefinition != null &&
            currentRifleDefinition.DefaultBullet != null &&
            currentRifleDefinition.DefaultBullet.BulletId == bulletId)
        {
            return currentRifleDefinition.DefaultBullet;
        }

        BulletDefinition bullet = contentDatabase != null ? contentDatabase.GetBullet(bulletId) : null;

        if (bullet != null)
            return bullet;

        return null;
    }

    private void RefreshScopeUi()
    {
        if (!IsOwner || scopeUI == null)
            return;

        string bulletsStr = magazineBulletIds.Value.ToString();
        string[] ids;

        if (string.IsNullOrEmpty(bulletsStr))
        {
            if (currentRifleDefinition != null && currentRifleDefinition.DefaultBullet != null && currentAmmo.Value > 0)
            {
                ids = new string[currentAmmo.Value];
                for (int i = 0; i < ids.Length; i++)
                    ids[i] = currentRifleDefinition.DefaultBullet.BulletId;
            }
            else
            {
                ids = System.Array.Empty<string>();
            }
        }
        else
        {
            ids = bulletsStr.Split('|');
        }

        var bulletDefs = new List<BulletDefinition>(ids.Length);
        foreach (string id in ids)
            bulletDefs.Add(ResolveBulletDefinition(id));

        scopeUI.SetBullets(bulletDefs);

        if (currentRifleDefinition != null)
        {
            float displayedMagnification = localAiming
                ? currentMagnification
                : currentRifleDefinition.MinimumMagnification;

            scopeUI.SetZoom(
                displayedMagnification,
                currentRifleDefinition.MinimumMagnification,
                currentRifleDefinition.MaximumMagnification
            );
        }
    }

    private void OnAmmoChanged(int oldValue, int newValue)
    {
        RefreshScopeUi();
    }

    private void OnLoadedBulletChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        RefreshScopeUi();
    }

    private void OnNetworkAimingChanged(bool oldValue, bool newValue)
    {
        if (!newValue && currentRifleVisual != null && !IsOwner)
            currentRifleVisual.SetLaser(false, currentRifleVisual.LaserOrigin.position);
    }

    private void OnMagazineBulletIdsChanged(FixedString4096Bytes oldValue, FixedString4096Bytes newValue)
    {
        RefreshScopeUi();
    }
}
