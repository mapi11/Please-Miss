using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCanvasController : MonoBehaviour
{
    [SerializeField] private GameObject canvasObject;

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Evaluate();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Evaluate();
    }

    private void Evaluate()
    {
        if (FindObjectOfType<LobbyManager>() != null && LobbyManager.IsInLobby)
            canvasObject.SetActive(false);
        else
            canvasObject.SetActive(true);
    }
}
