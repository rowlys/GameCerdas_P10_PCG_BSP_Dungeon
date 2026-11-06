using UnityEngine;
using UnityEngine.AI;

public class EnemyFSM : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack,
        Flee,
        Dead,

        Search, // Bagian Modul 87
        Alert,  // Bagian Modul 88
        Heal    // Bagian Modul 89
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private EnemyPerception perception;
    public EnemyHealth Health {get; private set;}

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float waypointTolerance = 0.5f;

    // Update Bagian Modul 88 Alert
    [Header("Alert")]
    [SerializeField] private float alertConfirmTime = 0.75f; 
    [SerializeField] private float alertGiveUpTime = 3f;     
    [SerializeField] private float alertTurnSpeed = 5f;      

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float lostPlayerDelay = 2f;

    // Update Bagian Modul 87 Search
    [Header("Search")]
    [SerializeField] private float searchSpeed = 3f;     
    [SerializeField] private float searchDuration = 8f; 
    [SerializeField] private float searchRadius = 4f;      
    [SerializeField] private float searchLookTime = 1.5f;  
    [SerializeField] private float searchTurnSpeed = 120f; 

    [Header("Attack")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackExitRange = 2.75f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;

    // Update Bagian Modul 89, field safeDistance dihapus karena Flee sekarang selalu berakhir di SafePoint (lalu Heal).
    [Header("Flee")]
    [SerializeField] private Transform safePoint;
    [SerializeField] private float fleeSpeed = 5f;
    [SerializeField] private float lowHealthThreshold = 30f;

    // Update Bagian Modul 89 Heal
    [Header("Heal")]
    [SerializeField] private float healRate = 10f; 
    [SerializeField, Range(0.5f, 1f)]
    private float healTargetPercent = 1f;          

    [Header("Debug")]
    [SerializeField] private EnemyState currentState;

    private int currentPatrolIndex = 0;

    private float lostPlayerTimer = 0f;
    private float nextAttackTime = 0f;

    private bool fleeTriggered = false;

    // Update Bagian Modul 87 Timer umum:
    private float stateTimer = 0f;

    // Update Bagian Modul 88 Alert timer.
    private float alertConfirmTimer = 0f;
    private float alertGiveUpTimer = 0f;

    // Update Bagian Modul 87 Search data pencarian.
    private float searchLookTimer = 0f;
    private Vector3 searchPoint;

    private PlayerHealth playerHealth;

    public EnemyState CurrentState => currentState;

    public bool IsAlive =>
        currentState != EnemyState.Dead;

    public bool IsInCombat =>
        currentState == EnemyState.Chase ||
        currentState == EnemyState.Search ||
        currentState == EnemyState.Attack ||
        currentState == EnemyState.Flee;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (perception == null)
            perception = GetComponent<EnemyPerception>();

        if (Health == null)
            Health = GetComponent<EnemyHealth>();

        if(player == null)
            player = GameObject.FindWithTag("Player").transform;

        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    private void Start()
    {
        currentState = EnemyState.Patrol;
        EnterState(currentState);
    }

    private void Update()
    {
        // ================================
        // GLOBAL TRANSITION PRIORITY
        // ================================

        if (Health.IsDead)
        {
            ChangeState(EnemyState.Dead);
            return;
        }

        // Update Bagian Modul 89 Tidak memicu Flee ketika sedang Heal.
        if (!fleeTriggered &&
            Health.CurrentHealth <= lowHealthThreshold &&
            currentState != EnemyState.Flee &&
            currentState != EnemyState.Heal)
        {
            fleeTriggered = true;
            ChangeState(EnemyState.Flee);
        }

        // Update Bagian Modul 87 Hitung lama berada di state sekarang.
        stateTimer += Time.deltaTime;

        // ================================
        // UPDATE CURRENT STATE
        // ================================

        switch (currentState)
        {
            case EnemyState.Patrol:
                UpdatePatrol();
                break;

            // Update Bagian Modul 88
            case EnemyState.Alert:
                UpdateAlert();
                break;

            case EnemyState.Chase:
                UpdateChase();
                break;

            // Update Bagian Modul 87
            case EnemyState.Search:
                UpdateSearch();
                break;

            case EnemyState.Attack:
                UpdateAttack();
                break;

            case EnemyState.Flee:
                UpdateFlee();
                break;

            // Update Bagian Modul 89
            case EnemyState.Heal:
                UpdateHeal();
                break;

            case EnemyState.Dead:
                UpdateDead();
                break;
        }
    }

    // =====================================
    // CHANGE STATE
    // =====================================

    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState)
            return;

        ExitState(currentState);

        currentState = newState;

        // Update Bagian Modul 87 Reset timer setiap ganti state.
        stateTimer = 0f;

        Debug.Log(
            gameObject.name +
            " → State: " +
            currentState
        );

        EnterState(currentState);
    }

    // =====================================
    // ENTER STATE
    // =====================================

    private void EnterState(EnemyState state)
    {
        switch (state)
        {
            case EnemyState.Patrol:

                agent.isStopped = false;
                agent.speed = patrolSpeed;

                SetPatrolDestination();

                break;

            // Update Bagian Modul 88 Alert berhenti dan bersiap memastikan stimulus.
            case EnemyState.Alert:

                agent.isStopped = true;
                agent.ResetPath();

                alertConfirmTimer = 0f;
                alertGiveUpTimer = 0f;

                break;

            case EnemyState.Chase:

                agent.isStopped = false;
                agent.speed = chaseSpeed;

                lostPlayerTimer = 0f;

                break;

            // Update Bagian Modul 87 Search menuju lastSeenPosition.
            case EnemyState.Search:

                agent.isStopped = false;
                agent.speed = searchSpeed;

                searchLookTimer = 0f;

                searchPoint = perception.LastKnownPosition;
                agent.SetDestination(searchPoint);

                break;

            case EnemyState.Attack:

                agent.isStopped = true;
                agent.ResetPath();

                break;

            case EnemyState.Flee:

                agent.isStopped = false;
                agent.speed = fleeSpeed;

                if (safePoint != null)
                {
                    agent.SetDestination(
                        safePoint.position
                    );
                }

                break;

            // Update Bagian Modul 89 Heal diam di SafePoint untuk memulihkan HP.
            case EnemyState.Heal:

                agent.isStopped = true;
                agent.ResetPath();

                break;

            case EnemyState.Dead:

                agent.isStopped = true;
                agent.ResetPath();

                break;
        }
    }

    // =====================================
    // EXIT STATE
    // =====================================

    private void ExitState(EnemyState state)
    {
        switch (state)
        {
            // Update Bagian Modul 88 & 89 Alert dan Heal juga menghentikan agent, jadi agent harus bergerak lagi saat keluar.
            case EnemyState.Alert:
            case EnemyState.Attack:
            case EnemyState.Heal:
                agent.isStopped = false;
                break;
        }
    }

    // =====================================
    // PATROL
    // =====================================

    private void UpdatePatrol()
    {
        // Update Bagian Modul 88 Alertmmmelihat ATAU mendengar sesuatu → Alert.
        if (perception.CanSeePlayer ||
            perception.CanHearPlayer)
        {
            ChangeState(EnemyState.Alert);
            return;
        }

        if (patrolPoints == null ||
            patrolPoints.Length == 0)
            return;

        if (!agent.pathPending &&
            agent.remainingDistance <=
            waypointTolerance)
        {
            currentPatrolIndex++;

            if (currentPatrolIndex >=
                patrolPoints.Length)
            {
                currentPatrolIndex = 0;
            }

            SetPatrolDestination();
        }
    }

    private void SetPatrolDestination()
    {
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
            return;

        Transform point =
            patrolPoints[currentPatrolIndex];

        if (point != null)
        {
            agent.SetDestination(
                point.position
            );
        }
    }


    private void UpdateAlert()
    {
        // Player terlihat → hitung waktu konfirmasi
        if (perception.CanSeePlayer)
        {
            alertGiveUpTimer = 0f;
            alertConfirmTimer += Time.deltaTime;

            FaceTowards(player.position, alertTurnSpeed);

            // Yakin jika cukup lama terlihat, atau sudah sangat dekat
            if (alertConfirmTimer >= alertConfirmTime ||
                perception.DistanceToPlayer <= attackRange)
            {
                ChangeState(EnemyState.Chase);
            }

            return;
        }

        // Tidak melihat player
        alertConfirmTimer = 0f;
        alertGiveUpTimer += Time.deltaTime;

        // Menoleh ke arah stimulus terakhir (memory)
        if (perception.HasLastKnownPosition)
        {
            FaceTowards(
                perception.LastKnownPosition,
                alertTurnSpeed
            );
        }

        // Terlalu lama tidak terkonfirmasi → kembali Patrol
        if (alertGiveUpTimer >= alertGiveUpTime)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    // =====================================
    // CHASE
    // =====================================

    private void UpdateChase()
    {
        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        if (perception.CanSeePlayer)
        {
            lostPlayerTimer = 0f;

            agent.SetDestination(
                player.position
            );

            if (distance <= attackRange)
            {
                ChangeState(
                    EnemyState.Attack
                );

                return;
            }
        }
        else
        {
            lostPlayerTimer +=
                Time.deltaTime;

            if (lostPlayerTimer >=
                lostPlayerDelay)
            {
                // Update Bagian Modul 87 Search Chase → Search.
                ChangeState(
                    EnemyState.Search
                );

                return;
            }
        }
    }


    private void UpdateSearch()
    {
        // 1. Player ditemukan
        if (perception.CanSeePlayer)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // 2. Menyerah
        if (stateTimer >= searchDuration)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        // 3. Mendengar player → menuju sumber suara
        if (perception.CanHearPlayer)
        {
            searchPoint = perception.LastKnownPosition;
            agent.SetDestination(searchPoint);
            searchLookTimer = 0f;
            return;
        }

        // 4. Masih berjalan menuju titik pencarian
        if (agent.pathPending ||
            agent.remainingDistance > waypointTolerance)
            return;

        // 5. Sudah sampai → melihat sekitar
        searchLookTimer += Time.deltaTime;

        transform.Rotate(
            0f,
            searchTurnSpeed * Time.deltaTime,
            0f
        );

        if (searchLookTimer >= searchLookTime)
        {
            searchLookTimer = 0f;
            PickRandomSearchPoint();
        }
    }

    // Update Bagian Modul 87 Pilih titik acak di sekitar lastSeenPosition.
    private void PickRandomSearchPoint()
    {
        Vector3 randomOffset =
            Random.insideUnitSphere * searchRadius;

        randomOffset.y = 0f;

        Vector3 candidate =
            perception.LastKnownPosition + randomOffset;

        // Pastikan titik berada di atas NavMesh
        // (bukan di dalam obstacle / di luar Ground)
        if (NavMesh.SamplePosition(
                candidate,
                out NavMeshHit hit,
                searchRadius,
                NavMesh.AllAreas))
        {
            searchPoint = hit.position;
            agent.SetDestination(searchPoint);
        }
    }

    // =====================================
    // ATTACK
    // =====================================

    private void UpdateAttack()
    {
        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        FacePlayer();

        if (!perception.CanSeePlayer ||
            distance > attackExitRange)
        {
            ChangeState(
                EnemyState.Chase
            );

            return;
        }

        if (Time.time >= nextAttackTime)
        {
            AttackPlayer();

            nextAttackTime =
                Time.time +
                attackCooldown;
        }
    }

    private void AttackPlayer()
    {
        Debug.Log("Enemy attacks Player!");

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(
                attackDamage
            );
        }
    }

    // Update Bagian Modul 88 Isi FacePlayer() dipindah ke FaceTowards() agar bisa dipakai ulang oleh Alert.
    private void FacePlayer()
    {
        FaceTowards(player.position, 10f);
    }

    // Update Bagian Modul 88 Versi umum FacePlayer() menghadap posisi apa pun (player atau memory).
    private void FaceTowards(Vector3 targetPosition, float turnSpeed)
    {
        Vector3 direction =
            targetPosition -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
    }

    // =====================================
    // FLEE
    // =====================================

    private void UpdateFlee()
    {
        if (safePoint == null)
            return;

        // Update Bagian Modul 89 Heal sampai SafePoint → Heal.
        if (!agent.pathPending &&
            agent.remainingDistance <= waypointTolerance)
        {
            ChangeState(EnemyState.Heal);
        }
    }


    private void UpdateHeal()
    {
        // Diganggu player saat memulihkan diri
        if (perception.CanSeePlayer)
        {
            // Jika HP sudah di atas threshold,
            // Enemy boleh kabur lagi nanti.
            if (Health.CurrentHealth > lowHealthThreshold)
                fleeTriggered = false;

            ChangeState(EnemyState.Chase);
            return;
        }

        // HP bertambah healRate per DETIK (bukan per frame)
        Health.Heal(healRate * Time.deltaTime);

        if (Health.CurrentHealth >=
            Health.MaxHealth * healTargetPercent)
        {
            // fleeTriggered di-reset ketika Health pulih,
            // agar Enemy bisa Flee lagi di pertarungan berikutnya.
            fleeTriggered = false;

            Debug.Log(
                gameObject.name +
                " pulih. HP: " +
                Health.CurrentHealth
            );

            ChangeState(EnemyState.Patrol);
        }
    }

    // =====================================
    // DEAD
    // =====================================

    private void UpdateDead()
    {
        // Tidak melakukan action.
    }

    // =====================================
    // DEBUG GIZMOS
    // =====================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        Gizmos.color = Color.magenta;

        Gizmos.DrawWireSphere(
            transform.position,
            attackExitRange
        );

        // Update Bagian Modul 87 Area pencarian Search(hanya saat Play & state Search).
        if (Application.isPlaying &&
            currentState == EnemyState.Search)
        {
            Gizmos.color = Color.cyan;

            Gizmos.DrawWireSphere(
                perception.LastKnownPosition,
                searchRadius
            );

            Gizmos.DrawLine(
                transform.position,
                searchPoint
            );
        }
    }
}
