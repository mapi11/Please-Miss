using UnityEngine;

public class LocationPrefab : MonoBehaviour
{
    [System.Serializable]
    public class Attachment
    {
        public Transform slot;
        [Range(0f, 100f)] public float spawnChance = 100f;
        public GameObject[] prefabs;
    }

    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Attachment[] attachments;

    public Transform StartPoint => startPoint;
    public Transform EndPoint => endPoint;
    public Attachment[] Attachments => attachments;
}
