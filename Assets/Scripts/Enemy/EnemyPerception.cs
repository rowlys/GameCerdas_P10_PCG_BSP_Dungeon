using UnityEngine;

public class EnemyPerception : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Vision")]
    [SerializeField] private float visionRange = 10f;
    [SerializeField, Range(0f, 360f)]
    private float visionAngle = 90f;

    [SerializeField] private float eyeHeight = 1f;

    // Update Bagian Modul 88 Alert pendengaran.
    [Header("Hearing")]
    [SerializeField] private float hearingRange = 4f;

    [Header("Layer")]
    [SerializeField] private LayerMask obstacleMask;

    public bool CanSeePlayer { get; private set; }

    // Update Bagian Modul 88 Alert hasil pendengaran.
    public bool CanHearPlayer { get; private set; }

    // Update Bagian Modul 87 MEMORY (lastSeenPosition)
    public Vector3 LastKnownPosition { get; private set; }

    // Apakah Enemy pernah mendeteksi player.
    public bool HasLastKnownPosition { get; private set; }

    private void Awake()
    {
        if(player == null)
            player = GameObject.FindWithTag("Player").transform;
    }

    public float DistanceToPlayer
    {
        get
        {
            if (player == null)
                return Mathf.Infinity;

            return Vector3.Distance(
                transform.position,
                player.position
            );
        }
    }

    private void Update()
    {
        CanSeePlayer = CheckPlayerVisibility();

        // Update Bagian Modul 88 Hearing hanya dianggap jika player tidak terlihat (vision lebih meyakinkan).
        CanHearPlayer =
            !CanSeePlayer &&
            CheckPlayerHearing();

        // Update Bagian Modul 87 Simpan memory hanya saat terdeteksi.
        if (CanSeePlayer || CanHearPlayer)
        {
            LastKnownPosition = player.position;
            HasLastKnownPosition = true;
        }
    }

    private bool CheckPlayerVisibility()
    {
        if (player == null)
            return false;

        Vector3 origin =
            transform.position +
            Vector3.up * eyeHeight;

        Vector3 target =
            player.position +
            Vector3.up * 0.8f;

        Vector3 direction =
            target - origin;

        float distance = direction.magnitude;

        // 1. Periksa jarak
        if (distance > visionRange)
            return false;

        // 2. Periksa sudut pandang
        float angle = Vector3.Angle(
            transform.forward,
            direction
        );

        if (angle > visionAngle * 0.5f)
            return false;

        // 3. Periksa obstacle
        bool blocked = Physics.Raycast(
            origin,
            direction.normalized,
            distance,
            obstacleMask
        );

        if (blocked)
            return false;

        return true;
    }

    // Update Bagian Modul 88 Alert pendengaran cukup cek jarak.
    private bool CheckPlayerHearing()
    {
        if (player == null)
            return false;

        return DistanceToPlayer <= hearingRange;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            visionRange
        );

        Vector3 leftDirection =
            Quaternion.Euler(
                0f,
                -visionAngle * 0.5f,
                0f
            ) * transform.forward;

        Vector3 rightDirection =
            Quaternion.Euler(
                0f,
                visionAngle * 0.5f,
                0f
            ) * transform.forward;

        Gizmos.DrawRay(
            transform.position,
            leftDirection * visionRange
        );

        Gizmos.DrawRay(
            transform.position,
            rightDirection * visionRange
        );

        // Update Bagian Modul 88 Hearing Range (cyan)
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            hearingRange
        );

        // Update Bagian Modul 87 Memory / lastSeenPosition (oranye)
        if (HasLastKnownPosition)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);

            Gizmos.DrawSphere(
                LastKnownPosition,
                0.3f
            );
        }
    }
}
