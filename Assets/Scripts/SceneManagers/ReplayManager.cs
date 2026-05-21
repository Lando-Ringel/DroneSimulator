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
            m_ClonedCRTSettings.pixelSize.value = 15;             // Pixelate heavily on impact
            // Keep the live camera active or freeze the screen for a brief moment of heavy static
            yield return new WaitForSecondsRealtime(0.64f); 
            m_ClonedCRTSettings.pixelSize.value = pixelSize;             // Pixelate heavily on impact
            m_ReplayCamera.enabled = true;
            Queue<ReplayableState> newQueue = new Queue<ReplayableState>(playerDroneController.ReplayBuffer);
            ReplayableState state = newQueue.Dequeue();
            Transform droneTransform = Instantiate(playerDroneController.ReplayModelPrefab, state.Position, state.Rotation).transform;
            ReplayDroneController replayDroneController = droneTransform.GetComponent<ReplayDroneController>();
            transform.position = state.Position + m_ReplayCameraOffset;
            Vector3 direction = droneTransform.position - transform.position;
            transform.rotation = Quaternion.LookRotation(direction);
            ReplayableObjectController replayEnemyController = null;
            Queue<ReplayableState> enemyQueue = null;
            if(enemyController!=null)
            {
                enemyQueue = enemyController.SetUpReplayModel();
                state = enemyQueue.Dequeue();
                replayEnemyController = enemyController.m_ReplayModelObject.GetComponent<ReplayableObjectController>(); 
                
            }
            while (newQueue.Count > 0)
            {
                state = newQueue.Dequeue();
                replayDroneController.MoveReplayableObject(state);
                if(enemyController!=null)
                {
                    state = enemyQueue.Dequeue();
                    replayEnemyController.MoveReplayableObject(state);
                }
                direction = droneTransform.position - transform.position;
                transform.rotation = Quaternion.LookRotation(direction);
                yield return null; // Wait for next frame
            }

            replayDroneController.Explode();
            yield return new WaitForSecondsRealtime(4f);
            m_ReplayCamera.enabled = false;
            OnReplayComplete?.Invoke();
            if(enemyController!=null)
            {
                enemyController.RemoveReplayModel();
                enemyController.TakeDamage(playerDroneController.AttackPrefab.GetComponent<AttackController>(), playerAttackPosition);
            }
        }
    }
}