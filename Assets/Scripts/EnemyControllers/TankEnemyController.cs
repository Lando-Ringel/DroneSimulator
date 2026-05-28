using System.Collections;
using UnityEngine;
using DroneSimulator.DroneControllers;
using DroneSimulator.AttackControllers;

namespace DroneSimulator.EnemyControllers
{
    [RequireComponent(typeof(Rigidbody))]
    public class TankEnemyController : EnemyController
    {
        [Header("References")]
        [SerializeField] private Transform m_Turret;
        [SerializeField] private Transform[] m_Wheels;
        [SerializeField] private MeshRenderer m_TankTrackRenderer;

        [Header("Movement")]
        [SerializeField] private float m_MoveSpeed = 6f;
        [SerializeField] private float m_RotationSpeed = 70f;
        [SerializeField] private float m_Acceleration = 4f;

        [Header("Terrain")]
        [SerializeField] private float m_GroundCheckDistance = 6f;
        [SerializeField] private float m_GroundAlignSpeed = 8f;
        [SerializeField] private LayerMask m_GroundMask;

        [Header("Obstacle Avoidance")]
        [SerializeField] private LayerMask m_ObstacleMask;
        [SerializeField] private float m_ObstacleDistance = 5f;
        [SerializeField] private float m_ObstacleRadius = 1.25f;
        [SerializeField] private float m_AvoidTurnSpeed = 120f;

        [Header("Wandering")]
        [SerializeField] private float m_MinMoveTime = 2f;
        [SerializeField] private float m_MaxMoveTime = 5f;
        [SerializeField] private float m_MinIdleTime = 1f;
        [SerializeField] private float m_MaxIdleTime = 3f;

        [Header("Evade")]
        [SerializeField] private float m_EvadeDistance = 18f;
        [SerializeField] private float m_EvadeSpeedMultiplier = 1.6f;

        [Header("Turret")]
        [SerializeField] private float m_TurretRotationSpeed = 120f;

        [Header("Animation")]
        [SerializeField] private float m_WheelSpinSpeed = 500f;
        [SerializeField] private float m_TrackScrollSpeed = 2f;

        private Rigidbody m_Rigidbody;
        private KamikazeDroneController m_PlayerDrone;

        private Material m_TrackMaterial;

        private bool m_IsMoving;
        private bool m_IsEvading;
        private bool m_IsDead;

        private Vector3 m_CurrentMoveDirection;
        private Vector3 m_GroundNormal = Vector3.up;
        private Vector3 m_SmoothedGroundNormal = Vector3.up;
        private float m_CurrentSpeed;
        private float m_TrackOffset;

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();

            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            // m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            m_PlayerDrone = FindAnyObjectByType<KamikazeDroneController>();

            if (m_TankTrackRenderer != null)
            {
                m_TrackMaterial = m_TankTrackRenderer.materials[0];
            }
        }

        private void Start()
        {
            StartCoroutine(WanderRoutine());
        }

        private void FixedUpdate()
        {
            if (m_IsDead || m_IsPaused)
                return;

            UpdateGrounding();

            HandleEvade();

            HandleObstacleAvoidance();

            MoveTank();

            // AlignToGround();

            AnimateWheels();

            AnimateTracks();

            RecordState();
        }

        private void Update()
        {
            if (m_IsDead || m_IsPaused)
                return;

            HandleTurretTracking();
        }

        private IEnumerator WanderRoutine()
        {
            while (true)
            {
                if (m_IsEvading)
                {
                    yield return null;
                    continue;
                }

                Vector2 randomCircle = Random.insideUnitCircle.normalized;

                Vector3 randomDirection = new Vector3(
                    randomCircle.x,
                    0f,
                    randomCircle.y
                );

                m_CurrentMoveDirection = randomDirection;

                m_IsMoving = true;

                float moveTime = Random.Range(
                    m_MinMoveTime,
                    m_MaxMoveTime
                );

                yield return new WaitForSeconds(moveTime);

                m_IsMoving = false;

                float idleTime = Random.Range(
                    m_MinIdleTime,
                    m_MaxIdleTime
                );

                yield return new WaitForSeconds(idleTime);
            }
        }

