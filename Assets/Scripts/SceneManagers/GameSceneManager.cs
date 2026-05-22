using UnityEngine;
using UnityEngine.SceneManagement;
using DroneSimulator.ObjectControllers;
using Unity.Mathematics;
using DroneSimulator.DroneControllers;
using DroneSimulator.EnemyControllers;
using System.Collections.Generic;
using System;

namespace DroneSimulator.SceneManagers
{
    public class GameSceneManager : MonoBehaviour
    {
        public static GameSceneManager Instance { get; private set; }
        [SerializeField] private GameObject m_PlayerDroneControllerPrefab;
        [SerializeField] private GameObject m_EnemyTankControllerPrefab;
        [SerializeField] private GameObject m_ReplayManagerPrefab;
        public bool IsPaused { get; private set; }
        private InputSystem_Actions m_inputSystem_Actions;
        private SpawnPadController m_SpawnPadController;
        private string m_currentSceneName;
        private PlayerDroneController m_PlayerDroneController;
        private ReplayManager m_ReplayManager;
        private List<EnemyController> m_EnemyControllers = new List<EnemyController>();
        public event Action OnPaused;
        [Header("Spawn Settings")]
        [SerializeField] private LayerMask m_ObstacleLayer; // Assign your obstacle layer in the Inspector
        [SerializeField] private float m_TankSpawnClearanceRadius = 3f; // Approximate safety radius around the tank hull  
        [SerializeField] private Vector3 m_TankSpawnPosition;      
        private void Awake()
        {
            m_inputSystem_Actions = new InputSystem_Actions();          
            m_SpawnPadController = FindAnyObjectByType<SpawnPadController>();
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            m_currentSceneName = scene.name;

            if (m_currentSceneName.ToLower().EndsWith("map"))
            {
                SetUpMapScene();
            }
        }

        private void SetUpPlayer()
        {
            if(m_PlayerDroneController==null)
            {
                GameObject playerDrone = Instantiate(
                    m_PlayerDroneControllerPrefab,
                    m_SpawnPadController.GetSpawnPointPosition(),
                    quaternion.identity
                );
                m_PlayerDroneController = playerDrone.GetComponent<PlayerDroneController>();
                m_PlayerDroneController.OnKilled += m_ReplayManager.StartReplay;                
                OnPaused += m_PlayerDroneController.PauseController;
                m_PlayerDroneController.InitializePlayerController(m_inputSystem_Actions);
            }
            else
            {
                m_PlayerDroneController.ResetDrone();
            }
        }

        private void SetUpMapScene()
        { 
            if (m_SpawnPadController == null) return;
            
            m_ReplayManager = Instantiate(m_ReplayManagerPrefab, Vector3.zero, Quaternion.identity).GetComponent<ReplayManager>();
            m_ReplayManager.OnReplayComplete += SetUpPlayer;
            m_ReplayManager.OnReplayComplete += UniversalPause;
            m_ReplayManager.OnReplayStart += UniversalPause;
            SetUpPlayer();

            GameObject enemyTankObject = SpawnEnemyTank(m_PlayerDroneController.transform.position, 15f);
            EnemyController enemyController = enemyTankObject.GetComponent<EnemyController>();
            
            m_EnemyControllers.Add(enemyController);
            enemyController.OnKilled += m_PlayerDroneController.EnemyKilled;
            enemyController.OnHit += m_PlayerDroneController.EnemyHit;
            OnPaused += enemyController.PauseController;
        }


        private GameObject SpawnEnemyTank(Vector3 playerPos, float minDistance)
        {

            // Default fallback position in case all 20 iterations land inside obstacles
            Vector3 spawnPos = m_TankSpawnPosition; 
            if(spawnPos==Vector3.zero)
            {
                spawnPos = playerPos + new Vector3(minDistance, 0f, 0f); 
                bool foundValidSpot = false;

                for (int i = 0; i < 20; i++)
                {
                    Vector3 offset = new Vector3(
                        UnityEngine.Random.Range(-50f, 50f),
                        0f,
                        UnityEngine.Random.Range(-50f, 50f)
                    );

                    Vector3 candidate = playerPos + offset;

                    // 1. Check distance condition
                    if (Vector3.Distance(candidate, playerPos) >= minDistance)
                    {
                        // 2. Check collision condition (slightly raise the center to ensure reliable 3D physics detection)
                        Vector3 checkOrigin = candidate + Vector3.up * 1f;
                        
                        bool insideObstacle = Physics.CheckSphere(
                            checkOrigin, 
                            m_TankSpawnClearanceRadius, 
                            m_ObstacleLayer, 
                            QueryTriggerInteraction.Ignore
                        );

                        if (!insideObstacle)
                        {
                            spawnPos = candidate;
                            foundValidSpot = true;
                            break;
                        }
                    }
                }

                // Optional: Log a warning if the map is too crowded and the loop timed out
                if (!foundValidSpot)
                {
                    Debug.LogWarning($"[SpawnManager] Could not find an obstacle-free spawn position after 20 attempts. Spawning at fallback position.");
                }
            }
            // Note: Replaced 'quaternion.identity' with Unity standard 'Quaternion.identity'
            return Instantiate(m_EnemyTankControllerPrefab, spawnPos, Quaternion.identity);
        }

        private void UniversalPause()
        {
            OnPaused?.Invoke();
            if(IsPaused)
            {
                IsPaused = false;
            }
            else
            {
                IsPaused = true;
            }
        }
    }
}