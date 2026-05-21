using UnityEngine;
using DroneSimulator.EnemyControllers;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

namespace DroneSimulator.DroneControllers
{
    public class ReplayTankController : ReplayableObjectController
    {
        public Transform m_ParticleHoldersTransform;


        public override void MoveReplayableObject(ReplayableState replayableState)
        {
            transform.position = replayableState.Position;
            transform.rotation = replayableState.Rotation;            
        }
        
    }


}