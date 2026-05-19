using UnityEngine;
using UnityEngine.SceneManagement;
using DroneSimulator.ObjectControllers;
using Unity.Mathematics;
using DroneSimulator.DroneControllers;
using DroneSimulator.EnemyControllers;
using System.Collections.Generic;
namespace DroneSimulator
{
    public class GameSceneManager : MonoBehaviour
    {
        public static GameSceneManager Instance { get; private set; }

        [SerializeField] private GameObject m_PlayerDroneControllerPrefab;
        [SerializeField] private GameObject m_EnemyTankControllerPrefab;
        [SerializeField] private GameObject m_ReplayManagerPrefab;
        private SpawnPadController m_SpawnPadController;
        private string m_currentSceneName;
        private PlayerDroneController m_PlayerDroneController;
        private ReplayManager m_ReplayManager;
        private List<EnemyController> m_EnemyControllers = new List<EnemyController>();
        private void Awake()
        {
            
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
            }
            else
            {
                m_PlayerDroneController.ResetDrone(default);
            }
        }

        private void SetUpMapScene()
        { 
            if (m_SpawnPadController == null) return;
            
            m_ReplayManager = Instantiate(m_ReplayManagerPrefab, Vector3.zero, Quaternion.identity).GetComponent<ReplayManager>();
            m_ReplayManager.OnReplayComplete += SetUpPlayer;
            
            SetUpPlayer();

            GameObject enemyTankObject = SpawnEnemyTank(m_PlayerDroneController.transform.position, 15f);
            EnemyController enemyController = enemyTankObject.GetComponent<EnemyController>();
            m_EnemyControllers.Add(enemyController);
            enemyController.OnKilled += m_PlayerDroneController.EnemyKilled;
            enemyController.OnHit += m_PlayerDroneController.EnemyHit;
        }

        private GameObject SpawnEnemyTank(Vector3 playerPos, float minDistance)
        {
            Vector3 spawnPos = playerPos;

            for (int i = 0; i < 20; i++)
            {
                Vector3 offset = new Vector3(
                    UnityEngine.Random.Range(-50f, 50f),
                    0f,
                    UnityEngine.Random.Range(-50f, 50f)
                );

                Vector3 candidate = playerPos + offset;

                if (Vector3.Distance(candidate, playerPos) >= minDistance)
                {
                    spawnPos = candidate;
                    break;
                }
            }
            return Instantiate(m_EnemyTankControllerPrefab, spawnPos, quaternion.identity);
        }
    }
}