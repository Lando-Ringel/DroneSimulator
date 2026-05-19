using UnityEngine;
using DroneSimulator.EnemyControllers;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

namespace DroneSimulator.DroneControllers
{
    public class ReplayDroneController : ReplayableObjectController
    {
        [SerializeField] protected GameObject m_explosionPrefab;
        public void Explode()
        {
            Instantiate(m_explosionPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
        public override void MoveReplayableObject(ReplayableState replayableState)
        {
            transform.position = replayableState.Position;
            transform.rotation = replayableState.Rotation;
        }


    }


}