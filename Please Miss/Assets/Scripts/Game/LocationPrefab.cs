using UnityEngine;

public class LocationPrefab : MonoBehaviour
{
    [System.Serializable]
    public class Attachment
    {
        public Transform slot;
        public GameObject prefab;
    }

    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Attachment[] attachments;

    public Transform StartPoint => startPoint;
    public Transform EndPoint => endPoint;
    public Attachment[] Attachments => attachments;
}
