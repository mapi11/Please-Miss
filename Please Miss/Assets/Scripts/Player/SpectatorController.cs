using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpectatorController : NetworkBehaviour
{
    [Header("Orbit")]
    [SerializeField] private float orbitSpeed = 5f;
    [SerializeField] private float verticalSpeed = 3f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private float distance = 5f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 10f;

    [Header("Smoothing")]
    [SerializeField] private float smoothTime = 0.2f;

    [Header("References")]
    [SerializeField] private Camera spectatorCamera;
    [SerializeField] private GameObject spectatorUI;
    [SerializeField] private Transform headModel;

    [Header("Multiplayer")]
    [SerializeField] private int localOnlyLayer = 6;
    [SerializeField] private int spectatorHeadLayer = 9;

    private readonly List<NetworkObject> validTargets = new List<NetworkObject>();
    private int currentTargetIndex = -1;
    private bool isSpectating;
    private float yaw;
    private float pitch;
    private Vector3 smoothPositionVelocity;

    public bool IsSpectating => isSpectating;

    public void SetReferences(Camera cam, GameObject ui)
    {
        spectatorCamera = cam;
        spectatorUI = ui;
    }

    private void Start()
    {
        if (spectatorCamera == null)
            spectatorCamera = GetComponentInChildren<Camera>();

        if (spectatorUI != null)
            spectatorUI.SetActive(IsSpawned && IsOwner);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            if (spectatorCamera != null)
                spectatorCamera.enabled = false;

            if (spectatorUI != null)
                spectatorUI.SetActive(false);
            return;
        }

        DisablePlayerCamera();
        HideHeadForOwner();

        isSpectating = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = 0f;
        pitch = 0f;

        RefreshTargets();
        currentTargetIndex = validTargets.Count > 0 ? 0 : -1;

        if (spectatorUI != null)
        {
            spectatorUI.SetActive(true);

            var spectatorUiComp = spectatorUI.GetComponentInChildren<SpectatorUI>(true);
            if (spectatorUiComp != null)
                spectatorUiComp.Initialize(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (!IsOwner) return;

        isSpectating = false;
        EnablePlayerCamera();

        var spectatorManager = GetLocalSpectatorManager();
        if (spectatorManager != null)
            spectatorManager.ExitSpectatorMode();
    }

    private void Update()
    {
        if (!isSpectating || !IsOwner) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
            CycleTarget(1);
        else if (mouse.rightButton.wasPressedThisFrame)
            CycleTarget(-1);

        HandleZoom(mouse);
        UpdateOrbit(mouse);
    }

    public string GetCurrentTargetName()
    {
        if (currentTargetIndex < 0 || currentTargetIndex >= validTargets.Count)
            return "None";

        NetworkObject target = validTargets[currentTargetIndex];
        if (target == null) return "None";

        var nameComp = target.GetComponent<NetworkPlayerName>();
        return nameComp != null ? nameComp.CurrentName : $"Player {target.NetworkObjectId}";
    }

    private void DisablePlayerCamera()
    {
        var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (playerObj == null) return;

        var cam = playerObj.GetComponentInChildren<Camera>();
        if (cam != null && cam != spectatorCamera)
        {
            cam.enabled = false;

            var listener = cam.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
        }
    }

    private void EnablePlayerCamera()
    {
        var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (playerObj == null) return;

        var cams = playerObj.GetComponentsInChildren<Camera>(true);
        foreach (var cam in cams)
        {
            if (cam != spectatorCamera)
            {
                cam.enabled = true;

                var listener = cam.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = true;
            }
        }
    }

    private static SpectatorManager GetLocalSpectatorManager()
    {
        if (NetworkManager.Singleton?.LocalClient?.PlayerObject == null) return null;
        return NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<SpectatorManager>();
    }

    private void HideHeadForOwner()
    {
        if (headModel != null)
            SetLayerRecursively(headModel, spectatorHeadLayer);

        if (spectatorCamera != null)
            spectatorCamera.cullingMask &= ~(1 << spectatorHeadLayer);
    }

    private void CycleTarget(int direction)
    {
        RefreshTargets();
        if (validTargets.Count == 0)
        {
            currentTargetIndex = -1;
            return;
        }

        currentTargetIndex = (currentTargetIndex + direction + validTargets.Count) % validTargets.Count;
    }

    private void HandleZoom(Mouse mouse)
    {
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
    }

    private void UpdateOrbit(Mouse mouse)
    {
        if (validTargets.Count == 0 || currentTargetIndex < 0) return;

        NetworkObject target = validTargets[currentTargetIndex];
        if (target == null) return;

        Vector3 targetPos = target.transform.position + Vector3.up * 1.5f;
        Vector2 mouseDelta = mouse.delta.ReadValue();

        float sens = PlayerPrefs.GetFloat("MouseSensitivity", 0.08f);

        yaw += mouseDelta.x * orbitSpeed * sens;
        pitch = Mathf.Clamp(pitch - mouseDelta.y * verticalSpeed * sens, minPitch, maxPitch);

        Vector3 desiredPos = targetPos + Quaternion.Euler(pitch, yaw, 0f) * (Vector3.back * distance);
        Quaternion desiredRot = Quaternion.LookRotation(targetPos - desiredPos);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref smoothPositionVelocity, smoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime / smoothTime);
    }

    private void RefreshTargets()
    {
        validTargets.Clear();

        if (NetworkManager.Singleton == null) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null || client.PlayerObject == NetworkManager.Singleton.LocalClient?.PlayerObject)
                continue;

            var role = client.PlayerObject.GetComponent<NetworkPlayerRole>();
            if (role == null || !role.IsRunner) continue;

            var health = client.PlayerObject.GetComponent<PlayerHealth>();
            if (health == null || health.IsDead) continue;

            validTargets.Add(client.PlayerObject);
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }
}
