using UnityEngine;
using DroneSimulator.EnemyControllers;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

namespace DroneSimulator.DroneControllers
{
    public abstract class ReplayableObjectController : MonoBehaviour
    {
        public abstract void MoveReplayableObject(ReplayableState replayableState);
        
    }


}