        private void UpdateGrounding()
        {
            Vector3 accumulatedNormal = Vector3.zero;
            int hitCount = 0;

            // Sample center + 4 corners for a stable average normal
            Vector3[] offsets = new Vector3[]
            {
                Vector3.zero,
                transform.forward * 0.8f,
                -transform.forward * 0.8f,
                transform.right * 0.8f,
                -transform.right * 0.8f
            };

            foreach (var offset in offsets)
            {
                Vector3 origin = transform.position + offset + Vector3.up * 2f;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, m_GroundCheckDistance, m_GroundMask))
                {
                    accumulatedNormal += hit.normal;
                    hitCount++;
                }
            }

            if (hitCount > 0)
                m_GroundNormal = (accumulatedNormal / hitCount).normalized;

            // Smooth the normal over time so sudden slope changes don't snap the tank
            m_SmoothedGroundNormal = Vector3.Slerp(
                m_SmoothedGroundNormal,
                m_GroundNormal,
                m_GroundAlignSpeed * Time.fixedDeltaTime
            );
        }
        private void AlignToGround()
        {
            Quaternion groundRotation =
                Quaternion.FromToRotation(
                    transform.up,
                    m_GroundNormal
                ) * transform.rotation;

            Quaternion smoothedRotation =
                Quaternion.Slerp(
                    m_Rigidbody.rotation,
                    groundRotation,
                    m_GroundAlignSpeed * Time.fixedDeltaTime
                );

            m_Rigidbody.MoveRotation(smoothedRotation);
        }

        private void HandleEvade()
        {
            if (m_PlayerDrone == null)
            {
                m_IsEvading = false;
                return;
            }

            Vector3 flatDronePosition =
                m_PlayerDrone.transform.position;

            flatDronePosition.y = transform.position.y;

            float distance =
                Vector3.Distance(
                    transform.position,
                    flatDronePosition
                );

            m_IsEvading = distance <= m_EvadeDistance;

            if (!m_IsEvading)
                return;

            Vector3 evadeDirection =
                (transform.position - flatDronePosition).normalized;

            m_CurrentMoveDirection = evadeDirection;

            m_IsMoving = true;
        }

        private void HandleObstacleAvoidance()
        {
            Vector3 origin =
                transform.position +
                Vector3.up * 1f;

            Vector3 direction =
                transform.forward;

            bool blocked = Physics.SphereCast(
                origin,
                m_ObstacleRadius,
                direction,
                out RaycastHit hit,
                m_ObstacleDistance,
                m_ObstacleMask
            );

            if (!blocked)
                return;

            Vector3 avoidDirection =
                Vector3.Cross(
                    hit.normal,
                    Vector3.up
                ).normalized;

            if (Vector3.Dot(
                avoidDirection,
                transform.right) < 0f)
            {
                avoidDirection *= -1f;
            }

            m_CurrentMoveDirection = Vector3.Lerp(
                m_CurrentMoveDirection,
                avoidDirection,
                Time.fixedDeltaTime * 4f
            );
        }

        private void MoveTank()
        {
            if (!m_IsMoving)
            {
                m_CurrentSpeed = Mathf.Lerp(m_CurrentSpeed, 0f, Time.fixedDeltaTime * m_Acceleration);

                // Still align to ground while idle — use smoothed normal, one write
                Quaternion idleGroundRotation = Quaternion.FromToRotation(transform.up, m_SmoothedGroundNormal) * transform.rotation;
                m_Rigidbody.MoveRotation(Quaternion.Slerp(m_Rigidbody.rotation, idleGroundRotation, m_GroundAlignSpeed * Time.fixedDeltaTime));
                return;
            }

            float targetSpeed = m_IsEvading ? m_MoveSpeed * m_EvadeSpeedMultiplier : m_MoveSpeed;
            m_CurrentSpeed = Mathf.Lerp(m_CurrentSpeed, targetSpeed, Time.fixedDeltaTime * m_Acceleration);

            Vector3 moveDirection = Vector3.ProjectOnPlane(m_CurrentMoveDirection, m_SmoothedGroundNormal).normalized;
            if (moveDirection == Vector3.zero) return;

            // Build target: face move direction AND conform to slope — one combined rotation
            Quaternion targetYaw = Quaternion.LookRotation(moveDirection, m_SmoothedGroundNormal);
            Quaternion rotated = Quaternion.RotateTowards(m_Rigidbody.rotation, targetYaw, m_RotationSpeed * Time.fixedDeltaTime);
            m_Rigidbody.MoveRotation(rotated); // single MoveRotation call this frame

            float angle = Quaternion.Angle(m_Rigidbody.rotation, targetYaw);

            // Soft speed falloff instead of hard cutoff at 45°
            float speedFactor = Mathf.InverseLerp(60f, 20f, angle); // ramps 0→1 between 60° and 20°
            Vector3 move = moveDirection * m_CurrentSpeed * speedFactor * Time.fixedDeltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + move);
        }
        private void HandleTurretTracking()
        {
            if (m_Turret == null)
                return;

            Quaternion targetRotation;

            if (m_PlayerDrone != null)
            {
                Vector3 directionToPlayer =
                    m_PlayerDrone.transform.position -
                    m_Turret.position;

                // Project the direction onto the tank's local plane so it stays parallel to the hull
                Vector3 targetDirection = Vector3.ProjectOnPlane(directionToPlayer, transform.up);

                if (targetDirection != Vector3.zero)
                {
                    // Explicitly use the tank's transform.up as the upward reference
                    targetRotation =
                        Quaternion.LookRotation(targetDirection, transform.up);

                    m_Turret.rotation =
                        Quaternion.RotateTowards(
                            m_Turret.rotation,
                            targetRotation,
                            m_TurretRotationSpeed * Time.deltaTime
                        );

                    return;
                }
            }

            // Fix the default forward rotation to also use the tank's local up
            targetRotation =
                Quaternion.LookRotation(
                    transform.forward,
                    transform.up
                );

            m_Turret.rotation =
                Quaternion.RotateTowards(
                    m_Turret.rotation,
                    targetRotation,
                    m_TurretRotationSpeed * 0.5f * Time.deltaTime
                );
        }
        private void AnimateWheels()
        {
            if (!m_IsMoving)
                return;

            if (m_Wheels == null)
                return;

            float spin =
                m_WheelSpinSpeed *
                Time.fixedDeltaTime;

            if (m_IsEvading)
            {
                spin *= m_EvadeSpeedMultiplier;
            }

            for (int i = 0; i < m_Wheels.Length; i++)
            {
                if (m_Wheels[i] == null)
                    continue;

                m_Wheels[i].Rotate(
                    Vector3.right,
                    spin,
                    Space.Self
                );
            }
        }

        private void AnimateTracks()
        {
            if (m_TrackMaterial == null)
                return;

            if (!m_IsMoving)
                return;

            float speed =
                m_TrackScrollSpeed;

            if (m_IsEvading)
            {
                speed *= m_EvadeSpeedMultiplier;
            }

            m_TrackOffset +=
                speed * Time.fixedDeltaTime;

            m_TrackOffset %= 1f;

            Vector2 offset =
                m_TrackMaterial.mainTextureOffset;

            offset.x = m_TrackOffset;

            m_TrackMaterial.mainTextureOffset =
                offset;
        }

        public override void TakeDamage(
            AttackController attackController,
            Vector3 attackPosition)
        {
            if (m_IsDead)
                return;

            RaiseHit();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;

            Vector3 origin =
                transform.position +
                Vector3.up * 1f;

            Gizmos.DrawWireSphere(
                origin +
                transform.forward *
                m_ObstacleDistance,
                m_ObstacleRadius
            );

            Gizmos.DrawRay(
                origin,
                transform.forward *
                m_ObstacleDistance
            );

            Gizmos.color = Color.yellow;

            Gizmos.DrawRay(
                transform.position,
                m_GroundNormal * 3f
            );
        }
    }
}