using System.Collections;
using UnityEngine;
using DroneSimulator.DroneControllers;
using DroneSimulator.AttackControllers;
using System.Collections.Generic;

namespace DroneSimulator.EnemyControllers
{
    public class TankEnemyController : EnemyController
    {
        [Header("References")]
        [SerializeField] private Transform[] m_Wheels;
        [SerializeField] private MeshRenderer m_TankTrackRenderer;

        [Header("Stats")]
        [SerializeField] private float m_Health = 100f;
        private bool m_IsDead;

        [Header("Movement")]
        [SerializeField] private float m_MoveSpeed = 3f;
        [SerializeField] private float m_RotationSpeed = 90f;
        [SerializeField] private float m_MinMoveTime = 2f;
        [SerializeField] private float m_MaxMoveTime = 5f;
        [SerializeField] private float m_MinIdleTime = 1f;
        [SerializeField] private float m_MaxIdleTime = 3f;
        
        [Header("Evade")]
        [SerializeField] private float m_EvadeDistance = 15f;
        [SerializeField] private float m_EvadeSpeedMultiplier = 1.75f;

        [SerializeField] private KamikazeDroneController m_PlayerDrone;
        private bool m_IsEvading; 

        [Header("Wheel Animation")]
        [SerializeField] private float m_WheelSpinSpeed = 360f;
        [SerializeField] private float m_TrackScrollSpeed = 1.5f;

        [Header("FX")]
        [SerializeField] private GameObject m_fireParticlePrefab;
        [SerializeField] private GameObject m_smokeParticlePrefab;
        [SerializeField] private Vector3 m_FireParticleOffset = new Vector3(0f, -1.75f, 0f);
        private List<GameObject> m_ActiveSmokeParticles = new List<GameObject>();
        private GameObject m_ActiveFireParticle;
        [SerializeField] private Transform m_ParticleHoldersTransform;
        private Transform[] m_ParticleHolders;


        private Material m_TrackMaterial;
        private bool m_IsMoving;
        private float m_TrackOffset;

        private void Awake()
        {
            m_PlayerDrone = FindAnyObjectByType<KamikazeDroneController>();
            m_TrackMaterial = m_TankTrackRenderer.materials[0];
            m_ParticleHolders = new Transform[m_ParticleHoldersTransform.childCount];
            for(int i = 0; i < m_ParticleHoldersTransform.childCount; i++)
            {
                m_ParticleHolders[i] = m_ParticleHoldersTransform.GetChild(i);
            }
        }

        private void Start()
        {
            StartCoroutine(RandomWalkRoutine());
        }

        private void Update()
        {
            if(m_IsDead)
                return;

            HandleEvade();

            if (m_IsMoving)
            {
                Move();
                AnimateWheels();
                AnimateTracks();
            }
        }

        private void HandleEvade()
        {
            if (m_PlayerDrone == null)
                return;

            Vector3 flatPlayerPos = m_PlayerDrone.transform.position;
            flatPlayerPos.y = transform.position.y;

            float distance = Vector3.Distance(
                transform.position,
                flatPlayerPos
            );

            m_IsEvading = distance <= m_EvadeDistance;

            if (!m_IsEvading)
                return;

            m_IsMoving = true;

            Vector3 evadeDirection =
                (transform.position - flatPlayerPos).normalized;

            if (evadeDirection != Vector3.zero)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(evadeDirection);

                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    m_RotationSpeed * 2f * Time.deltaTime
                );
            }
        }

        private IEnumerator RandomWalkRoutine()
        {
            while (true)
            {
                // If we are evading, pause the random walk state machine loops
                if (m_IsEvading)
                {
                    yield return null;
                    continue;
                }

                // Pick random direction
                float randomYRotation = Random.Range(0f, 360f);
                Quaternion targetRotation = Quaternion.Euler(0f, randomYRotation, 0f);

                // Rotate toward direction
                while (Quaternion.Angle(transform.rotation, targetRotation) > 1f && !m_IsEvading)
                {
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        m_RotationSpeed * Time.deltaTime
                    );

                    yield return null;
                }

                // Begin movement
                if (!m_IsEvading)
                {
                    m_IsMoving = true;
                }

                float moveDuration = Random.Range(m_MinMoveTime, m_MaxMoveTime);
                float moveTimer = 0f;
                
                // Instead of a hard WaitForSeconds, we yield smoothly so evade can interrupt immediately
                while (moveTimer < moveDuration && !m_IsEvading)
                {
                    moveTimer += Time.deltaTime;
                    yield return null;
                }

                // Stop movement
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

        private void Move()
        {
            float speed = m_MoveSpeed;

            if (m_IsEvading)
            {
                speed *= m_EvadeSpeedMultiplier;
            }

            transform.position += transform.forward * speed * Time.deltaTime;
        }

        private void AnimateWheels()
        {
            if (m_Wheels == null) return;

            float spinAmount = m_WheelSpinSpeed * Time.deltaTime;
            if (m_IsEvading) spinAmount *= m_EvadeSpeedMultiplier; // Match visual to speed boost

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

        public override void TakeDamage(AttackController attackController)
        {
            if (m_IsDead)
                return;
            m_Health -= attackController.AttackDamage;
            Vector3 hitPosition  = attackController.transform.position;
            Transform closestHolder = GetClosestParticleHolder(hitPosition);
            Vector3 spawnPosition = closestHolder != null ? closestHolder.position : hitPosition;

            if (m_Health <= 0f)
            {
                Debug.Log("Dead!");
                m_IsDead = true;
                m_IsMoving = false;
                m_IsEvading = false;
                StopAllCoroutines();
                m_ActiveFireParticle = Instantiate(
                    m_fireParticlePrefab,
                    spawnPosition,
                    Quaternion.identity,
                    closestHolder
                );
            }
            else
            {
                Debug.Log("Take Damage!");
                GameObject smokeParticleObj = Instantiate(
                    m_smokeParticlePrefab,
                    spawnPosition,
                    Quaternion.identity,
                    closestHolder
                );
                m_ActiveSmokeParticles.Add(smokeParticleObj);
            }
        }

        private Transform GetClosestParticleHolder(Vector3 hitPosition)
        {
            Transform closestHolder = null;
            float closestDistance = Mathf.Infinity;
            for (int i = 0; i < m_ParticleHolders.Length; i++)
            {
                Transform holder = m_ParticleHolders[i];
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
            
            // Helpful visualization for your evasion radius in editor
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_EvadeDistance);
        }
    }
}