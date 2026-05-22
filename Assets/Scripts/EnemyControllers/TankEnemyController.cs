using System.Collections;
using UnityEngine;
using DroneSimulator.DroneControllers;
using DroneSimulator.AttackControllers;
using System.Collections.Generic;
using DroneSimulator.SceneManagers;

namespace DroneSimulator.EnemyControllers
{
    public class TankEnemyController : EnemyController
    {
        [Header("References")]
        [SerializeField] private Transform[] m_Wheels;
        [SerializeField] private MeshRenderer m_TankTrackRenderer;
        [SerializeField] private Transform m_Turret;

        [Header("Stats")]
        [SerializeField] private float m_Health = 100f;

        [Header("Movement")]
        [SerializeField] private float m_MoveSpeed = 3f;
        [SerializeField] private float m_RotationSpeed = 60f; // Lowered slightly for realistic heavy tank mass
        [SerializeField] private float m_MinMoveTime = 2f;
        [SerializeField] private float m_MaxMoveTime = 5f;
        [SerializeField] private float m_MinIdleTime = 1f;
        [SerializeField] private float m_MaxIdleTime = 3f;

        [Header("Obstacle Avoidance")]
        [SerializeField] private LayerMask m_ObstacleLayer;      // Set this to your Obstacle layer
        [SerializeField] private float m_DetectionDistance = 5f; // How far ahead to look
        [SerializeField] private float m_AvoidanceForce = 90f;   // Turning speed modifier when avoiding
        [SerializeField] private float m_TankWidth = 2.5f;       // Approximate width of the tank for side checks        
        [SerializeField] private float m_CurrentAvoidDirection = 0f;
        [Header("Evade")]
        [SerializeField] private float m_EvadeDistance = 15f;
        [SerializeField] private float m_EvadeSpeedMultiplier = 1.75f;

        [SerializeField] private KamikazeDroneController m_PlayerDrone;
        private bool m_IsEvading; 

        [Header("Turret Controls")]
        [SerializeField] private float m_TurretLookSpeed = 120f; // Degrees per second for turret rotation

        [Header("Wheel Animation")]
        [SerializeField] private float m_WheelSpinSpeed = 360f;
        [SerializeField] private float m_TrackScrollSpeed = 1.5f;

        [Header("FX")]
        [SerializeField] private GameObject m_fireParticlePrefab;
        [SerializeField] private GameObject m_smokeParticlePrefab;
        private List<GameObject> m_ActiveSmokeParticles = new List<GameObject>();
        [SerializeField] private Transform m_ParticleHoldersTransform;

        private Material m_TrackMaterial;
        private bool m_IsMoving;
        private float m_TrackOffset;
        private Vector3 m_CurrentEvadeDirection;

        private void Awake()
        {
            m_PlayerDrone = FindAnyObjectByType<KamikazeDroneController>();
            m_TrackMaterial = m_TankTrackRenderer.materials[0];
        }

        private void Start()
        {
            StartCoroutine(RandomWalkRoutine());
        }

        private void Update()
        {
            if(m_IsDead || m_IsPaused)
                return;

            HandleEvade();
            HandleTurretTracking();

            if (!m_IsEvading && m_IsMoving)
            {
                if (CheckAndAvoidObstacles(out Quaternion avoidRot))
                {
                    // Smoothly override current rotation to steer away from the obstacle
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        avoidRot,
                        m_AvoidanceForce * Time.deltaTime
                    );
                }
            }            

            if (m_IsMoving)
            {
                Move();
                AnimateWheels();
                AnimateTracks();
            }
            RecordState();
        }
        public override Queue<ReplayableState> SetUpReplayModel()
        {
            base.SetUpReplayModel();
            ReplayTankController replayTankController = m_ReplayModelObject.GetComponent<ReplayTankController>();
            for(int i = 0; i < m_ParticleHoldersTransform.childCount; i++)
            {
                foreach(Transform child in m_ParticleHoldersTransform.GetChild(i))
                {
                    GameObject particlObj = Instantiate(child.gameObject);
                    particlObj.transform.parent = replayTankController.m_ParticleHoldersTransform.GetChild(i);
                    ParticleSystem ps = particlObj.GetComponent<ParticleSystem>();
                    ParticleSystem.MainModule main = ps.main;
                    main.prewarm = true;
                }
            }
            return new Queue<ReplayableState>(m_ReplayBuffer);        
        }
        public override void RemoveReplayModel()
        {
            base.RemoveReplayModel();
            ReplayTankController replayTankController = m_ReplayModelObject.GetComponent<ReplayTankController>();
            for(int i = 0; i < replayTankController.m_ParticleHoldersTransform.childCount; i++)
            {
                foreach(Transform child in replayTankController.m_ParticleHoldersTransform.GetChild(i))
                {
                    Destroy(child.gameObject);
                }
            }
        }


