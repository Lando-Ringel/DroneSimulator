using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DroneSimulator.DroneControllers;
using System;
using DroneSimulator.EnemyControllers;
using DroneSimulator.AttackControllers;

public class ReplayManager : MonoBehaviour
{
    private Camera m_ReplayCamera;
    [SerializeField] private Vector3 m_ReplayCameraOffset =  new Vector3(3.5f, 0, 3.5f);

    public event Action OnReplayComplete;

    public void StartReplay(PlayerDroneController playerDroneController, EnemyController enemyController)
    {

        StartCoroutine(PlayReplay(playerDroneController, enemyController));
    }
    void Start()
    {
        m_ReplayCamera = GetComponentInChildren<Camera>();
    }

    private IEnumerator PlayReplay(PlayerDroneController playerDroneController, EnemyController enemyController)
    {
        m_ReplayCamera.enabled = true;
        Queue<ReplayableState> newQueue = new Queue<ReplayableState>(playerDroneController.ReplayBuffer);
        ReplayableState state = newQueue.Dequeue();
        Transform droneTransform = Instantiate(playerDroneController.ReplayModelPrefab, state.Position, state.Rotation).transform;
        ReplayDroneController replayDroneController = droneTransform.GetComponent<ReplayDroneController>();
        transform.position = state.Position + m_ReplayCameraOffset;
        Vector3 direction = droneTransform.position - transform.position;
        transform.rotation = Quaternion.LookRotation(direction);
        Transform enemyTransform = null;
        ReplayableObjectController replayEnemyController = null;
        Queue<ReplayableState> enemyQueue = null;
        if(enemyController!=null)
        {
            enemyQueue = new Queue<ReplayableState>(enemyController.ReplayBuffer);
            enemyController.gameObject.SetActive(false);
            state = enemyQueue.Dequeue();
            enemyTransform = Instantiate(enemyController.ReplayModelPrefab, state.Position, state.Rotation).transform;
            replayEnemyController = enemyTransform.GetComponent<ReplayableObjectController>(); 
            
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
        yield return new WaitForSeconds(4f);
        m_ReplayCamera.enabled = false;
        OnReplayComplete?.Invoke();
        if(enemyController!=null && playerDroneController.DroneType==DroneControllerType.Kamikaze)
        {
            Destroy(enemyTransform.gameObject);
            enemyController.TakeDamage(playerDroneController.AttackPrefab.GetComponent<AttackController>());
            enemyController.gameObject.SetActive(true);
        }
        

    }
}