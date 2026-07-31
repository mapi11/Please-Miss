using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Look")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;

    [SerializeField] public float mouseSensitivity = 0.08f;
    [SerializeField, Range(0f, 1f)] private float zoomSensitivityMultiplier = 1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.2f;
    [SerializeField] private float sprintSpeed = 6.2f;
    [SerializeField] private float moveSmoothTime = 0.08f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float jumpStickToGroundForce = -2f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float jumpSquash = 0.88f;

    [Header("Crouch")]
    [SerializeField] private float standingHeight = 1.75f;
    [SerializeField] private float crouchHeight = 1.0f;
    [SerializeField] private float crouchBodyLowering = 0.45f;

    [SerializeField] private float standingCameraY = 1.55f;
    [SerializeField] private float crouchCameraY = 0.92f;

    [SerializeField] private float crouchSpeed = 10f;

    [Header("Visual Body")]
    [SerializeField] private Transform bodyVisual;
    [SerializeField] private Transform headVisual;
    [SerializeField] private Transform leftShoulder;
    [SerializeField] private Transform rightShoulder;

    [Header("Arms")]
    [SerializeField, Range(0f, 1f)] private float shoulderPitchInfluence = 0.35f;

    [Header("Hands")]
    [SerializeField] private Hand rightHand;
    [SerializeField] private Hand leftHand;

    public enum InteractionHand { Right, Left }

    [Header("Interaction")]
    [SerializeField] private InteractionHand interactionHand = InteractionHand.Right;
    [SerializeField] private float interactionRadius = 2.2f;
    [SerializeField] private float interactionViewDot = 0.35f;
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private float handActivationDistance = 0.12f;
    [SerializeField] private float interactionReachSmoothTime = 0.09f;

    [Header("Inventory")]
    [SerializeField] private Inventory inventory;

    [Header("Crosshair")]
    [SerializeField] private GameObject crosshair;

    [Header("Throw")]
    [SerializeField] private float maxChargeTime = 2f;
    [SerializeField] private ChargeSlider chargeSlider;

    [Header("Multiplayer")]
    [SerializeField] private int localOnlyLayer = 6;

    [Header("Stamina")]
    [SerializeField] private Stamina stamina;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Role")]
    [SerializeField] private PlayerRoleState roleState;
    [SerializeField] private SniperWeaponController sniperWeapon;

    [Header("Audio")]
    [SerializeField] private AudioListener audioListener;

    [Header("Debug")]
    [SerializeField] private bool isCrouching;
    [SerializeField] private Interactable currentInteractable;

    private CharacterController characterController;
    private Rigidbody rb;

    private Vector2 currentMoveInput;
    private Vector2 moveInputVelocity;

    private float verticalVelocity;
    private float pitch;

    private bool handReachedInteractable;
    private Hand reachingHand;
    private bool isChargingThrow;
    private float throwChargeTime;
    private NetworkInventorySync cachedSync;
    private float shoulderY;
    private float airborneSquash = 1f;
    private float bodyInitialY;
    private Vector3 leftShoulderInitialEuler;
    private Vector3 rightShoulderInitialEuler;
    private bool isFrozen;
    private bool isPointing;
    private bool sniperAiming;
    private Transform pointTarget;
    private Transform idleHandTarget;

    private bool isTwoHandedHolding;
    private TwoHandedHold currentTwoHanded;

    public bool IsCrouching => isCrouching;
    public float Pitch => pitch;
    public bool IsChargingThrow => isChargingThrow;
    public bool IsFrozen => isFrozen;
    public Interactable CurrentInteractable => currentInteractable;
    public bool HasCurrentInteractable => currentInteractable != null;
    public Inventory PlayerInventory => inventory;
    public Hand RightHand => rightHand;
    public Hand LeftHand => leftHand;
    public InteractionHand SelectedInteractionHand => interactionHand;
    public float AirborneSquash => airborneSquash;
    public Transform ActiveHandHoldPivot => InteractionHandRef != null ? InteractionHandRef.HoldPivot : null;

    private Hand InteractionHandRef =>
        isTwoHandedHolding && currentTwoHanded != null
            ? leftHand
            : interactionHand == InteractionHand.Right ? rightHand : leftHand;
    private Hand PointingHandRef => interactionHand == InteractionHand.Right ? leftHand : rightHand;
    private Hand IdleHandRef => interactionHand == InteractionHand.Right ? rightHand : leftHand;

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        characterController.height = standingHeight;
        characterController.center = new Vector3(0f, standingHeight * 0.5f, 0f);

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (audioListener == null && playerCamera != null)
            audioListener = playerCamera.GetComponent<AudioListener>();

        if (rightHand == null || leftHand == null)
        {
            var allHands = GetComponentsInChildren<Hand>(true);
            foreach (var h in allHands)
            {
                if (h.name.Contains("Left"))
                    leftHand = h;
                else if (h.name.Contains("Right"))
                    rightHand = h;
            }

            if (rightHand == null || leftHand == null)
            {
                foreach (var h in allHands)
                {
                    if (h.transform.localPosition.x < 0f)
                        leftHand = h;
                    else if (rightHand == null)
                        rightHand = h;
                }
            }
        }

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (stamina == null)
            stamina = GetComponent<Stamina>();

        if (roleState == null)
            roleState = GetComponent<PlayerRoleState>();

        if (sniperWeapon == null)
            sniperWeapon = GetComponent<SniperWeaponController>();

        if (bodyVisual != null)
            bodyInitialY = bodyVisual.localPosition.y;

        if (leftShoulder != null)
        {
            shoulderY = leftShoulder.localPosition.y;
            leftShoulderInitialEuler = leftShoulder.localEulerAngles;
        }

        if (rightShoulder != null)
            rightShoulderInitialEuler = rightShoulder.localEulerAngles;

        pointTarget = new GameObject("PointTarget").transform;
        pointTarget.SetParent(cameraPivot != null ? cameraPivot : playerCamera.transform);
        pointTarget.localPosition = new Vector3(0f, -0.15f, 0.85f);

        idleHandTarget = new GameObject("IdleHandTarget").transform;
        idleHandTarget.SetParent(transform);
        idleHandTarget.localPosition = Vector3.zero;

        var netSync = GetComponent<NetworkInventorySync>();
        if (netSync != null)
            netSync.OnHeldVisualChanged += OnHeldVisualChanged;
    }

    public override void OnNetworkSpawn()
    {
        lobbyChecked = false;
        isFrozen = true;

        bool local = IsOwner;

        if (playerCamera != null)
            playerCamera.enabled = local;

        if (audioListener != null)
            audioListener.enabled = local;

        if (local)
        {
            if (headVisual != null)
                SetLayerRecursively(headVisual, localOnlyLayer);

            if (bodyVisual != null)
                SetLayerRecursively(bodyVisual, localOnlyLayer);

            int hideMask = 1 << localOnlyLayer;
            if (playerCamera != null)
                playerCamera.cullingMask &= ~hideMask;

            isFrozen = true;
        }
        else
        {
            if (crosshair != null)
                crosshair.SetActive(false);
        }

        if (IsOwner && sniperWeapon != null)
            sniperWeapon.OnLocalAimChanged += HandleSniperAimChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && sniperWeapon != null)
            sniperWeapon.OnLocalAimChanged -= HandleSniperAimChanged;

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private bool lobbyChecked;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        var netSync = GetComponent<NetworkInventorySync>();
        if (netSync != null)
            netSync.OnHeldVisualChanged -= OnHeldVisualChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lobbyChecked = false;
    }

    private void OnHeldVisualChanged(GameObject heldVisual)
    {
        ClearTwoHandedGrip();
        currentTwoHanded = null;

        if (heldVisual == null)
            return;

        currentTwoHanded = heldVisual.GetComponent<TwoHandedHold>();
        if (currentTwoHanded != null && currentTwoHanded.IsValid)
            SetTwoHandedGrip(currentTwoHanded.LeftGrip);
    }

    private void SetTwoHandedGrip(Transform leftGrip)
    {
        isTwoHandedHolding = true;

        if (leftHand != null)
        {
            leftHand.snapToTarget = true;
            leftHand.targetFollowSmoothTimeOverride = -1f;
            leftHand.SetTarget(leftGrip);
        }
    }

    private void ClearTwoHandedGrip()
    {
        if (!isTwoHandedHolding)
            return;

        isTwoHandedHolding = false;

        if (leftHand != null)
        {
            leftHand.snapToTarget = false;
            leftHand.ClearTarget();
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            if (characterController != null && characterController.enabled)
                characterController.enabled = false;
            return;
        }

        if (!lobbyChecked)
        {
            lobbyChecked = true;
            bool inLobby = FindObjectOfType<LobbyManager>() != null;

            if (inLobby)
            {
                SetFrozen(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (crosshair != null)
                    crosshair.SetActive(false);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (isFrozen)
        {
            if (characterController != null && characterController.enabled)
                characterController.enabled = false;
            return;
        }

        if (characterController != null && !characterController.enabled)
            return;

        bool uiOpen = (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen)
                    || GameManager.LocalRunnerFinished
                    || (GameManager.Instance != null && GameManager.Instance.State.Value == GameManager.GameState.Ended)
                    || (playerHealth != null && playerHealth.IsDead);

        if (!uiOpen)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        HandleLook();
        HandleArms();
        HandleCrouch();
        HandleMovement();
        HandleStamina();
        HandleInteraction();
        HandleInventoryInput();
        HandlePointing();
        UpdateCrosshair();
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseDelta = mouse.delta.ReadValue();

        float effectiveSensitivity = sniperAiming && sniperWeapon != null
            ? mouseSensitivity * Mathf.Lerp(1f, 1f / sniperWeapon.CurrentZoomFactor, zoomSensitivityMultiplier)
            : mouseSensitivity;

        float mouseX = mouseDelta.x * effectiveSensitivity;
        float mouseY = mouseDelta.y * effectiveSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleArms()
    {
        float crouchRatio = Mathf.InverseLerp(standingHeight, crouchHeight, characterController.height);
        float crouchArmY = Mathf.Lerp(shoulderY, shoulderY * (crouchHeight / standingHeight), crouchRatio);

        float pitchRatio = pitch / 90f;
        float pitchArmY = crouchArmY - pitchRatio * 0.12f * shoulderPitchInfluence;
        float targetArmY = Mathf.Lerp(crouchArmY, pitchArmY, Mathf.Abs(pitchRatio));

        if (leftShoulder != null)
        {
            Vector3 euler = leftShoulderInitialEuler;
            euler.x += pitch * shoulderPitchInfluence;
            leftShoulder.localEulerAngles = euler;

            Vector3 pos = leftShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetArmY, Time.deltaTime * crouchSpeed);
            leftShoulder.localPosition = pos;
        }

        if (rightShoulder != null)
        {
            Vector3 euler = rightShoulderInitialEuler;
            euler.x += pitch * shoulderPitchInfluence;
            rightShoulder.localEulerAngles = euler;

            Vector3 pos = rightShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetArmY, Time.deltaTime * crouchSpeed);
            rightShoulder.localPosition = pos;
        }
    }

    private void HandleMovement()
    {
        Vector2 targetInput = ReadMoveInput();
        targetInput = Vector2.ClampMagnitude(targetInput, 1f);

        currentMoveInput = Vector2.SmoothDamp(
            currentMoveInput,
            targetInput,
            ref moveInputVelocity,
            moveSmoothTime
        );

        bool wantSprint = IsSprintPressed() && !isCrouching;
        bool canSprint = stamina == null || stamina.CanSprint;
        bool sprinting = wantSprint && canSprint;

        float baseSpeed = sprinting ? sprintSpeed : walkSpeed;
        float speed = baseSpeed;

        if (isCrouching)
            speed *= 0.55f;

        if (stamina != null)
            speed = Mathf.Max(speed * stamina.SpeedMultiplier, baseSpeed * 0.45f);

        Vector3 move = transform.right * currentMoveInput.x + transform.forward * currentMoveInput.y;
        move *= speed;

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = jumpStickToGroundForce;

            if (IsJumpPressed() && (stamina == null || !stamina.IsSlowed))
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

                if (stamina != null && (roleState == null || !roleState.IsSniper))
                    stamina.Consume(stamina.JumpCost);
            }
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        characterController.Move(move * Time.deltaTime);

        float targetAirborne = characterController.isGrounded ? 1f : jumpSquash;
        airborneSquash = Mathf.Lerp(airborneSquash, targetAirborne, Time.deltaTime * crouchSpeed);
    }

    private Vector2 ReadMoveInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;

        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            input.y -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            input.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            input.x -= 1f;

        return input;
    }

    private bool IsJumpPressed()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    private bool IsSprintPressed()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    private bool IsCrouchPressed()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed || keyboard.cKey.isPressed);
    }

    private void HandleCrouch()
    {
        bool wantCrouch = IsCrouchPressed();

        if (!wantCrouch && !HasHeadroom())
            wantCrouch = true;

        isCrouching = wantCrouch;

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float targetCameraY = isCrouching ? crouchCameraY : standingCameraY;

        characterController.height = Mathf.Lerp(
            characterController.height,
            targetHeight,
            Time.deltaTime * crouchSpeed
        );

        characterController.center = new Vector3(0f, characterController.height * 0.5f, 0f);

        if (cameraPivot != null)
        {
            Vector3 cameraLocalPos = cameraPivot.localPosition;
            cameraLocalPos.y = Mathf.Lerp(cameraLocalPos.y, targetCameraY, Time.deltaTime * crouchSpeed);
            cameraPivot.localPosition = cameraLocalPos;
        }

        if (headVisual != null && cameraPivot != null)
        {
            headVisual.localPosition = cameraPivot.localPosition;
            headVisual.localRotation = cameraPivot.localRotation;
        }

        float crouchRatio = Mathf.InverseLerp(standingHeight, crouchHeight, characterController.height);

        if (bodyVisual != null)
        {
            float baseScale = Mathf.Lerp(1f, 0.55f, crouchRatio);
            float finalScale = baseScale * airborneSquash;
            bodyVisual.localScale = Vector3.Lerp(
                bodyVisual.localScale,
                new Vector3(finalScale, finalScale, finalScale),
                Time.deltaTime * crouchSpeed
            );

            float bodyOffset = (1f - baseScale) * crouchBodyLowering;
            Vector3 bodyPos = bodyVisual.localPosition;
            bodyPos.y = Mathf.Lerp(bodyPos.y, bodyInitialY - bodyOffset, Time.deltaTime * crouchSpeed);
            bodyVisual.localPosition = bodyPos;
        }
    }

    private Collider[] headroomHits = new Collider[16];

    private bool HasHeadroom()
    {
        float radius = characterController != null ? characterController.radius * 0.9f : 0.3f;
        float currentTop = transform.position.y + characterController.height;
        float standingTop = transform.position.y + standingHeight;
        float bottom = currentTop + 0.05f;
        float top = standingTop - 0.05f;

        if (bottom >= top) return true;

        Vector3 start = new Vector3(transform.position.x, bottom, transform.position.z);
        Vector3 end = new Vector3(transform.position.x, top, transform.position.z);

        int count = Physics.OverlapCapsuleNonAlloc(start, end, radius, headroomHits, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (!headroomHits[i].transform.IsChildOf(transform))
                return false;
        }

        return true;
    }

    private void HandleStamina()
    {
        if (stamina == null) return;
        if (roleState != null && roleState.IsSniper) return;
        bool sprinting = IsSprintPressed() && !isCrouching && stamina.CanSprint;
        stamina.Tick(Time.deltaTime, sprinting);
    }

    private void HandleInteraction()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (currentInteractable == null)
        {
            if (reachingHand != null)
                ReleaseCurrentInteractable();

            if (WasInteractPressedThisFrame())
            {
                var found = FindBestInteractable();
                if (found != null)
                    TryStartInteraction(found);
            }
        }

        if (currentInteractable != null)
        {
            bool inputHeld = IsInteractPressed();
            bool inputReleased = WasInteractReleasedThisFrame();

            if (inputHeld)
            {
                float distanceToPlayer = Vector3.Distance(
                    GetInteractionOrigin().position,
                    currentInteractable.transform.position
                );

                if (distanceToPlayer > interactionRadius)
                {
                    StopInteraction();
                    return;
                }

                Hand hand = reachingHand;
                if (hand != null)
                {
                    hand.snapToTarget = false;
                    hand.SetTarget(currentInteractable.HandTarget);

                    float handDistance = hand.DistanceToTarget;
                    if (handDistance <= handActivationDistance)
                    {
                        if (!handReachedInteractable)
                        {
                            handReachedInteractable = true;
                            currentInteractable.OnHandBegin(this);
                        }

                        if (currentInteractable == null)
                            return;

                        if (currentInteractable.CancelOnCanInteractFail && !currentInteractable.CanInteract(this))
                        {
                            StopInteraction();
                            return;
                        }

                        currentInteractable.OnHandHold(this, Time.deltaTime);
                    }
                }
            }

            if (inputReleased)
                StopInteraction();
        }
    }

    private void TryStartInteraction(Interactable interactable)
    {
        if (interactable is PickableItem && IsInventoryFull())
            return;

        currentInteractable = interactable;
        handReachedInteractable = false;

        currentInteractable.OnLocalInteractionBegin(this);
        if (IsServer)
            currentInteractable.OnServerInteractionBegin(OwnerClientId);

        Hand hand = InteractionHandRef;
        reachingHand = hand;
        if (hand != null)
        {
            hand.snapToTarget = false;
            hand.targetFollowSmoothTimeOverride = interactionReachSmoothTime;
            hand.SetTarget(currentInteractable.HandTarget);
        }
    }

    private bool IsInventoryFull()
    {
        if (inventory == null) return true;

        for (int i = 0; i < inventory.MaxSlots; i++)
        {
            if (inventory.GetItemAtSlot(i) == null)
                return false;
        }

        return true;
    }

    private void StopInteraction()
    {
        if (currentInteractable != null && handReachedInteractable)
            currentInteractable.OnHandEnd(this);

        if (currentInteractable != null)
        {
            currentInteractable.OnLocalInteractionEnd(this);
            if (IsServer)
                currentInteractable.OnServerInteractionEnd(OwnerClientId);
        }

        currentInteractable = null;
        handReachedInteractable = false;

        Hand hand = reachingHand;
        if (hand != null)
            hand.ClearTarget();
        reachingHand = null;

        if (isTwoHandedHolding && currentTwoHanded != null)
        {
            if (leftHand != null)
            {
                leftHand.snapToTarget = true;
                leftHand.targetFollowSmoothTimeOverride = -1f;
                leftHand.SetTarget(currentTwoHanded.LeftGrip);
            }
        }
    }

    public void ReleaseCurrentInteractable()
    {
        if (currentInteractable != null && handReachedInteractable)
            currentInteractable.OnHandEnd(this);

        if (currentInteractable != null)
        {
            currentInteractable.OnLocalInteractionEnd(this);
            if (IsServer)
                currentInteractable.OnServerInteractionEnd(OwnerClientId);
        }

        currentInteractable = null;
        handReachedInteractable = false;

        Hand hand = reachingHand;
        if (hand != null)
            hand.ClearTarget();
        reachingHand = null;

        if (isTwoHandedHolding && currentTwoHanded != null)
        {
            if (leftHand != null)
            {
                leftHand.snapToTarget = true;
                leftHand.targetFollowSmoothTimeOverride = -1f;
                leftHand.SetTarget(currentTwoHanded.LeftGrip);
            }
        }
    }

    private void HandleInventoryInput()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            CancelCharge();
            return;
        }

        if (inventory == null) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame) { CancelCharge(); inventory.SwitchToSlot(0); }
        else if (keyboard.digit2Key.wasPressedThisFrame) { CancelCharge(); inventory.SwitchToSlot(1); }
        else if (keyboard.digit3Key.wasPressedThisFrame) { CancelCharge(); inventory.SwitchToSlot(2); }
        else if (keyboard.digit4Key.wasPressedThisFrame) { CancelCharge(); inventory.SwitchToSlot(3); }
        else if (keyboard.digit5Key.wasPressedThisFrame) { CancelCharge(); inventory.SwitchToSlot(4); }

        if (keyboard.qKey.wasPressedThisFrame && !isChargingThrow)
        {
            if (!CanStartThrowActiveItem())
            {
                CancelCharge();
                return;
            }

            isChargingThrow = true;
            throwChargeTime = 0f;

            if (chargeSlider != null)
                chargeSlider.Show(0f);
        }
        else if (keyboard.qKey.isPressed && isChargingThrow)
        {
            throwChargeTime += Time.deltaTime;
            float normalized = Mathf.Clamp01(throwChargeTime / maxChargeTime);

            if (chargeSlider != null)
                chargeSlider.UpdateValue(normalized);
        }
        else if (keyboard.qKey.wasReleasedThisFrame && isChargingThrow)
        {
            float normalized = Mathf.Clamp01(throwChargeTime / maxChargeTime);

            CancelCharge();

            if (!CanStartThrowActiveItem())
                return;

            Vector3 direction = cameraPivot != null
                ? cameraPivot.forward
                : playerCamera != null
                    ? playerCamera.transform.forward
                    : transform.forward;

            if (cachedSync == null)
                cachedSync = GetComponent<NetworkInventorySync>();

            if (cachedSync != null)
                cachedSync.LaunchActiveItem(normalized, direction);
        }
    }

    private bool CanStartThrowActiveItem()
    {
        if (inventory == null) return false;
        int slot = inventory.ActiveSlot;
        if (slot < 0) return false;
        return !string.IsNullOrEmpty(inventory.ActiveItemType);
    }

    private void CancelCharge()
    {
        if (!isChargingThrow) return;

        isChargingThrow = false;
        throwChargeTime = 0f;

        if (chargeSlider != null)
            chargeSlider.Hide();
    }

    private void HandlePointing()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.middleButton.wasPressedThisFrame)
        {
            if (currentInteractable != null)
                return;

            StartPointing();
        }
        else if (mouse.middleButton.wasReleasedThisFrame)
        {
            StopPointing();
        }
    }

    private void StartPointing()
    {
        if (isPointing || isTwoHandedHolding) return;
        isPointing = true;

        Hand pointing = PointingHandRef;
        if (pointing != null && pointTarget != null)
            pointing.SetTarget(pointTarget);

        Hand idle = IdleHandRef;
        if (idle != null && idleHandTarget != null)
        {
            bool idleIsLeft = idle == leftHand;
            idleHandTarget.localPosition = new Vector3(idleIsLeft ? -0.4f : 0.4f, 0.6f, 0.4f);
            idleHandTarget.localRotation = Quaternion.Euler(0f, 0f, idleIsLeft ? 0 : 0);
            idle.SetTarget(idleHandTarget);
        }
    }

    private void StopPointing()
    {
        if (!isPointing) return;
        isPointing = false;

        Hand pointing = PointingHandRef;
        if (pointing != null)
            pointing.ClearTarget();

        Hand idle = IdleHandRef;
        if (idle != null)
            idle.ClearTarget();
    }

    private Interactable FindBestInteractable()
    {
        Transform origin = GetInteractionOrigin();

        Collider[] hits = Physics.OverlapSphere(
            origin.position,
            interactionRadius,
            interactableMask,
            QueryTriggerInteraction.Collide
        );

        Interactable best = null;
        float bestScore = -999f;

        for (int i = 0; i < hits.Length; i++)
        {
            var interactable = hits[i].GetComponentInParent<Interactable>();
            if (interactable == null) continue;
            if (!interactable.CanInteract(this)) continue;
            if (interactable is PickableItem && IsInventoryFull()) continue;

            Vector3 toTarget = interactable.transform.position - origin.position;
            float distance = toTarget.magnitude;
            if (distance <= 0.01f) continue;

            float dot = Vector3.Dot(origin.forward, toTarget.normalized);
            if (dot < interactionViewDot) continue;

            float score = dot * 2f - distance * 0.25f;
            if (score > bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }

        return best;
    }

    private Transform GetInteractionOrigin()
    {
        if (playerCamera != null) return playerCamera.transform;
        if (cameraPivot != null) return cameraPivot;
        return transform;
    }

    private bool WasInteractPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.eKey.wasPressedThisFrame;
    }

    private bool IsInteractPressed()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.eKey.isPressed;
    }

    private bool WasInteractReleasedThisFrame()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.eKey.wasReleasedThisFrame;
    }

    private void UpdateCrosshair()
    {
        if (crosshair == null) return;
        if (sniperAiming) { crosshair.SetActive(false); return; }
        crosshair.SetActive(HasInteractableInSight());
    }

    private void HandleSniperAimChanged(bool aiming)
    {
        sniperAiming = aiming;
        if (crosshair != null)
            crosshair.SetActive(!aiming);
    }

    private bool HasInteractableInSight()
    {
        Transform origin = GetInteractionOrigin();

        Collider[] hits = Physics.OverlapSphere(
            origin.position,
            interactionRadius,
            interactableMask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hits.Length; i++)
        {
            var interactable = hits[i].GetComponentInParent<Interactable>();
            if (interactable == null || !interactable.CanInteract(this)) continue;
            if (interactable is PickableItem && IsInventoryFull()) continue;

            Vector3 toTarget = interactable.transform.position - origin.position;
            if (toTarget.magnitude <= 0.01f) continue;

            float dot = Vector3.Dot(origin.forward, toTarget.normalized);
            if (dot >= interactionViewDot)
                return true;
        }

        return false;
    }

    public void ApplyRemoteVisualState(bool remoteCrouching, float remotePitch, float remoteAirborneSquash = 1f)
    {
        if (IsOwner) return;

        isCrouching = remoteCrouching;
        pitch = remotePitch;
        airborneSquash = remoteAirborneSquash;

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float targetCameraY = isCrouching ? crouchCameraY : standingCameraY;

        characterController.height = Mathf.Lerp(
            characterController.height,
            targetHeight,
            Time.deltaTime * crouchSpeed
        );

        characterController.center = new Vector3(0f, characterController.height * 0.5f, 0f);

        if (cameraPivot != null)
        {
            Vector3 cameraLocalPos = cameraPivot.localPosition;
            cameraLocalPos.y = Mathf.Lerp(cameraLocalPos.y, targetCameraY, Time.deltaTime * crouchSpeed);
            cameraPivot.localPosition = cameraLocalPos;
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        if (headVisual != null && cameraPivot != null)
        {
            headVisual.localPosition = cameraPivot.localPosition;
            headVisual.localRotation = cameraPivot.localRotation;
        }

        ApplyRemoteArmsAndBody();
    }

    private void ApplyRemoteArmsAndBody()
    {
        if (characterController == null) return;

        float crouchRatio = Mathf.InverseLerp(standingHeight, crouchHeight, characterController.height);

        float crouchArmY = Mathf.Lerp(shoulderY, shoulderY * (crouchHeight / standingHeight), crouchRatio);

        float pitchRatio = pitch / 90f;
        float pitchArmY = crouchArmY - pitchRatio * 0.12f * shoulderPitchInfluence;
        float targetArmY = Mathf.Lerp(crouchArmY, pitchArmY, Mathf.Abs(pitchRatio));

        if (leftShoulder != null)
        {
            Vector3 euler = leftShoulderInitialEuler;
            euler.x += pitch * shoulderPitchInfluence;
            leftShoulder.localEulerAngles = euler;

            Vector3 pos = leftShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetArmY, Time.deltaTime * crouchSpeed);
            leftShoulder.localPosition = pos;
        }

        if (rightShoulder != null)
        {
            Vector3 euler = rightShoulderInitialEuler;
            euler.x += pitch * shoulderPitchInfluence;
            rightShoulder.localEulerAngles = euler;

            Vector3 pos = rightShoulder.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetArmY, Time.deltaTime * crouchSpeed);
            rightShoulder.localPosition = pos;
        }

        if (bodyVisual != null)
        {
            float baseScale = Mathf.Lerp(1f, 0.55f, crouchRatio);
            float finalScale = baseScale * airborneSquash;
            bodyVisual.localScale = Vector3.Lerp(
                bodyVisual.localScale,
                new Vector3(finalScale, finalScale, finalScale),
                Time.deltaTime * crouchSpeed
            );

            float bodyOffset = (1f - baseScale) * crouchBodyLowering;
            Vector3 bodyPos = bodyVisual.localPosition;
            bodyPos.y = Mathf.Lerp(bodyPos.y, bodyInitialY - bodyOffset, Time.deltaTime * crouchSpeed);
            bodyVisual.localPosition = bodyPos;
        }
    }

    private void SetLayerRecursively(Transform obj, int layer)
    {
        obj.gameObject.layer = layer;
        for (int i = 0; i < obj.childCount; i++)
            SetLayerRecursively(obj.GetChild(i), layer);
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = playerCamera != null ? playerCamera.transform : transform;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.position, interactionRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawRay(origin.position, origin.forward * interactionRadius);
    }
}