        private void HandleEvade()
        {
            if (m_PlayerDrone == null)
                return;

            Vector3 flatPlayerPos = m_PlayerDrone.transform.position;
            flatPlayerPos.y = transform.position.y;

            float distance = Vector3.Distance(transform.position, flatPlayerPos);
            m_IsEvading = distance <= m_EvadeDistance;

            if (!m_IsEvading)
                return;

            // Calculate direction directly away from the player drone
            m_CurrentEvadeDirection = (transform.position - flatPlayerPos).normalized;

            if (m_CurrentEvadeDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(m_CurrentEvadeDirection);
                
                // Smoothly rotate the hull toward the escape direction
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    m_RotationSpeed * m_EvadeSpeedMultiplier * Time.deltaTime
                );

                // REALISM FIX: Only allow forward driving if the tank is mostly facing its escape trajectory.
                // This prevents the tank from awkwardly sliding sideways/drifting while turning around.
                float angleToEscape = Quaternion.Angle(transform.rotation, targetRotation);
                m_IsMoving = angleToEscape < 45f; 
            }
        }

        private void HandleTurretTracking()
        {
            // If the turret is missing or there is no target drone, let it smoothly return to center
            if (m_Turret == null) return;

            Quaternion targetTurretRotation;

            if (m_PlayerDrone != null && Vector3.Distance(transform.position, m_PlayerDrone.transform.position) <= m_EvadeDistance * 1.5f)
            {
                // Track the player drone dynamically on a flat 2D plain (locks turret pitch pitch)
                Vector3 targetDir = m_PlayerDrone.transform.position - m_Turret.position;
                targetDir.y = 0f; 

                if (targetDir != Vector3.zero)
                {
                    targetTurretRotation = Quaternion.LookRotation(targetDir);
                    m_Turret.rotation = Quaternion.RotateTowards(
                        m_Turret.rotation, 
                        targetTurretRotation, 
                        m_TurretLookSpeed * Time.deltaTime
                    );
                    return;
                }
            }

            // Return to facing forward relative to the chassis if no targets are nearby
            targetTurretRotation = Quaternion.LookRotation(transform.forward);
            m_Turret.rotation = Quaternion.RotateTowards(
                m_Turret.rotation, 
                targetTurretRotation, 
                m_TurretLookSpeed * 0.5f * Time.deltaTime
            );
        }

        private IEnumerator RandomWalkRoutine()
        {
            while (true)
            {
                if (m_IsEvading)
                {
                    yield return null;
                    continue;
                }

                float randomYRotation = Random.Range(0f, 360f);
                Quaternion targetRotation = Quaternion.Euler(0f, randomYRotation, 0f);

                // Pivot in place to face target direction before driving
                m_IsMoving = false; 
                // Inside your RandomWalkRoutine(), modify the rotation loop:
                while (Quaternion.Angle(transform.rotation, targetRotation) > 1f && !m_IsEvading)
                {
                    // NEW: If an obstacle is detected, break out of the random walk turn sequence 
                    // and let the Update loop's avoidance mechanism steer the vehicle instead.
                    if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, m_DetectionDistance, m_ObstacleLayer))
                    {
                        m_IsMoving = true; // Force movement to let the avoidance system drive around it
                        break;
                    }

                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        m_RotationSpeed * Time.deltaTime
                    );

                    yield return null;
                }
                if (!m_IsEvading)
                {
                    m_IsMoving = true;
                }

                float moveDuration = Random.Range(m_MinMoveTime, m_MaxMoveTime);
                float moveTimer = 0f;
                
                while (moveTimer < moveDuration && !m_IsEvading)
                {
                    moveTimer += Time.deltaTime;
                    yield return null;
                }

                if (!m_IsEvading)
                {
                    m_IsMoving = false;
                }

                float idleDuration = Random.Range(m_MinIdleTime, m_MaxIdleTime);
                float idleTimer = 0f;
                
                while (idleTimer < idleDuration && !m_IsEvading)
                {
                    idleTimer += Time.deltaTime;
                    yield return null;
                }
            }
        }

        private bool CheckAndAvoidObstacles(out Quaternion avoidanceRotation)
        {
            avoidanceRotation = transform.rotation;

            Vector3 centerOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 leftOrigin = centerOrigin - (transform.right * (m_TankWidth * 0.5f));
            Vector3 rightOrigin = centerOrigin + (transform.right * (m_TankWidth * 0.5f));

            bool centerHit = Physics.Raycast(
                centerOrigin,
                transform.forward,
                m_DetectionDistance,
                m_ObstacleLayer
            );

            bool leftHit = Physics.Raycast(
                leftOrigin,
                Quaternion.Euler(0, -20, 0) * transform.forward,
                m_DetectionDistance * 0.8f,
                m_ObstacleLayer
            );

            bool rightHit = Physics.Raycast(
                rightOrigin,
                Quaternion.Euler(0, 20, 0) * transform.forward,
                m_DetectionDistance * 0.8f,
                m_ObstacleLayer
            );

            if (!centerHit && !leftHit && !rightHit)
            {
                m_CurrentAvoidDirection = 0f;
                return false;
            }

            // Choose a persistent turn direction
            if (m_CurrentAvoidDirection == 0f)
            {
                if (leftHit && !rightHit)
                {
                    m_CurrentAvoidDirection = 1f;
                }
                else if (rightHit && !leftHit)
                {
                    m_CurrentAvoidDirection = -1f;
                }
                else
                {
                    // Both blocked or center blocked
                    m_CurrentAvoidDirection = Random.value > 0.5f ? 1f : -1f;
                }
            }

            Vector3 avoidDir =
                Quaternion.Euler(0, 90f * m_CurrentAvoidDirection, 0)
                * transform.forward;

            avoidDir.y = 0f;

            avoidanceRotation = Quaternion.LookRotation(avoidDir.normalized);

            return true;
        }

        private void Move()
        {
            float speed = m_MoveSpeed;

            if (m_IsEvading)
                speed *= m_EvadeSpeedMultiplier;

            Vector3 moveDir = transform.forward;

            // STOP movement if obstacle directly ahead
            if (Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                moveDir,
                m_DetectionDistance * 0.5f,
                m_ObstacleLayer))
            {
                return;
            }

            transform.position += moveDir * speed * Time.deltaTime;
        }
        private void AnimateWheels()
        {
            if (m_Wheels == null) return;

            float spinAmount = m_WheelSpinSpeed * Time.deltaTime;
            if (m_IsEvading) spinAmount *= m_EvadeSpeedMultiplier;

            for (int i = 0; i < m_Wheels.Length; i++)
            {
                if (m_Wheels[i] == null) continue;
                m_Wheels[i].Rotate(Vector3.right, spinAmount, Space.Self);
            }
        }

        private void AnimateTracks()
        {
            if (m_TrackMaterial == null) return;

            float scrollSpeed = m_TrackScrollSpeed;
            if (m_IsEvading) scrollSpeed *= m_EvadeSpeedMultiplier;

            m_TrackOffset += scrollSpeed * Time.deltaTime;
            m_TrackOffset %= 1f;

            Vector2 offset = m_TrackMaterial.mainTextureOffset;
            offset.x = m_TrackOffset;
            m_TrackMaterial.mainTextureOffset = offset;
        }

        public override void TakeDamage(AttackController attackController, Vector3 attackPosition)
        {
            if (m_IsDead)
                return;
            m_Health -= attackController.AttackDamage;
            Vector3 hitPosition  = attackController.transform.position;
            Transform closestHolder = GetClosestParticleHolder(hitPosition);
            Vector3 spawnPosition = closestHolder != null ? closestHolder.position : hitPosition;

            if (m_Health <= 0f)
            {
                m_IsDead = true;
                m_IsMoving = false;
                m_IsEvading = false;
                StopAllCoroutines();
                Instantiate(
                    m_fireParticlePrefab,
                    spawnPosition,
                    Quaternion.identity,
                    closestHolder
                );
                RaiseKilled();
            }
            else
            {
                GameObject smokeParticleObj = Instantiate(
                    m_smokeParticlePrefab,
                    spawnPosition,
                    Quaternion.identity,
                    closestHolder
                );
                m_ActiveSmokeParticles.Add(smokeParticleObj);
                RaiseHit();
            }
        }

        private Transform GetClosestParticleHolder(Vector3 hitPosition)
        {
            Transform closestHolder = null;
            float closestDistance = Mathf.Infinity;
            for (int i = 0; i < m_ParticleHoldersTransform.childCount; i++)
            {
                Transform holder = m_ParticleHoldersTransform.GetChild(i);
                if (holder == null)
                    continue;
                float distance = Vector3.Distance(hitPosition, holder.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHolder = holder;
                }
            }
            return closestHolder;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 3f);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_EvadeDistance);

            // ---- Visual Avoidance Debug Whiskers ----
            Gizmos.color = Color.cyan;
            Vector3 centerOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 leftOrigin = centerOrigin - (transform.right * (m_TankWidth * 0.5f));
            Vector3 rightOrigin = centerOrigin + (transform.right * (m_TankWidth * 0.5f));

            Gizmos.DrawRay(centerOrigin, transform.forward * m_DetectionDistance);
            Gizmos.DrawRay(leftOrigin, (Quaternion.Euler(0, -15, 0) * transform.forward) * (m_DetectionDistance * 0.8f));
            Gizmos.DrawRay(rightOrigin, (Quaternion.Euler(0, 15, 0) * transform.forward) * (m_DetectionDistance * 0.8f));
        }
    }
}