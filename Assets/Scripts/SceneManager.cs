using UnityEngine;
using UnityEngine.SceneManagement;
using DroneSimulator.ObjectControllers;
using Unity.Mathematics;
using DroneSimulator.DroneControllers;
using DroneSimulator.EnemyControllers;
namespace DroneSimulator
{
    public class GameSceneManager : MonoBehaviour
    {
        public static GameSceneManager Instance { get; private set; }

        [SerializeField] private GameObject playerDroneControllerPrefab;
        [SerializeField] private GameObject enemyTankControllerPrefab;

        private string m_currentSceneName;

        private void Awake()
        {
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

        private void SetUpMapScene()
        {
            SpawnPadController spawnPadController = FindAnyObjectByType<SpawnPadController>();
            if (spawnPadController == null) return;

            GameObject playerDrone = Instantiate(
                playerDroneControllerPrefab,
                spawnPadController.GetSpawnPointPosition(),
                quaternion.identity
            );
            PlayerDroneController playerDroneController = playerDrone.GetComponent<PlayerDroneController>();
            GameObject enemyTankObject = SpawnEnemyTank(playerDrone.transform.position, 15f);
            EnemyController enemyController = enemyTankObject.GetComponent<EnemyController>();
            enemyController.OnKilled += playerDroneController.EnemyKilled;
            enemyController.OnHit += playerDroneController.EnemyHit;
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

            return Instantiate(enemyTankControllerPrefab, spawnPos, quaternion.identity);
        }
    }
}