using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerColor : NetworkBehaviour
{
    [Header("Renderers")]
    [SerializeField] private Renderer[] colorRenderers;

    [Header("Material")]
    [SerializeField] private string urpColorProperty = "_BaseColor";
    [SerializeField] private string standardColorProperty = "_Color";

    private readonly NetworkVariable<int> networkPackedColor = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public Color32 CurrentColor
    {
        get
        {
            if (networkPackedColor.Value != 0)
                return LocalPlayerSettings.UnpackColor(networkPackedColor.Value);
            return Color.white;
        }
    }

    private void Awake()
    {
        if (colorRenderers == null || colorRenderers.Length == 0)
            colorRenderers = GetComponentsInChildren<Renderer>(true);
    }

    public override void OnNetworkSpawn()
    {
        networkPackedColor.OnValueChanged += OnColorChanged;

        if (IsOwner)
        {
            int packedColor = LocalPlayerSettings.PackColor(LocalPlayerSettings.PlayerColor);

            if (IsServer)
                SetColorOnServer(packedColor, OwnerClientId);
            else
                RequestSetColorServerRpc(packedColor);
        }

        if (networkPackedColor.Value != 0)
            ApplyPackedColor(networkPackedColor.Value);
    }

    public override void OnNetworkDespawn()
    {
        networkPackedColor.OnValueChanged -= OnColorChanged;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetColorServerRpc(int requestedPackedColor, ServerRpcParams rpcParams = default)
    {
        SetColorOnServer(requestedPackedColor, rpcParams.Receive.SenderClientId);
    }

    private void SetColorOnServer(int requestedPackedColor, ulong senderClientId)
    {
        if (senderClientId != OwnerClientId)
            return;

        Color32 requestedColor = LocalPlayerSettings.UnpackColor(requestedPackedColor);
        requestedColor.a = 255;

        networkPackedColor.Value = LocalPlayerSettings.PackColor(requestedColor);
    }

    private void OnColorChanged(int oldValue, int newValue)
    {
        ApplyPackedColor(newValue);
    }

    private void ApplyPackedColor(int packedColor)
    {
        if (packedColor == 0)
            return;

        Color32 color = LocalPlayerSettings.UnpackColor(packedColor);
        ApplyColor(color);
    }

    private void ApplyColor(Color32 color)
    {
        if (colorRenderers == null)
            return;

        for (int i = 0; i < colorRenderers.Length; i++)
        {
            Renderer renderer = colorRenderers[i];
            if (renderer == null) continue;

            Material material = renderer.material;
            if (material == null) continue;

            if (material.HasProperty(urpColorProperty))
                material.SetColor(urpColorProperty, color);

            if (material.HasProperty(standardColorProperty))
                material.SetColor(standardColorProperty, color);
        }
    }
}
