using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DroneSimulator.DroneControllers;
using System;
using DroneSimulator.EnemyControllers;
using DroneSimulator.AttackControllers;
using UnityEngine.Rendering;
using RetroShadersPro.URP;

namespace DroneSimulator.SceneManagers
{
    public class ReplayManager : MonoBehaviour
    {
        private Camera m_ReplayCamera;
        [SerializeField] private Vector3 m_ReplayCameraOffset =  new Vector3(3.5f, 0, 3.5f);
        public event Action OnReplayComplete;
        public event Action OnReplayStart;
        [SerializeField] private Volume m_VolumePrefab;
        private Volume m_Volume;
        private CRTSettings  m_ClonedCRTSettings;    
        int pixelSize;
        [Header("Obstacle Avoidance")]
        [SerializeField] private LayerMask m_ObstacleLayer; 
        [SerializeField] private float m_CameraClearanceRadius = 1f; // Safety buffer around the camera position        
        void Start()
        {
            m_Volume = Instantiate(m_VolumePrefab).GetComponent<Volume>();
            m_Volume.profile = Instantiate(m_Volume.sharedProfile);
            m_ReplayCamera = GetComponentInChildren<Camera>();
            if (m_Volume.profile.TryGet(out CRTSettings m_CRTSettings))
            {
                pixelSize = m_CRTSettings.pixelSize.value;
                m_ClonedCRTSettings = Instantiate(m_CRTSettings);
                m_Volume.profile.Remove<CRTSettings>();
                m_Volume.profile.components.Add(m_ClonedCRTSettings);            
            }
        }

        public void StartReplay(PlayerDroneController playerDroneController, EnemyController enemyController)
        {
            StartCoroutine(PlayReplay(playerDroneController, enemyController));
        }
        private IEnumerator PlayReplay(PlayerDroneController playerDroneController, EnemyController enemyController)
        {
            OnReplayStart?.Invoke();
            Vector3 playerAttackPosition = playerDroneController.transform.position; 
            m_ClonedCRTSettings.pixelSize.value = 15;             
            yield return new WaitForSecondsRealtime(0.64f); 
            m_ClonedCRTSettings.pixelSize.value = pixelSize;             
            m_ReplayCamera.enabled = true;

            Queue<ReplayableState> newQueue = new Queue<ReplayableState>(playerDroneController.ReplayBuffer);
            ReplayableState startState = newQueue.Dequeue();
            
            // --- GET THE FINAL STOP POSITION AHEAD OF TIME ---
            Vector3 startPosition = startState.Position;
            Vector3 endPosition = startPosition; // Fallback if buffer only has 1 item
            
            if (newQueue.Count > 0)
            {
                // Convert to array temporarily to peek at the very last element without destroying the queue
                ReplayableState[] queueArray = newQueue.ToArray();
                endPosition = queueArray[queueArray.Length - 1].Position;
            }
            // -------------------------------------------------

            Transform droneTransform = Instantiate(playerDroneController.ReplayModelPrefab, startState.Position, startState.Rotation).transform;
            ReplayDroneController replayDroneController = droneTransform.GetComponent<ReplayDroneController>();

            // --- NEW OBSTACLE-FREE CAMERA SPAWNING LOGIC (START & STOP VISIBLE) ---
            Vector3 defaultCameraPos = startPosition + m_ReplayCameraOffset;
            Vector3 finalCameraPos = defaultCameraPos; 
            bool foundValidSpot = false;

            for (int i = 0; i < 30; i++) // Increased iterations slightly to account for stricter rules
            {
                float distance = m_ReplayCameraOffset.magnitude;
                Vector3 randomOffset = UnityEngine.Random.onUnitSphere * distance;
                
                if (randomOffset.y < 1f) randomOffset.y = UnityEngine.Random.Range(2f, 5f);

                Vector3 candidatePos = startPosition + randomOffset;

                // 1. Check if camera physical body is inside an obstacle
                bool insideObstacle = Physics.CheckSphere(
                    candidatePos, 
                    m_CameraClearanceRadius, 
                    m_ObstacleLayer, 
                    QueryTriggerInteraction.Ignore
                );

                if (!insideObstacle)
                {
                    // 2. Line of sight check to START position
                    Vector3 dirToStart = startPosition - candidatePos;
                    bool startBlocked = Physics.Raycast(
                        candidatePos, 
                        dirToStart.normalized, 
                        dirToStart.magnitude, 
                        m_ObstacleLayer,
                        QueryTriggerInteraction.Ignore
                    );

                    // 3. Line of sight check to STOP position
                    Vector3 dirToEnd = endPosition - candidatePos;
                    bool endBlocked = Physics.Raycast(
                        candidatePos, 
                        dirToEnd.normalized, 
                        dirToEnd.magnitude, 
                        m_ObstacleLayer,
                        QueryTriggerInteraction.Ignore
                    );

                    // Candidate is golden only if both lines of sight are crystal clear
                    if (!startBlocked && !endBlocked)
                    {
                        finalCameraPos = candidatePos;
                        foundValidSpot = true;
                        break;
                    }
                }
            }

            if (!foundValidSpot)
            {
                Debug.LogWarning($"[ReplayManager] Could not find a camera position that clearly sees both start and stop locations. Using fallback position.");
            }

            // Apply the safely calculated position and look at the starting point
            transform.position = finalCameraPos;
            Vector3 direction = startPosition - transform.position;
            transform.rotation = Quaternion.LookRotation(direction);
            // ------------------------------------------------

            ReplayableObjectController replayEnemyController = null;
            Queue<ReplayableState> enemyQueue = null;
            if(enemyController != null)
            {
                enemyQueue = enemyController.SetUpReplayModel();
                // Clear the initial state to sync with the drone setup
                if(enemyQueue.Count > 0) enemyQueue.Dequeue(); 
                replayEnemyController = enemyController.m_ReplayModelObject.GetComponent<ReplayableObjectController>(); 
            }

            while (newQueue.Count > 0)
            {
                ReplayableState state = newQueue.Dequeue();
                replayDroneController.MoveReplayableObject(state);
                if(enemyController != null && enemyQueue.Count > 0)
                {
                    ReplayableState enemyState = enemyQueue.Dequeue();
                    replayEnemyController.MoveReplayableObject(enemyState);
                }

                // Keep smoothly tracking the moving drone model throughout the sequence
                direction = droneTransform.position - transform.position;
                transform.rotation = Quaternion.LookRotation(direction);
                yield return null; 
            }

            replayDroneController.Explode();
            yield return new WaitForSecondsRealtime(4f);
            m_ReplayCamera.enabled = false;
            OnReplayComplete?.Invoke();
            if(enemyController != null)
            {
                enemyController.RemoveReplayModel();
                enemyController.TakeDamage(playerDroneController.AttackPrefab.GetComponent<AttackController>(), playerAttackPosition);
            }
        }
    }
}