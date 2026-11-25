using UnityEngine;

[DisallowMultipleComponent]
public class CameraController : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 4.5f, -7f);
    [SerializeField] private float followSpeed = 8f;
    private Transform target;

    private void Start()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            target = player.transform;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up);
    }
}
