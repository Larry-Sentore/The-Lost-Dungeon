using UnityEngine;

// Third-person follow camera. Stays behind the player and pulls in if a wall would block the view.
[DisallowMultipleComponent]
public class CameraController : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 4.5f, -7f);
    [SerializeField] private float followSpeed = 8f;
    [SerializeField] private float wallPadding = 0.3f;
    [Tooltip("Pull the camera in front of walls that block the view of the player.")]
    [SerializeField] private bool avoidWalls;
    private Transform target;

    // Finds the player so the camera knows who to follow.
    private void Start()
    {
        FindTarget();
    }

    // Looks for the player in the scene.
    private void FindTarget()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            target = player.transform;
        }
    }

    // Smoothly moves the camera behind the player (optionally pulling in if a wall is in the way).
    private void LateUpdate()
    {
        if (target == null)
        {
            FindTarget();
            if (target == null)
            {
                return;
            }
        }

        Vector3 focus = target.position + Vector3.up * 1.2f;
        Vector3 desired = target.position + offset;
        Vector3 toCamera = desired - focus;
        if (avoidWalls && Physics.SphereCast(focus, 0.25f, toCamera.normalized, out RaycastHit hit, toCamera.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            !hit.collider.transform.IsChildOf(target) && hit.collider.GetComponentInParent<EnemyController>() == null)
        {
            desired = focus + toCamera.normalized * Mathf.Max(1.5f, hit.distance - wallPadding);
        }

        // If the player teleported (new level), jump straight there instead of sliding across the map.
        if ((transform.position - desired).sqrMagnitude > 400f)
        {
            transform.position = desired;
        }
        transform.position = Vector3.Lerp(transform.position, desired, Mathf.Clamp01(followSpeed * Time.unscaledDeltaTime));
        transform.LookAt(focus);
    }
}
